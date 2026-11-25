# Adım 7: Onay, Ret, Tamamlama, İptal ve Hekim Paneli — Implementation Plan

**Goal:** Hekim kendisine gelen randevuyu onaylasın veya gerekçesiyle reddetsin, gerçekleşen randevuyu muayene notuyla tamamlasın; üye randevusunu iptal edebilsin. Hekimin işini toplu gördüğü bir panel olsun.

**Architecture:** Durum geçişleri `AppointmentService` içinde. Her geçiş: kaydı yükle → yetki → mevcut durum uygun mu → zaman kuralı → kaydet. `Appointment.RowVersion` sayesinde aynı anda yapılan iki işlemden ikincisi "randevu bu arada değişti" hatası alır. `ServiceResult`'a "bulunamadı" bilgisi (`IsNotFound`) eklenir: MVC 404'e, REST API (Adım 8) 404'e, diğer hatalar 400'e çevrilir.

**Spec:** `docs/specs/2026-11-24-veteriner-sistem-design.md`

## Durum makinesi

| Geçiş | Kim | Koşul |
|---|---|---|
| Pending → Approved | randevunun hekimi, Admin | randevu henüz başlamamış |
| Pending → Rejected | randevunun hekimi, Admin | gerekçe zorunlu (5–500 karakter) |
| Approved → Completed | randevunun hekimi, Admin | randevu başlamış; muayene notu zorunlu (≤ 2000) |
| Pending/Approved → Cancelled | hayvanın sahibi, Admin | randevu henüz başlamamış |

- Başka geçiş yok; hatalı geçişte Türkçe neden ("Yalnızca onay bekleyen randevular onaylanabilir." vb.).
- Yetkisi olmayan (başka hekim, başka üye) → bulunamadı (404).
- Görme: Admin hepsi; hekim kendi randevuları; üye kendi hayvanlarının randevuları.

## Hekim paneli (`/Hekim`)
- Onay bekleyenler (en yakından), Bugün, Yaklaşan onaylılar, Tamamlanmayı bekleyenler (onaylı ve başlamış).

## Review Focus
1. Başka hekimin randevusu onaylanamaz/reddedilemez/tamamlanamaz (404).
2. Üye başkasının randevusunu iptal edemez; geçmiş randevu iptal edilemez.
3. Reddedilen/iptal edilen randevunun saati tekrar uygun saat listesinde görünür.
4. Aynı anda onay + ret → yalnız biri uygulanır.

---

### Task 1: Servis
- Modify: `Services/ServiceResult.cs` (`IsNotFound`, `NotFound(...)`), `Services/AppointmentService.cs` (`FindAsync`, `ApproveAsync`, `RejectAsync`, `CompleteAsync`, `CancelAsync`, `GetVetDashboardAsync`; `BookAsync`'teki "Hayvan bulunamadı" → NotFound)
- Test: `tests/VetKlinik.Tests/Services/AppointmentWorkflowTests.cs`
- Commit `feat: randevu onay, ret, tamamlama ve iptal kuralları`

### Task 2: Ekranlar
- Create: `Views/Appointments/Details.cshtml`, `Controllers/VetPanelController.cs` (`/Hekim`), `Views/VetPanel/Index.cshtml`, `Views/Appointments/_Row.cshtml`
- Modify: `Controllers/AppointmentsController.cs` (Details, Approve, Reject, Complete, Cancel — POST), `Views/Appointments/Index.cshtml`, `_Layout.cshtml` (hekim için "Hekim Paneli")
- Test: `tests/VetKlinik.Tests/Web/AppointmentWorkflowPagesTests.cs`
- Commit `feat: hekim paneli ve randevu işlemleri ekranları`, push.
