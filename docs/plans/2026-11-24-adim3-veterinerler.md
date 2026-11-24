# Adım 3: Veteriner Hekim Yönetimi — Implementation Plan

**Goal:** Admin, hekimleri (giriş hesabıyla birlikte), uzmanlık alanlarını, her hekimin verebildiği hizmetleri ve haftalık müsaitlik saatlerini yönetebilsin.

**Architecture:** `VeterinarianService` ve `SpecialtyService` (`Services/`), `ServiceResult` döndürür. Hekim oluşturma = Identity kullanıcısı (`Veteriner` rolü) + `Veterinarian` satırı, tek transaction içinde. Admin controller'ları yalnızca form bağlar.

**Tech Stack:** EF Core, Identity `UserManager`, MVC Areas, xUnit.

**Spec:** `docs/specs/2026-11-24-veteriner-sistem-design.md`

## Global Constraints

- Hekim hesabını yalnızca Admin açar; parola politikası Adım 1 ile aynı (≥6, rakam).
- Müsaitlik: gün başına en fazla bir aralık (form 7 satır, Pazartesi başlangıçlı). Çalışılan günde `Starts < Ends` ve aralık kliniğin o günkü saatleri içinde olmalı; klinik kapalıysa o gün çalışılamaz. Hatalar gün adıyla Türkçe: "{Gün}: …".
- Randevusu olan hekim silinemez: "Bu hekime ait randevular olduğu için silinemez. Bunun yerine pasif hale getirebilirsiniz." Silinebilen hekimin kullanıcı hesabı da silinir.
- Kullanımda olan (bir hekime atanmış) uzmanlık silinemez: "Bu uzmanlık alanı hekimlere atanmış olduğu için silinemez." Uzmanlık adı Türkçe harmanlamayla benzersiz.
- Hizmet seçiminde pasif hizmetler listelenmez, ama hekimde zaten kayıtlıysa korunur.
- E-posta düzenlemede değiştirilemez (girişte kullanıcı adıdır).

## Review Focus

1. Hekim oluşturmada e-posta zaten kayıtlıysa → Türkçe hata, yarım `Veterinarian`/kullanıcı kalmaz.
2. Formdan var olmayan uzmanlık/hizmet id'si gelirse yok sayılır (500 değil).
3. Hekimin müsaitliği kliniğin kapalı olduğu güne (Pazar) konamaz.
4. Pasif hekim listede "Pasif" görünür; giriş yapabilir (randevuları görebilsin) ama ileride randevu alınamaz.
5. Var olmayan id ile Edit/Delete → 404.

---

### Task 1: Servisler

**Files:**
- Create: `Services/VeterinarianService.cs`, `Services/SpecialtyService.cs`, `Models/Admin/VeterinarianFormModel.cs`
- Modify: `Program.cs` (DI), `Data/DbSeeder.cs` (uzmanlıklar: Küçük Hayvan, Egzotik Hayvan, Cerrahi, Dermatoloji, Dahiliye — yoksa)
- Test: `tests/VetKlinik.Tests/Services/VeterinarianServiceTests.cs`, `tests/VetKlinik.Tests/Services/SpecialtyServiceTests.cs`

**Interfaces:**
- Consumes: `ClinicService.GetAsync()`, `TurkishDays`, `Roles.Veteriner`, `ServiceResult`.
- Produces:
  - `VeterinarianFormModel { string FullName; string Email; string? Password; string Title; bool IsActive; List<int> SpecialtyIds; List<int> ServiceIds; List<AvailabilityDayModel> Days }`; `AvailabilityDayModel { DayOfWeek Day; bool Works; TimeOnly? Starts; TimeOnly? Ends }`. `Password` yalnızca oluşturmada zorunlu (servis kontrol eder).
  - `VeterinarianService(AppDbContext, UserManager<ApplicationUser>, ClinicService)`:
    `Task<List<Veterinarian>> ListAsync()` (User, Specialties, Services dahil; ada göre),
    `Task<Veterinarian?> FindAsync(int id)` (tüm ilişkiler dahil),
    `Task<VeterinarianFormModel> NewFormAsync()` (7 gün, çalışılmıyor),
    `Task<VeterinarianFormModel?> GetFormAsync(int id)`,
    `Task<ServiceResult<Veterinarian>> CreateAsync(VeterinarianFormModel)`,
    `Task<ServiceResult> UpdateAsync(int id, VeterinarianFormModel)`,
    `Task<ServiceResult> DeleteAsync(int id)`.
  - `SpecialtyService(AppDbContext)`: `ListAsync()` (hekim sayısıyla: `List<(Specialty, int VetCount)>` yerine `record SpecialtyRow(int Id, string Name, int VeterinarianCount)`), `CreateAsync(string name)`, `DeleteAsync(int id)`.

- [ ] **Step 1: Failing tests**
  - `Create_makes_user_in_Veteriner_role_with_services_and_hours`
  - `Create_with_taken_email_fails_and_leaves_nothing` (e-posta seed'deki `ownera@test.local`; Veterinarians sayısı değişmez)
  - `Create_requires_password`
  - `Availability_outside_clinic_hours_is_rejected` (Pazartesi 08:00–12:00 → "Pazartesi")
  - `Availability_on_closed_day_is_rejected` (Pazar)
  - `Unknown_specialty_and_service_ids_are_ignored`
  - `Update_replaces_services_specialties_and_hours`
  - `Delete_with_appointment_is_refused`; `Delete_without_appointment_removes_vet_and_user`
  - `SpecialtyServiceTests`: `Create_rejects_duplicate_turkish_name` ("cerrahi" vs seed "Cerrahi"), `Delete_in_use_is_refused`, `Delete_unused_removes`
- [ ] **Step 2:** `dotnet test` → FAIL (derleme).
- [ ] **Step 3:** Uygula. Oluşturma: `db.Database.BeginTransactionAsync()` → `UserManager.CreateAsync` → `AddToRoleAsync` → `Veterinarian` ekle → commit; herhangi bir hata → rollback + Fail.
- [ ] **Step 4:** `dotnet test` → PASS.
- [ ] **Step 5:** Commit `feat: hekim ve uzmanlık servisleri`.

### Task 2: Admin ekranları

**Files:**
- Create: `Areas/Admin/Controllers/VeterinariansController.cs`, `Areas/Admin/Controllers/SpecialtiesController.cs`, `Areas/Admin/Views/Veterinarians/{Index,Create,Edit,Delete,_Form}.cshtml`, `Areas/Admin/Views/Specialties/Index.cshtml`
- Modify: `Areas/Admin/Views/Shared/_AdminNav.cshtml` (Hekimler, Uzmanlıklar), `Areas/Admin/Views/Home/Index.cshtml` + `Areas/Admin/Controllers/HomeController.cs` (aktif hekim sayısı), `Views/Home/Index.cshtml` + `Controllers/HomeController.cs` + `Models/HomeViewModel.cs` (aktif hekimler ve uzmanlıkları)
- Test: `tests/VetKlinik.Tests/Web/AdminVeterinarianPagesTests.cs`

**Interfaces:**
- Consumes: Task 1 servisleri, `ServiceCatalogService.ListAsync(false)`.
- Produces: `/Admin/Veterinarians[/Create|/Edit/{id}|/Delete/{id}]`, `/Admin/Specialties` (liste + ekleme formu + satırda sil, POST). Formda uzmanlık ve hizmetler checkbox listesi, müsaitlik 7 satır tablo (çalışıyor checkbox + iki `type=time`).

- [ ] **Step 1: Failing tests**
  - `Admin_creates_veterinarian_who_can_log_in` (POST Create → 302; yeni hekim e-posta/parolasıyla Login → 302 ve `/Admin` → AccessDenied yönlendirmesi)
  - `Edit_unknown_veterinarian_returns_404`
  - `Admin_adds_specialty` (POST → liste yeni adı içerir)
  - `Home_page_lists_active_veterinarians`
- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3:** Uygula.
- [ ] **Step 4:** `dotnet test` → PASS; tarayıcıda: hekim ekle (hizmet, saat), saat hatası, hekim girişi, uzmanlık ekle/sil.
- [ ] **Step 5:** Commit `feat: admin hekim ve uzmanlık yönetimi`, push.
