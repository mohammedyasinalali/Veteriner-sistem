# Adım 4: Evcil Hayvanlar ve Sahiplik Yetkisi — Implementation Plan

**Goal:** Üye kendi hayvanlarını (ad, tür, ırk, doğum tarihi, kilo, isteğe bağlı fotoğraf) ekleyip düzenleyebilsin ve silebilsin; başka bir üyenin hayvanını URL'deki id'yi değiştirerek göremesin.

**Architecture:** Yetki kararı `PetService` içindedir: her metot `CurrentUser` alır ve yalnızca o kullanıcının görebileceği kayıtlar üzerinden sorgu yapar (`Visible(user)`). Görünmeyen kayıt için controller 404 döner (403 değil; kaydın varlığı da gizlenir). Fotoğraf işlemleri `PhotoStorage` içindedir.

**Tech Stack:** EF Core, MVC, `IFormFile`, xUnit.

**Spec:** `docs/specs/2026-11-24-veteriner-sistem-design.md`

## Global Constraints

- Görünürlük: Admin hepsini; Üye yalnızca kendi hayvanlarını; Veteriner yalnızca kendisinden randevusu olan hayvanları (salt okunur) görür.
- Düzenleme/silme: yalnızca sahibi veya Admin. Ekleme: yalnızca Üye (sahip = giriş yapan kullanıcı; formda sahip alanı yok).
- Hayvan adı aynı sahip için benzersiz (Türkçe harmanlama) — MCP araçları hayvanı adıyla bulacak.
- Doğum tarihi gelecekte olamaz ("bugün" `TimeProvider`'dan); kilo 0,01–500 kg.
- Fotoğraf isteğe bağlı: yalnızca jpg/png, en fazla 2 MB; içerik imzası da kontrol edilir (uzantısı değiştirilmiş dosya reddedilir). `wwwroot/uploads` altına GUID adıyla kaydedilir (git'e girmez). Hayvan silinince ya da fotoğraf değişince eski dosya silinir.
- Randevusu olan hayvan silinemez: "Bu hayvana ait randevular olduğu için silinemez." Aşı kayıtları hayvanla birlikte silinir.

## Review Focus

1. Üye B, Üye A'nın hayvanının Details/Edit/Delete adresine (GET ve POST) gidince 404 alır; veri değişmez.
2. Formdan gizli `OwnerId` gönderilse bile sahip değişmez.
3. `.png` uzantılı ama içi metin olan dosya reddedilir.
4. Hekim, randevusu olmayan hayvanı göremez.

---

### Task 1: Servisler

**Files:**
- Create: `Auth/CurrentUser.cs`, `Infrastructure/SpeciesNames.cs`, `Models/Pets/PetFormModel.cs`, `Infrastructure/FileValidationAttributes.cs`, `Services/PhotoStorage.cs`, `Services/PetService.cs`
- Modify: `Services/ServiceRegistration.cs` (PetService, PhotoStorage, TimeProvider)
- Test: `tests/VetKlinik.Tests/Services/PetServiceTests.cs`, `tests/VetKlinik.Tests/Services/PhotoStorageTests.cs`

**Interfaces:**
- `record CurrentUser(string Id, bool IsAdmin, bool IsVeterinarian)` + `static CurrentUser From(ClaimsPrincipal)`.
- `PhotoStorage(string folder)`: `ServiceResult Validate(IFormFile)`, `Task<string> SaveAsync(IFormFile)`, `void Delete(string?)`.
- `PetService(AppDbContext, PhotoStorage, TimeProvider)`: `ListAsync(user)`, `FindAsync(user, id)` (görünür değilse null), `FindEditableAsync(user, id)`, `CreateAsync(user, form)`, `UpdateAsync(user, id, form)`, `DeleteAsync(user, id)`.

- [ ] **Step 1: Failing tests**
  - `Member_lists_only_own_pets`, `Admin_lists_all_pets`, `Vet_sees_only_pets_with_appointment`
  - `Member_cannot_find_other_members_pet`, `Member_cannot_update_or_delete_other_members_pet`
  - `Create_sets_owner_to_current_user`, `Create_rejects_duplicate_name_for_same_owner` (başka sahipte aynı ad serbest)
  - `Create_rejects_future_birth_date`
  - `Delete_with_appointment_is_refused`, `Delete_removes_vaccines`
  - `PhotoStorageTests`: `Accepts_real_png`, `Rejects_text_renamed_to_png`, `Rejects_file_over_2mb`, `Rejects_gif_extension`
- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3:** Uygula.
- [ ] **Step 4:** `dotnet test` → PASS.
- [ ] **Step 5:** Commit `feat: evcil hayvan servisi ve sahiplik yetkisi`.

### Task 2: Ekranlar

**Files:**
- Create: `Controllers/PetsController.cs`, `Views/Pets/{Index,Details,Create,Edit,Delete,_Form}.cshtml`
- Modify: `Views/Shared/_Layout.cshtml` (menü: Üye "Hayvanlarım", Admin "Hayvanlar", Hekim "Hastalarım"), `wwwroot/js/validation-tr.js` (dosya boyutu/uzantı kuralları), `tests/.../WebFixture.cs` (`MemberClientAsync`)
- Test: `tests/VetKlinik.Tests/Web/PetPagesTests.cs`

- [ ] **Step 1: Failing tests**
  - `Member_creates_pet_and_sees_it`
  - `Other_member_gets_404_for_details_edit_and_delete` (GET ve POST; POST sonrası hayvan hâlâ var)
  - `Anonymous_is_redirected_to_login`
- [ ] **Step 2:** FAIL. **Step 3:** Uygula. **Step 4:** PASS + tarayıcıda: hayvan ekle (fotoğraflı), düzenle, sil, başka üyeyle id değiştir.
- [ ] **Step 5:** Commit `feat: evcil hayvan ekranları`, push.
