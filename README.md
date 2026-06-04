# MyApp — Kaldırılması ve Durdurulması Korumalı MSI Örneği

Basit bir WPF uygulaması ve onu kuran bir MSI. İki koruma katmanı vardır:

1. **Kaldırma koruması (uninstall):** Kullanıcı uygulamayı kaldırmak istediğinde
   master parola sorulur; yanlışsa kaldırma iptal edilir.
2. **Durdurma koruması (Task Manager + services.msc):** Birlikte kurulan bir
   Windows **service** (arka plan ajanı) SYSTEM altında çalışır. Task Manager'dan
   "End task" ile sonlandırılmaya karşı korunur (öldürülürse SCM otomatik yeniden
   başlatır) ve `services.msc` / `sc stop` ile durdurulmaya karşı korunur.
3. **Uygulamadan kontrollü durdurma:** Masaüstü uygulaması, **master parola**
   (uninstall ile aynı) doğrulayarak servisi düzgünce durdurabilir; ayrıca tekrar
   başlatabilir (başlatma parola istemez).

## Proje yapısı

```
msi-test/
├── MyApp/            → WPF uygulaması (.NET 9, framework-dependent)
│                       servisi durdur/başlat + master parola (ServiceControlClient.cs)
├── MyApp.Service/    → Arka plan ajanı / Windows service (.NET 9 Worker)
│                       process kill koruması (ProcessProtection.cs) +
│                       services.msc stop koruması (ServiceProtection.cs)
├── UninstallGuard/   → Parola soran WiX custom action (.NET Framework 4.7.2)
└── Installer/        → WiX 5 MSI projesi (her şeyi paketler)
```

## İki koruma katmanı

### 1) Kaldırma (uninstall) — master parola

- Kaldırma sırasında MSI custom action devreye girer ve parola sorar.
- **Demo parolası:** `Admin123!` — bu yalnızca örnek amaçlıdır ve repo public
  olduğu için herkese açıktır. **Üretimde mutlaka değiştirin** (aşağıdaki adımla
  kendi parolanızın hash'ini üretip gömün) veya doğrulamayı bir API'ye taşıyın.
- Kodda parolanın kendisi DEĞİL, SHA-256 hash'i gömülüdür
  (`UninstallGuard/CustomActions.cs` içindeki `MasterPasswordHash`).
- Yeni hash üretmek için (PowerShell):

  ```powershell
  $p = "YeniParola"
  [BitConverter]::ToString(
    [System.Security.Cryptography.SHA256]::Create().ComputeHash(
      [Text.Encoding]::UTF8.GetBytes($p))
  ).Replace("-", "")
  ```

### 2) Durdurma (Task Manager) — service + DACL + SCM recovery

- `MyAppAgent` adlı bir Windows service kurulur (LocalSystem, otomatik başlatma).
- **Task Manager koruması:** Service başlarken kendi sürecinin DACL'ine,
  **Interactive** kullanıcılar için `PROCESS_TERMINATE` iznini **reddeden** bir ACE
  ekler (`MyApp.Service/ProcessProtection.cs`). Böylece oturum açmış kullanıcı/admin
  Task Manager'dan "End task" yapamaz (Access Denied).
- **services.msc / `sc stop` koruması:** Service ayrıca kendi **servis** güvenlik
  tanımlayıcısına, Interactive kullanıcılar için `SERVICE_STOP` (SDDL'de `WP`)
  iznini reddeden bir ACE ekler (`MyApp.Service/ServiceProtection.cs`). Böylece
  normal/yönetici kullanıcı `services.msc`'den "Durdur" diyemez. SYSTEM ve SCM
  etkilenmez; düzgün durdurma ve recovery çalışmaya devam eder.
- Yine de **zorla öldürülürse** (Task Manager End task / `taskkill /F`), MSI'da
  tanımlı **SCM recovery** ayarı (util:ServiceConfig) service'i 5 sn içinde
  yeniden başlatır. (Not: `services.msc`'den **bilinçli** "Durdur" recovery'yi
  tetiklemez — Windows bunu hata saymaz; bu yüzden yukarıdaki STOP reddi eklendi.)

> **STOP korumasını elle kaldırmak istersen** (yönetici olarak):
> En kolayı MSI'ı **yeniden kurmak/kaldırmaktır** — kaldırma sırasında koruma
> otomatik temizlenir, yeniden kurulumda servis güvenliği sıfırlanır.
> Servisi silmeden, sadece reddi kaldırmak istersen:
> ```powershell
> sc sdshow MyAppAgent            # mevcut SDDL'i göster
> # Çıktıdaki "(D;;WP;;;IU)" parçasını çıkarıp kalan SDDL ile:
> sc sdset MyAppAgent "<(D;;WP;;;IU) parçası çıkarılmış SDDL>"
> ```
> Ya da servis exe'sinin bakım modunu kullan (SYSTEM/yönetici gerektirir):
> servis `--unprotect` argümanıyla çalıştırıldığında yalnızca bu reddi kaldırıp
> çıkar (kaldırma sırasında MSI da bunu çağırır).
- Service kurulum sırasında MSI'ın `StartServices` aksiyonuyla **başlatılmaz**;
  bunun yerine kurulum bittikten sonra sessiz bir custom action (`sc start`)
  service'i başlatır. Nedeni: MSI service başlatma başarısız olursa "Service failed
  to start … Retry/Ignore" diyaloğunu her zaman gösterir ve bunu bastırmanın MSI
  yolu yoktur. Custom action `Return="ignore"` ile başarısızlığı yok sayar; service
  zaten `Start="auto"` olduğu için en geç bir sonraki açılışta da çalışır.

### 3) Uygulamadan kontrollü durdurma / başlatma — master parola

- WPF uygulamasındaki **"Stop service"** butonu master parola sorar (uninstall ile
  aynı; `MyApp/ServiceControlClient.cs` içindeki SHA-256 hash). Doğruysa servisi
  düzgünce durdurur.
- Servis `SERVICE_STOP` reddi uyguladığı için uygulama doğrudan `sc stop` yapamaz.
  Bunun yerine `C:\ProgramData\MyApp\stop.request` adlı bir **kontrol dosyası**
  yazar; SYSTEM altındaki servis bu dosyayı görünce kendi STOP reddini kaldırır,
  dosyayı siler ve kendini temiz şekilde durdurur. Temiz durdurma SCM recovery'yi
  tetiklemez, yani servis kendiliğinden geri gelmez.
- **"Start service"** butonu servisi `ServiceController` ile başlatır ve **parola
  istemez**. Bunun çalışması için servis SDDL'ine Interactive kullanıcılar için bir
  `SERVICE_START` (SDDL `RP`) **izin** ACE'si de eklenir (`(A;;RPLCRC;;;IU)`); aksi
  halde varsayılan servis güvenliği normal kullanıcıya START vermez ve
  "Cannot open service" hatası alınır. STOP yine reddedilir, sadece START açılır.
  Servis yeniden başladığında korumaları tekrar uygular.
- Parola doğrulaması ileride bir API'ye taşınacak şekilde tek bir metotta tutulur
  (`ValidatePassword`).

> **Service neden düz `net9.0` hedefler (`net9.0-windows` değil)?**
> Service yalnızca P/Invoke ile Windows API'leri çağırır (WPF/WinForms kullanmaz).
> `net9.0-windows` TFM'i `runtimeconfig.json`'a `Microsoft.WindowsDesktop.App`
> bağımlılığı yazar; SCM service'i başlatırken bu Desktop runtime aranır,
> bulunamazsa süreç **managed kod hiç çalışmadan çöker** (service başlatılamaz —
> kurulumda "Starting services" sonrası retry/rollback olarak görülür). Düz
> `net9.0` yalnızca `Microsoft.NETCore.App` gerektirir; bu her .NET 9 kurulumunda
> bulunur. (WPF uygulaması ise WPF için `net9.0-windows` + Desktop runtime'a
> ihtiyaç duyar; MSI'daki LaunchCondition bunu kontrol eder.)

> **Önemli — neden Task Manager'da parola SORULMUYOR?**
> Task Manager'ın "End task" işlemi çekirdek düzeyinde `TerminateProcess` çağırır;
> süreç anında ölür ve uygulamanın kodu hiç çalışmaz. Bu yüzden bir uygulamanın
> kendi zorla öldürülüşünü yakalayıp "parola?" diye sorması **mümkün değildir**.
> Parola yalnızca uygulamanın kendi kontrol ettiği yollarda (kaldırma, uygulama
> içi "Çıkış" düğmesi) sorulabilir. Task Manager'a karşı yapılabilecek olan
> "engelleme + yeniden başlatma"dır — bu projedeki yaklaşım da budur.

## Önkoşullar (Windows)

1. **.NET 9 SDK** (derleme için) — https://dotnet.microsoft.com/download
2. **Hedef makinede .NET 9 Desktop Runtime** (çalıştırma için). MSI bunu bir
   `LaunchCondition` ile kontrol eder; kurulu değilse kurulumu durdurup
   kullanıcıyı indirme bağlantısına yönlendirir.
3. WiX araçları NuGet'ten `WixToolset.Sdk` ile gelir; ayrıca global kurulum şart
   değildir. İsterseniz:
   ```powershell
   dotnet tool install --global wix --version 5.0.2
   ```

## Derleme (Windows)

Exe'ler **publish** klasöründen paketlendiği için MyApp ve MyApp.Service ayrıca
`dotnet publish` edilmelidir (CI'da olduğu gibi):

```powershell
# Kök dizinde
dotnet publish MyApp\MyApp.csproj -c Release
dotnet publish MyApp.Service\MyApp.Service.csproj -c Release
dotnet build   UninstallGuard\UninstallGuard.csproj -c Release
dotnet build   Installer\Installer.wixproj -c Release
```

MSI çıktısı:
```
Installer\bin\Release\MyAppSetup.msi
```

> Pratikte derlemeyi **GitHub Actions** yapar (bkz. `.github/workflows/build-msi.yml`).
> `windows-latest` runner kullanılır; kendi Windows makineni vermene gerek yoktur.
> `v*` etiketli bir push'ta MSI ayrıca bir GitHub Release'e eklenir.

## Hızlı test

Kurulum sihirbazının son sayfasında **"Launch MyApp"** seçeneği (varsayılan
işaretli) bulunur; "Finish" deyince uygulama kullanıcı bağlamında otomatik açılır.

```powershell
# Kurulum (çift tık da olur) — service de kurulur ve başlar
msiexec /i Installer\bin\Release\MyAppSetup.msi

# Kaldırma — burada PAROLA EKRANI çıkar:
msiexec /x Installer\bin\Release\MyAppSetup.msi
# veya Denetim Masası > Programlar ve Özellikler > MyApp > Kaldır
```

Service'in çalıştığını ve korunduğunu görmek için:

```powershell
sc query MyAppAgent                 # RUNNING olmalı
# Task Manager > Details > MyApp.Service.exe > End task → "Access Denied"
type C:\ProgramData\MyApp\agent.log # heartbeat ve koruma logları
```

Ayrıntılı senaryolar için **[TESTING.md](TESTING.md)**.

## Güvenlik notu (önemli — sınırlar)

- **Kaldırma koruması** normal kullanıcıyı (Denetim Masası / çift tık /
  `msiexec /x`) durdurur. Sessiz mod (`/qn`) kaldırma, parola sorulamayacağı için
  tasarım gereği **engellenir**.
- **Durdurma koruması** bir **caydırıcıdır**, kernel düzeyi değildir:
  - Normal kullanıcının Task Manager'dan öldürmesini engeller.
  - Kararlı bir **admin** DACL'i geri değiştirip ya da recovery'yi kapatıp yine de
    sonlandırabilir; dosyaları elle silmek de mümkündür.
- Gerçek, kurcalanamaz koruma yalnızca Microsoft imzalı **PPL** (Protected Process
  Light) veya bir **kernel sürücüsü** ile mümkündür — yüksek karmaşıklık/risk.
- Üretimde parola doğrulamasını bir sunucu **API**'sine taşıyın ve hassas mantığı
  sunucuda tutun.

## Üretim için: API ile doğrulama

`UninstallGuard/CustomActions.cs` içindeki yerel hash karşılaştırması yerine bir
HTTP çağrısı koyabilirsiniz:

```csharp
private static bool Verify(string password)
{
    using (var client = new System.Net.Http.HttpClient())
    {
        var resp = client.PostAsync(
            "https://api.example.com/uninstall/validate",
            new System.Net.Http.StringContent(password)).Result;
        return resp.IsSuccessStatusCode;
    }
}
```
