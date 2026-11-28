# Adım 8: REST API — Implementation Plan

**Goal:** Uygulamanın temel işlemleri JSON ile de kullanılabilsin: hekim listesi, uygun saatler, aşı takvimi, hizmet raporu ve randevu oluşturma.

**Architecture:** `Api/` klasöründe `[ApiController]` denetleyiciler. İş kuralı yazmazlar; MVC'nin kullandığı servisleri çağırır, sonucu DTO'ya çevirirler. Hiçbir uçta Domain nesnesi (ör. `ApplicationUser`, parola özeti) dışarı verilmez. Kimlik doğrulama MVC ile aynı çerez; `/api` altında giriş yoksa yönlendirme yerine `401`, yetki yoksa `403` döner. `ServiceResult.IsNotFound` → `404`, diğer kural hataları → `400` (ProblemDetails, Türkçe `errors`).

**Spec:** `docs/specs/2026-11-24-veteriner-sistem-design.md` (REST API bölümü)

## Uçlar

| Metot | Yol | Kim | Yanıt |
|---|---|---|---|
| GET | `/api/veterinarians` | giriş yapmış herkes | aktif hekimler, uzmanlıkları ve verdikleri aktif hizmetler |
| GET | `/api/veterinarians/available?serviceId=&date=&veterinarianId=` | giriş yapmış herkes | hizmet bilgisi + hekim başına uygun saatler; hizmet yok/pasif → 404 |
| GET | `/api/pets/{id}/vaccines` | hayvanı görebilen (sahip, randevulu hekim, Admin) | geçmiş + yaklaşan dozlar; başkasının hayvanı → 404 |
| GET | `/api/reports/services?from=&to=` | Admin | hizmet başına randevu sayısı, tamamlanan, gelir (LINQ `GroupBy`, SQL'de hesaplanır) |
| GET | `/api/appointments` | giriş yapmış herkes | görebildiği randevular |
| GET | `/api/appointments/{id}` | görebilen | randevu; değilse 404 |
| POST | `/api/appointments` | hayvanın sahibi / Admin | `201 Created` + `Location`; kural hatası `400`, başkasının hayvanı `404` |

## Kurallar

- Rapor: `StartsAt` tarihi `[from, to]` aralığında (iki uç dahil). Verilmezse içinde bulunulan ay. `from > to` → 400.
  - `AppointmentCount`: iptal/ret dışındaki randevular; `CompletedCount`; `Revenue`: yalnız tamamlananların randevu anındaki ücreti.
- JSON: Türkçe karakterler kaçışsız, enum'lar metin (`"Pending"`), tarih-saatler klinik yerel saati (Kind yok).
- POST gövdesindeki fiyat/süre/durum alanları DTO'da yoktur, yok sayılır.

## Review Focus

1. Başka üyenin hayvanının aşıları / randevusu 404; Admin olmayan rapora 403; girişsiz 401 (yönlendirme değil).
2. Rapor SQL'de gruplanıyor, iptal/ret gelire girmiyor.
3. API'nin kabul ettiği saat ile MVC'nin kabul ettiği saat aynı kural (aynı servis).
4. DTO'larda kişisel veri yok (e-posta, parola özeti, sahibin bilgileri).

---

### Task 1: Rapor servisi
- Create: `Services/ReportService.cs`
- Test: `tests/VetKlinik.Tests/Services/ReportServiceTests.cs`
- Commit `feat: hizmet bazlı randevu ve gelir raporu`

### Task 2: API
- Create: `Api/ApiControllerBase.cs`, `Api/Dtos/*.cs`, `Api/{Veterinarians,Pets,Reports,Appointments}ApiController.cs`
- Modify: `Program.cs` (JSON ayarları), `Auth/IdentitySetup.cs` (`/api` için 401/403)
- Test: `tests/VetKlinik.Tests/Web/ApiTests.cs`
- Commit `feat: REST API ve DTO'lar`, push.
