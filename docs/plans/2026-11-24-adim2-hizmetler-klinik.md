# Adım 2: Hizmetler ve Klinik Çalışma Saatleri — Implementation Plan

**Goal:** Admin, kliniğin sunduğu hizmetleri (ad, açıklama, süre, ücret, aktif/pasif) CRUD ile ve kliniğin haftalık çalışma saatlerini yönetebilsin; ana sayfa aktif hizmetleri ve çalışma saatlerini herkese göstersin.

**Architecture:** İş kuralları `Services/` altındaki `ServiceCatalogService` ve `ClinicService` içinde, `ServiceResult` döndürür (Adım 1). Admin controller'ları (`Areas/Admin`) yalnızca form bağlar. Seeder tek kliniği ve örnek hizmetleri yoksa oluşturur.

**Tech Stack:** EF Core, ASP.NET Core MVC Areas, xUnit.

**Spec:** `docs/specs/2026-11-24-veteriner-sistem-design.md`

## Global Constraints

- Tek klinik. `ClinicService` her zaman ilk (tek) kliniği kullanır; seeder onu oluşturur.
- Hata/arayüz metinleri Türkçe. Silme işlemleri yalnızca POST + antiforgery.
- Randevusu olan hizmet silinemez (DB zaten `Restrict`); servis bunu önceden kontrol edip Türkçe mesaj döner: "Bu hizmete ait randevular olduğu için silinemez. Bunun yerine pasif hale getirebilirsiniz."
- Hizmet süresi 10–480 dk, ücret 0–100000 ₺. Hizmet adı benzersiz (büyük/küçük harf duyarsız).
- Çalışma saatleri: açık günlerde `Opens < Closes`; hata: "{Gün}: kapanış saati açılış saatinden sonra olmalıdır."
- Gün adları Türkçe (Pazartesi…Pazar), hafta Pazartesi'den başlar.

## Review Focus

1. Pasif hizmet ana sayfada görünmez ama admin listesinde görünür.
2. Var olmayan id ile Edit/Delete → 404.
3. Aynı adı farklı harf büyüklüğüyle ("muayene") eklemek → reddedilir.
4. Kapalı işaretli günün saatleri doğrulanmaz (kapalı günde 00:00–00:00 geçerli).
5. Ücret alanına Türkçe ondalık ("499,99") yazılırsa sunucu doğru okur (invariant/tr kültür uyumu).

---

### Task 1: Servisler ve seed

**Files:**
- Create: `src/VetKlinik.Web/Services/ServiceCatalogService.cs`, `src/VetKlinik.Web/Services/ClinicService.cs`, `src/VetKlinik.Web/Models/Admin/ServiceFormModel.cs`, `src/VetKlinik.Web/Models/Admin/ClinicFormModel.cs`, `src/VetKlinik.Web/Infrastructure/TurkishDays.cs`
- Modify: `src/VetKlinik.Web/Data/DbSeeder.cs`, `src/VetKlinik.Web/Program.cs` (DI)
- Test: `tests/VetKlinik.Tests/Services/ServiceCatalogServiceTests.cs`, `tests/VetKlinik.Tests/Services/ClinicServiceTests.cs`, `tests/VetKlinik.Tests/Services/DbSeederTests.cs`

**Interfaces:**
- Produces:
  - `ServiceFormModel { string Name; string? Description; int DurationMinutes; decimal Price; bool IsActive }` (DataAnnotations, Türkçe mesajlar).
  - `ServiceCatalogService(AppDbContext)`: `Task<List<Service>> ListAsync(bool includeInactive)` (ada göre sıralı), `Task<Service?> FindAsync(int id)`, `Task<ServiceResult<Service>> CreateAsync(ServiceFormModel)`, `Task<ServiceResult> UpdateAsync(int id, ServiceFormModel)` (yoksa Fail "Hizmet bulunamadı."), `Task<ServiceResult> DeleteAsync(int id)`.
  - `ClinicFormModel { string Name; string Address; string Phone; List<ClinicDayModel> Days }`, `ClinicDayModel { DayOfWeek Day; bool IsClosed; TimeOnly Opens; TimeOnly Closes }`.
  - `ClinicService(AppDbContext)`: `Task<Clinic> GetAsync()` (Hours dahil), `Task<ClinicFormModel> GetFormAsync()` (Pazartesi→Pazar sırası), `Task<ServiceResult> UpdateAsync(ClinicFormModel)`.
  - `TurkishDays`: `static string Name(DayOfWeek)`, `static readonly DayOfWeek[] Week` (Pazartesi başlangıçlı).
  - Seeder: klinik yoksa "VetKlinik Sakarya" (Hafta içi 09:00–18:00, Cumartesi 09:00–14:00, Pazar kapalı); hiç hizmet yoksa: Muayene 30dk 500₺, Aşı 15dk 350₺, Kısırlaştırma 90dk 3000₺, Diş Temizliği 60dk 1500₺, Tıraş ve Bakım 45dk 600₺.

- [ ] **Step 1: Failing tests**
  - `ServiceCatalogServiceTests`: `Create_adds_service`, `Create_rejects_duplicate_name_case_insensitive` ("muayene" seed'deki "Muayene" ile çakışır), `Update_unknown_id_fails`, `Delete_with_appointment_is_refused_with_message` (Errors içinde "pasif"; hizmet hâlâ var), `Delete_without_appointment_removes`, `List_without_inactive_hides_passive`.
  - `ClinicServiceTests`: `Update_saves_hours_and_info`, `Update_rejects_closing_before_opening` (Errors "Pazartesi" içerir), `Closed_day_hours_are_not_validated`, `Form_starts_on_monday`.
  - `DbSeederTests.Seeds_clinic_and_services_once` (iki kez seed → 1 klinik, 5 hizmet).
- [ ] **Step 2:** `dotnet test` → derleme hatası/FAIL.
- [ ] **Step 3:** Uygula; DI'ya `AddScoped`.
- [ ] **Step 4:** `dotnet test` → hepsi PASS.
- [ ] **Step 5:** Commit `feat: hizmet ve klinik servisleri`.

### Task 2: Admin ekranları ve ana sayfa

**Files:**
- Create: `Areas/Admin/Controllers/ServicesController.cs`, `Areas/Admin/Controllers/ClinicController.cs`, `Areas/Admin/Views/Services/{Index,Create,Edit,Delete,_Form}.cshtml`, `Areas/Admin/Views/Clinic/Edit.cshtml`, `Areas/Admin/Views/Shared/_AdminNav.cshtml`
- Modify: `Areas/Admin/Views/Home/Index.cshtml` (nav + hizmet sayısı), `Controllers/HomeController.cs` + `Views/Home/Index.cshtml` (aktif hizmetler + çalışma saatleri), `Program.cs` (tr-TR request culture: `RequestLocalizationOptions` varsayılan "tr-TR")
- Test: `tests/VetKlinik.Tests/Web/AdminPagesTests.cs`

**Interfaces:**
- Consumes: Task 1 servisleri, `Roles.Admin`.
- Produces: `/Admin/Services`, `/Admin/Services/Create|Edit/{id}|Delete/{id}`, `/Admin/Clinic/Edit`. Controller'lar `[Area("Admin"), Authorize(Roles = Roles.Admin)]`. Başarılı işlemden sonra `TempData["Success"]` ile Türkçe mesaj.

- [ ] **Step 1: Failing tests** (`WebApplicationFactory` tek örnek, `IClassFixture` ile; admin olarak giriş yapan yardımcı `LoginAsAdminAsync(HttpClient)` antiforgery token'ı Login sayfasından okur)
  - `Anonymous_services_page_redirects_to_login`
  - `Admin_sees_seeded_services` (GET `/Admin/Services` → 200, "Kısırlaştırma" içerir)
  - `Admin_creates_service_with_turkish_decimal` (POST "Price=499,99" → sonra liste "499,99" içerir)
  - `Edit_unknown_service_returns_404`
  - `Home_page_shows_active_services_and_hours` ("Muayene" ve "Pazartesi" içerir)
- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3:** Controller ve view'ları yaz; formlarda `_ValidationScriptsPartial`; jQuery validate için Türkçe ondalık: `site.js`'de `$.validator.methods.number` virgülü kabul edecek şekilde genişlet.
- [ ] **Step 4:** `dotnet test` → PASS; uygulamada el ile: hizmet ekle/düzenle/sil, randevulu hizmeti silme mesajı, saat düzenleme.
- [ ] **Step 5:** Commit `feat: admin hizmet ve çalışma saati yönetimi`, push.
