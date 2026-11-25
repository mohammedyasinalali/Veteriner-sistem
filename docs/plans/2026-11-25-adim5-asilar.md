# Adım 5: Aşı Kayıtları — Implementation Plan

**Goal:** Hayvanın aşı geçmişi ve yaklaşan/geciken aşıları görülsün; sahibi ve (randevusu olan) hekim aşı kaydı ekleyebilsin.

**Architecture:** `VaccineService` hayvana erişimi `PetService.FindAsync` ile kontrol eder (yetki mantığı tekrar yazılmaz). Aşı takvimi (`GetScheduleAsync`) servis içinde hesaplanır; aynı metodu ileride REST API (`/api/pets/{id}/vaccines`) ve MCP (`asi_takvimi`) kullanacak. "Bugün" `TimeProvider`'dan gelir.

**Spec:** `docs/specs/2026-11-24-veteriner-sistem-design.md`

## Global Constraints

- Görme: hayvanı görebilen herkes (Adım 4 kuralları).
- Ekleme: hayvanı görebilen herkes — sahibi, Admin, hayvanla randevusu olan hekim. Hekim eklerse kayda `VeterinarianId` yazılır.
- Düzenleme/silme: Admin; kaydı giren hekim; hekim girmemişse hayvanın sahibi. (Sahip, hekimin girdiği kaydı değiştiremez.)
- Uygulama tarihi gelecekte olamaz; sonraki doz tarihi uygulama tarihinden sonra olmalı.
- Takvim: geçmiş = tüm kayıtlar (yeniden eskiye). Yaklaşan = her aşı adı için en son kaydın `NextDueOn` değeri; bugünden önceyse "Gecikmiş".
- Yetkisiz erişim → 404.

## Review Focus

1. Üye B, Üye A'nın hayvanına aşı ekleyemez / kaydını düzenleyemez (404, veri değişmez).
2. Hekimin girdiği kaydı sahip değiştiremez.
3. Aynı aşının yeni dozu girilince eski kaydın sonraki tarihi "yaklaşan" listesinden düşer.
4. `petId` formda gizli alan olarak taşınırken başka bir hayvanın id'siyle değiştirilirse kayıt o hayvana geçmez.

---

### Task 1: Servis
- Create: `Models/Pets/VaccineFormModel.cs`, `Services/VaccineService.cs`
- Test: `tests/VetKlinik.Tests/Services/VaccineServiceTests.cs` (`FakeTimeProvider`)
- Tests: `Owner_adds_vaccine`, `Other_member_cannot_add_or_edit`, `Vet_with_appointment_adds_vaccine_with_vet_id`, `Vet_without_appointment_cannot_add`, `Owner_cannot_edit_vet_record`, `Future_administered_date_rejected`, `Next_due_must_be_after_administered`, `Schedule_uses_latest_dose_and_marks_overdue`, `Update_cannot_move_record_to_other_pet`
- Commit `feat: aşı kayıtları servisi ve aşı takvimi`

### Task 2: Ekranlar
- Create: `Controllers/VaccinesController.cs`, `Views/Vaccines/{Create,Edit,Delete,_Form}.cshtml`, `Views/Pets/_Vaccines.cshtml`
- Modify: `Views/Pets/Details.cshtml` (aşı geçmişi + yaklaşan aşılar), `Controllers/PetsController.cs`
- Test: `tests/VetKlinik.Tests/Web/VaccinePagesTests.cs` (`Owner_adds_vaccine_and_sees_it_on_pet_page`, `Other_member_gets_404`)
- Commit `feat: aşı ekranları`, push.
