# Adım 1: Identity, Roller ve Giriş/Kayıt — Implementation Plan

**Goal:** Kullanıcılar üye olup giriş yapabilsin; üç rol ve Admin hesabı (g231210566@sakarya.edu.tr / sau) uygulama açılışında hazır olsun; Admin alanı yalnızca Admin'e açık olsun.

**Architecture:** Identity ayarları tek bir extension metodunda (`AddVetKlinikIdentity`) toplanır; hem uygulama hem testler onu kullanır. Kayıt kuralı `AccountService` içindedir (kayıt her zaman `Uye` rolü verir); controller yalnızca formu bağlar. `DbSeeder` açılışta migration uygular ve roller + Admin'i idempotent oluşturur.

**Tech Stack:** ASP.NET Core Identity (EF stores), xUnit, `Microsoft.AspNetCore.Mvc.Testing`.

**Spec:** `docs/specs/2026-11-24-veteriner-sistem-design.md`

## Global Constraints

- Rol adları tam olarak: `Admin`, `Veteriner`, `Uye` (TestData ile aynı).
- Admin: `g231210566@sakarya.edu.tr` / `sau`. "sau" parola politikasını geçemez; Admin, `UserManager.CreateAsync(user)` (parolasız) + `PasswordHasher` ile oluşturulur. Diğer kullanıcılar için politika: en az 6 karakter, en az bir rakam, büyük harf/özel karakter zorunlu değil.
- Arayüz metinleri ve hata mesajları Türkçe.
- Parola/bağlantı dizesi depoya girmez.
- Testler: `dotnet test` (yerelde: user-secrets).

## Review Focus

1. Aynı e-posta ile ikinci kayıt → Türkçe "zaten kayıtlı" hatası, ikinci kullanıcı oluşmaz.
2. Uygulama iki kez başlatılırsa seeder ikinci Admin/rol oluşturmaz.
3. Kayıt formuna gizli `Role=Admin` alanı eklenirse yok sayılır; kullanıcı yine `Uye` olur (ViewModel'de rol alanı yok).
4. Giriş sonrası `returnUrl` dış bir siteye ise yönlendirme yapılmaz (`Url.IsLocalUrl`).
5. Üye `/Admin`'e giderse AccessDenied; anonim kullanıcı Login'e yönlendirilir.

---

### Task 1: Identity kurulumu, roller ve seeder

**Files:**
- Create: `src/VetKlinik.Web/Auth/Roles.cs`, `src/VetKlinik.Web/Auth/IdentitySetup.cs`, `src/VetKlinik.Web/Auth/TurkishIdentityErrorDescriber.cs`, `src/VetKlinik.Web/Data/DbSeeder.cs`
- Modify: `src/VetKlinik.Web/Program.cs`, `src/VetKlinik.Web/appsettings.json`
- Test: `tests/VetKlinik.Tests/Infrastructure/TestServices.cs`, `tests/VetKlinik.Tests/Services/DbSeederTests.cs`

**Interfaces:**
- Produces:
  - `static class Roles { const string Admin="Admin", Veteriner="Veteriner", Uye="Uye"; static readonly string[] All; }`
  - `static IServiceCollection AddVetKlinikIdentity(this IServiceCollection)` — `AddIdentity<ApplicationUser, IdentityRole>` + EF stores + token providers + parola politikası + `TurkishIdentityErrorDescriber` + cookie yolları (`/Account/Login`, `/Account/AccessDenied`).
  - `class SeedOptions { string AdminEmail; string AdminPassword; string AdminFullName; }` — `appsettings.json` → `"Seed": { "AdminEmail": "g231210566@sakarya.edu.tr", "AdminPassword": "sau", "AdminFullName": "Sistem Yöneticisi" }` (ödevde açıkça verilen parola; gizli değil).
  - `static Task DbSeeder.SeedAsync(IServiceProvider services)` — rolleri ve Admin'i yoksa oluşturur. Program.cs: `MigrateAsync()` + `SeedAsync` açılışta (scope içinde).
  - Test yardımcı: `TestServices.Build(string connectionString) -> ServiceProvider` (AddLogging + AddDbContext + AddVetKlinikIdentity + Seed options).

- [ ] **Step 1: Failing tests** (`DbSeederTests`, `IClassFixture<SqlServerFixture>`, her test başında `ResetAsync` sonrası `Users`/`Roles` boşaltılır)
  - `Creates_three_roles`: `roleManager.Roles.Select(r=>r.Name)` ⊇ {Admin, Veteriner, Uye}.
  - `Creates_admin_with_sau_password`: `FindByEmailAsync("g231210566@sakarya.edu.tr")` dolu, `CheckPasswordAsync(admin,"sau")` true, `IsInRoleAsync(admin,"Admin")` true.
  - `Running_twice_does_not_duplicate`: iki kez `SeedAsync` → Admin e-postalı kullanıcı sayısı 1, rol sayısı 3.
- [ ] **Step 2:** `dotnet test --filter DbSeederTests` → derleme hatası (tipler yok).
- [ ] **Step 3:** Yukarıdaki tipleri yaz; Program.cs'de `AddVetKlinikIdentity()`, `UseAuthentication()` (UseAuthorization'dan önce), açılışta migrate + seed.
- [ ] **Step 4:** `dotnet test` → hepsi PASS (eski 12 test dahil).
- [ ] **Step 5:** Commit `feat: Identity, roller ve admin seed`.

### Task 2: Kayıt, giriş, çıkış ve Admin alanı

**Files:**
- Create: `src/VetKlinik.Web/Services/AccountService.cs`, `src/VetKlinik.Web/Services/ServiceResult.cs`, `src/VetKlinik.Web/Controllers/AccountController.cs`, `src/VetKlinik.Web/Models/Account/RegisterViewModel.cs`, `src/VetKlinik.Web/Models/Account/LoginViewModel.cs`, `Views/Account/{Login,Register,AccessDenied}.cshtml`, `Views/Shared/_LoginPartial.cshtml`, `Areas/Admin/Controllers/HomeController.cs`, `Areas/Admin/Views/Home/Index.cshtml` (+ `_ViewImports`, `_ViewStart`)
- Modify: `Views/Shared/_Layout.cshtml` (Türkçe menü, `lang="tr"`, marka "VetKlinik", login partial, Admin linki yalnızca Admin'e), `Program.cs` (area route `{area:exists}/{controller=Home}/{action=Index}/{id?}`)
- Test: `tests/VetKlinik.Tests/Services/AccountServiceTests.cs`, `tests/VetKlinik.Tests/Web/AuthorizationTests.cs`

**Interfaces:**
- Consumes: `Roles`, `AddVetKlinikIdentity`, `TestServices.Build`.
- Produces:
  - `record ServiceResult(bool Succeeded, IReadOnlyList<string> Errors)` + `static Ok()`, `static Fail(params string[])`; generic `ServiceResult<T>` (Value). Sonraki tüm servisler bunu döndürür.
  - `AccountService(UserManager<ApplicationUser>)`: `Task<ServiceResult> RegisterAsync(RegisterViewModel model)` — kullanıcı oluşturur, `Uye` rolüne ekler.
  - `RegisterViewModel { FullName [Required, StringLength(100)], Email [Required, EmailAddress], Password [Required, DataType.Password, MinLength(6)], ConfirmPassword [Compare(Password)] }` — Türkçe `ErrorMessage`'lar. Rol alanı YOK.
  - `LoginViewModel { Email, Password, RememberMe }`.
  - `AccountController`: `Login` GET/POST (`returnUrl` yalnızca `Url.IsLocalUrl` ise), `Register` GET/POST (başarılıysa otomatik giriş), `Logout` POST, `AccessDenied` GET. POST'larda `[ValidateAntiForgeryToken]`, formlarda `_ValidationScriptsPartial`.
  - `Areas/Admin/HomeController` `[Area("Admin"), Authorize(Roles = Roles.Admin)]` — şimdilik özet sayfası (kullanıcı sayısı).

- [ ] **Step 1: Failing tests**
  - `AccountServiceTests.Register_creates_member_in_Uye_role`: sonuç Succeeded, kullanıcı `IsInRoleAsync(u,"Uye")`.
  - `AccountServiceTests.Register_rejects_duplicate_email`: aynı e-posta ikinci kez → `Succeeded == false`, `Errors` "zaten" içerir; e-postalı kullanıcı sayısı 1.
  - `AuthorizationTests` (`WebApplicationFactory<Program>`, `UseSetting("ConnectionStrings:Default", fixture.ConnectionString)`, `AllowAutoRedirect=false`):
    - `Anonymous_admin_request_redirects_to_login`: GET `/Admin` → 302, Location `/Account/Login` içerir.
    - `Login_page_renders`: GET `/Account/Login` → 200.
- [ ] **Step 2:** `dotnet test` → yeni testler FAIL.
- [ ] **Step 3:** Yukarıdaki dosyaları yaz; `AccountService` DI'ya `AddScoped`.
- [ ] **Step 4:** `dotnet test` → hepsi PASS. Uygulamayı çalıştırıp el ile: kayıt → giriş → çıkış; Admin ile `/Admin` açılır; üye ile AccessDenied.
- [ ] **Step 5:** Commit `feat: üye kaydı, giriş/çıkış ve admin alanı`, push.
