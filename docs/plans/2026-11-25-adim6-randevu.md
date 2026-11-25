# Adım 6: Randevu Alma — Çakışma Kontrolü ve Uygun Saatler — Implementation Plan

**Goal:** Üye, hayvanı için hizmet + gün seçip hekimlerin uygun saatlerini görsün ve randevu alsın. Randevu `Pending` olarak oluşur; çakışan, mesai dışı veya geçmiş saat reddedilir.

**Architecture:** Tüm kural `AppointmentService` içinde. Bir saatin uygun olup olmadığını tek bir saf fonksiyon (`SlotProblem`) söyler; hem uygun saat listesi hem de randevu kaydı bu fonksiyonu kullanır, böylece "listede görünen saat" ile "kabul edilen saat" hiçbir zaman ayrışmaz. Kayıt, `Serializable` transaction içinde çakışma kontrolü + ekleme olarak yapılır; aynı anda iki istek aynı saati alamaz. REST API (Adım 8) ve MCP (Adım 9) aynı metotları çağıracak.

**Spec:** `docs/specs/2026-11-24-veteriner-sistem-design.md` (Randevu kuralları bölümü)

## Global Constraints

- Saatler klinik yerel saati (Europe/Istanbul), `DateTime` (Kind yok). "Şimdi": `ClinicTime.Now(TimeProvider)`.
- Bir saat uygundur ⇔ hepsi:
  1. Hekim aktif ve bu hizmeti veriyor; hizmet aktif.
  2. `[başlangıç, başlangıç + süre)` hekimin o günkü müsaitliği ∩ kliniğin o günkü saatleri içinde.
  3. Başlangıç şimdiden sonra ve en fazla 60 gün sonra.
  4. Başlangıç dakikası 15'in katı (saniye 0).
  5. Hekimin `Pending`/`Approved` bir randevusuyla çakışmaz: `a.Start < b.End && b.Start < a.End`.
  6. (Yalnız kayıtta) aynı hayvanın da o aralıkta `Pending`/`Approved` randevusu yoktur.
- Randevu: `Status = Pending`, `EndsAt = StartsAt + süre`, süre ve ücret hizmetten kopyalanır, `CreatedAt = şimdi`.
- Randevu alan, hayvanı düzenleyebilen kişidir (sahibi veya Admin); değilse "Hayvan bulunamadı." (404).
- Eşzamanlı çakışma: Serializable transaction; SQL Server deadlock (1205) veya benzeri durumda Türkçe "Bu saat az önce doldu" hatası.
- Hata mesajları Türkçe ve nedenini söyler (MCP asistanı kullanıcıya iletecek).

## Review Focus

1. Uygun saat listesinde görünen her saat kayıtta kabul edilir; görünmeyen reddedilir (aynı fonksiyon).
2. 90 dk'lık hizmet, 30 dk sonra başlayan randevuyla çakışır (süre dikkate alınır).
3. Reddedilmiş/iptal edilmiş randevu saati boşaltır.
4. İki eşzamanlı kayıttan yalnız biri başarılı.
5. Başka üyenin hayvanı için randevu alınamaz; formdaki fiyat/süre/durum alanları yok sayılır.

---

### Task 1: AppointmentService
- Create: `Models/Appointments/BookingFormModel.cs`, `Services/AppointmentService.cs`
- Modify: `Services/ServiceRegistration.cs`
- Test: `tests/VetKlinik.Tests/Services/AppointmentServiceTests.cs` (`FakeTimeProvider`)
- Interfaces:
  - `record VetSlots(int VeterinarianId, string FullName, string Title, IReadOnlyList<DateTime> Slots)`
  - `GetAvailableSlotsAsync(int serviceId, DateOnly date, int? veterinarianId = null) → List<VetSlots>` (yalnız uygun saati olan hekimler)
  - `BookAsync(CurrentUser, BookingFormModel) → ServiceResult<Appointment>`
  - `ListAsync(CurrentUser) → List<Appointment>` (Üye: kendi hayvanları; Hekim: kendisininkiler; Admin: hepsi)
- Commit `feat: randevu servisi, çakışma kontrolü ve uygun saatler`

### Task 2: Ekranlar
- Create: `Controllers/AppointmentsController.cs` (`Index` = Randevularım, `Book` GET/POST), `Views/Appointments/{Index,Book}.cshtml`, `Infrastructure/AppointmentStatusNames.cs`
- Modify: `_Layout.cshtml` (Randevularım / Randevu Al), `Views/Home/Index.cshtml` (Randevu Al düğmesi)
- Test: `tests/VetKlinik.Tests/Web/AppointmentPagesTests.cs`
- Commit `feat: randevu alma ekranları`, push.
