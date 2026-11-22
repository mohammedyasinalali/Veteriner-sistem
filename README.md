# Veteriner Kliniği Randevu ve Hasta Takip Sistemi

| | |
|---|---|
| **Öğrenci numarası** | g231210566 |
| **Adı soyadı** | MOHAMAD YASEEN ALALI |
| **Ders grubu** | 2.A |
| **GitHub** | https://github.com/mohammedyasinalali/Veteriner-sistem |

## Proje hakkında

2026-2027 Güz dönemi Web Programlama dersi proje ödevidir.

Uygulama; bir veteriner kliniğinin hizmetlerini, hekimlerini, evcil hayvanların muayene ve aşı geçmişini ve
hayvan sahiplerinin randevularını yönetir. Aynı iş kuralları MVC sayfaları, REST API ve bir MCP (Model Context
Protocol) sunucusu üzerinden sunulacaktır. Böylece hayvan sahibi bir yapay zekâ asistanına "Pamuk'un bir sonraki
aşısı ne zaman?" diye sorduğunda cevap doğrudan bu uygulamadan alınabilecektir.

## Teknolojiler

- ASP.NET Core MVC (.NET 10), C#
- SQL Server 2022, Entity Framework Core 10 (Code First, Migration), LINQ
- ASP.NET Core Identity
- ModelContextProtocol.AspNetCore
- Bootstrap 5, jQuery

## Kurulum

Gereksinimler: .NET 10 SDK ve Docker.

1. `.env.example` dosyasını `.env` olarak kopyalayın ve güçlü bir `MSSQL_SA_PASSWORD` yazın.
2. SQL Server'ı başlatın: `docker compose up -d`
3. Bağlantı dizesini user-secrets'a ekleyin (`<PAROLA>` yerine `.env` dosyasındaki değer):

   ```bash
   dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,14333;Database=VetKlinik;User Id=sa;Password=<PAROLA>;TrustServerCertificate=True" --project src/VetKlinik.Web
   ```

4. Testler: `dotnet test`
5. Uygulama: `dotnet run --project src/VetKlinik.Web`

Parolalar depoda tutulmaz; `.env` dosyası `.gitignore` içindedir.
