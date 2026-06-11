# MyApp — Kaldırılması ve Durdurulması Korumalı MSI Örneği

Basit bir WPF uygulaması ve onu kuran bir MSI. Dört koruma katmanı vardır:

1. **Kaldırma koruması (uninstall):** Kullanıcı uygulamayı kaldırmak istediğinde
   master parola sorulur; yanlışsa kaldırma iptal edilir.
2. **Durdurma koruması (Task Manager + services.msc):** Birlikte kurulan bir
   Windows **service** (arka plan ajanı) SYSTEM altında çalışır. Task Manager'dan
   "End task" ile sonlandırılmaya karşı korunur (öldürülürse SCM otomatik yeniden
   başlatır) ve `services.msc` / `sc stop` ile durdurulmaya karşı korunur.
3. **Uygulamadan kontrollü durdurma:** Masaüstü uygulaması, **master parola**
   (uninstall ile aynı) doğrulayarak servisi düzgünce durdurabilir; ayrıca tekrar
   başlatabilir (başlatma parola istemez).
4. **Klasör silme koruması:** `C:\Program Files\MyApp` ve `C:\ProgramData\MyApp`
   klasörleri, oturum açmış kullanıcılar (admin dahil) tarafından silinmeye karşı
   korunur (Dosya Gezgini "Sil" / `del` / `rmdir` → Erişim reddedildi).

> **Nasıl çalışıyor? (özet)** Arka plan servisimiz Windows'un en yetkili hesabı olan
> **SYSTEM** ile çalışır. Servis, korumak istediği şeylerin (kendi süreci, servisi,
> klasörleri) izin listesine *"oturum açmış kullanıcılar bunu yapamaz"* kuralı
> (ACL/DACL) ekler. Bu kural **normal kullanıcıyı da admin'i de** kapsar; SYSTEM ise
> kuralın dışında kalır, böylece kendi işini (ör. kaldırma sırasında durdurma)
> yapmaya devam eder. Engelleme parolayla değil, **Windows'un kendi izin sistemiyle**
> olur — parola yalnızca uygulamanın kontrol ettiği iki yerde devreye girer:
> kaldırma ve uygulama içinden durdurma. Sınırlar için aşağıdaki güvenlik notuna bak.

## Proje yapısı

```
msi-test/
├── MyApp/            → WPF uygulaması (.NET 9, framework-dependent)
│                       servisi durdur/başlat + master parola (ServiceControlClient.cs)
├── MyApp.Service/    → Arka plan ajanı / Windows service (.NET 9 Worker)
│                       process kill koruması (ProcessProtection.cs) +
│                       services.msc stop koruması (ServiceProtection.cs) +
│                       klasör silme koruması (FolderProtection.cs)
├── UninstallGuard/   → Parola soran WiX custom action (.NET Framework 4.7.2)
└── Installer/        → WiX 5 MSI projesi (her şeyi paketler)
```

## Koruma katmanları

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
  yazar — içine parolanın **SHA-256 hash'ini** koyar. SYSTEM altındaki servis bu
  dosyayı görünce **hash'i kendi gömülü hash'iyle yeniden doğrular**; eşleşirse kendi
  STOP reddini kaldırır, dosyayı siler ve kendini temiz şekilde durdurur (eşleşmezse
  isteği reddedip çalışmaya devam eder). Böylece dosyanın yalnızca varlığına
  güvenilmez; parolayı bilmeyen biri elle dosya atarak servisi durduramaz. Temiz
  durdurma SCM recovery'yi tetiklemez, yani servis kendiliğinden geri gelmez.
- **"Start service"** butonu servisi `ServiceController` ile başlatır ve **parola
  istemez**. Bunun çalışması için servis SDDL'ine Interactive kullanıcılar için bir
  `SERVICE_START` (SDDL `RP`) **izin** ACE'si de eklenir (`(A;;RPLCRC;;;IU)`); aksi
  halde varsayılan servis güvenliği normal kullanıcıya START vermez ve
  "Cannot open service" hatası alınır. STOP yine reddedilir, sadece START açılır.
  Servis yeniden başladığında korumaları tekrar uygular.
- Parola doğrulaması ileride bir API'ye taşınacak şekilde tek bir metotta tutulur
  (`ValidatePassword`).

### 4) Klasör silme koruması — folder DACL

- Service başlarken (SYSTEM altında), iki klasörün izin listesine **Interactive**
  kullanıcılar için `DELETE` + `DELETE_CHILD` reddeden bir ACE ekler (`icacls`,
  `MyApp.Service/FolderProtection.cs`):
  - `C:\Program Files\MyApp` (uygulama dosyaları) — yol exe konumundan türetilir,
    hard-code değildir.
  - `C:\ProgramData\MyApp` (veriler/loglar).
- Böylece kullanıcı/admin bu klasörleri Dosya Gezgini "Sil" / `del` / `rmdir` ile
  silmeye çalışınca **Erişim reddedildi** alır. SYSTEM etkilenmez.
- **Kaldırmayı bozmaz:** ACE yalnızca Interactive kullanıcıyı hedefler; MSI'ın
  `RemoveFiles` aksiyonu SYSTEM olarak çalıştığı için klasörü silebilir. Ayrıca
  uninstall'da `--unprotect` bakım modu bu ACE'leri `StopServices`/`RemoveFiles`
  öncesi **kaldırır** (dosyaları SİLMEDEN). Sonuç: kaldırmada `Program Files`
  klasörü silinir, `ProgramData` klasörü ve logları **diskte kalır**.

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

### Gerçek makinede doğrulanmış davranışlar

Aşağıdaki senaryolar Windows üzerinde (v1.2.1 kurulumuyla) elle test edilip
doğrulanmıştır. Her satır, beklenen sonucu ve onu kanıtlayan gözlemi gösterir.

| Senaryo | Beklenen | Gözlenen kanıt |
|---|---|---|
| **Task Manager → End task** (normal kullanıcı) | Erişim reddedildi | Süreç sonlandırılamıyor (process DACL). |
| **Task Manager → End task** (elevated/admin) | Parola **çıkmaz**; süreç ölür ama ~5 sn'de geri gelir | Parola ekranı yok (kernel `TerminateProcess`, kod çalışmaz); `sc query` ~5 sn sonra tekrar `RUNNING` (SCM recovery). |
| **services.msc / `sc stop`** | Durdurma yetkisi yok | "Erişim reddedildi" (SERVICE_STOP deny ACE). |
| **Uygulamadan "Stop service" + doğru parola** | Servis düzgünce durur | `agent.log`: `Stop requested by the desktop app (master password re-verified by the service).`; `sc query` → STOPPED; kendiliğinden geri gelmez. |
| **Elle sahte `stop.request`** (parolasız bypass denemesi) | Reddedilir; servis çalışmaya devam eder | `agent.log`: `Stop request rejected: invalid or missing password hash.`; hemen ardından aynı PID ile `Agent running …`; `sc query` → hâlâ `RUNNING`; dosya tüketilip silinmiş. |
| **`C:\Program Files\MyApp` / `C:\ProgramData\MyApp` silme** | Erişim reddedildi | Dosya Gezgini "Sil" / `rmdir` → Access Denied (deny-DELETE ACE). |

> **Parolasız bypass testi nasıl yapılır** (servis çalışırken, yönetici gerekmez):
> ```powershell
> "sahte" | Out-File -Encoding ascii C:\ProgramData\MyApp\stop.request
> Start-Sleep -Seconds 5
> sc.exe query MyAppAgent                                   # hâlâ RUNNING olmalı
> Get-Content C:\ProgramData\MyApp\agent.log -Tail 6        # "Stop request rejected ..." satırı
> ```
> (PowerShell'de `sc` bazen bir alias'tır; `sc.exe` kullanın. cmd'de düz `sc query` çalışır.)

## Güvenlik notu (önemli — sınırlar)

- **Kaldırma koruması** normal kullanıcıyı (Denetim Masası / çift tık /
  `msiexec /x`) durdurur. Sessiz mod (`/qn`) kaldırma, parola sorulamayacağı için
  tasarım gereği **engellenir**.
- **Durdurma ve klasör korumaları** birer **caydırıcıdır**, kernel düzeyi değildir:
  - Normal kullanıcının Task Manager'dan öldürmesini, servisi durdurmasını ve
    klasörleri silmesini engeller.
  - Kararlı bir **admin** DACL'i geri değiştirip, recovery'yi kapatıp ya da klasörün
    sahipliğini (ownership) ele geçirip yine de sonlandırabilir/silebilir.
- Gerçek, kurcalanamaz koruma yalnızca Microsoft imzalı **PPL** (Protected Process
  Light) veya bir **kernel sürücüsü** ile mümkündür — yüksek karmaşıklık/risk.
- Üretimde parola doğrulamasını bir sunucu **API**'sine taşıyın ve hassas mantığı
  sunucuda tutun.

### Best practice mi? Değerlendirme

- ✅ **Doğru olan:** ACL ile koruma, parolayı tek metotta toplamak, uninstall'da
  kilidi otomatik açmak ve sınırları (caydırıcı, mutlak değil) dürüstçe belgelemek —
  bunlar temiz ve doğru desenlerdir.
- ⚠️ **Konumlandırma:** Bu mimari bir **"tamper protection" (kurcalama koruması)**
  örneğidir. AV/EDR ürünleri benzerini yapar ama **kernel sürücüsü + imzalı binary**
  ile. Yalnızca user-mode ACL ile yapıldığında bu, sektörde *"best-effort deterrent"*
  sayılır, kesin bir *"güvenlik kontrolü"* değil. Yani teknik meşrudur; yeter ki
  "mutlak koruma" diye değil, **caydırıcı** diye sunulsun (bu repo öyle yapıyor).

### Least Privilege (en az yetki) açısından

Servis **LocalSystem (SYSTEM)** ile çalışır — makinedeki en yüksek yetki. Least
Privilege ilkesi açısından bu **en dikkat edilmesi gereken noktadır:**

- **Neden SYSTEM gerekli?** Korumaların 2 ve 4'ü (servis/klasör kilitleri) bir
  nesneyi **admin'e karşı bile** kilitler. Bir nesneyi sahibine/admin'e karşı
  kilitlemek, onu değiştiren tarafın admin'in üzerinde olmasını gerektirir — pratikte
  **SYSTEM** demektir. `LocalService`/`NetworkService` gibi düşük hesaplar bu DACL
  değişikliklerini yapamaz. Yani korumanın gücü doğrudan SYSTEM'den gelir; yetkiyi
  düşürürseniz koruma da zayıflar.
- **Risk:** SYSTEM neredeyse her zaman "fazla yetki"dir. Servis bir açık barındırırsa
  (ör. `stop.request` dosyasının işlenişi veya ileride eklenecek bir API çağrısı),
  saldırgan doğrudan **SYSTEM yetkisi** kazanır (privilege escalation). Bu, güvenlik
  denetimlerinde kırmızı bayraktır.
- **`stop.request` IPC'si — çift doğrulama (sertleştirildi):** Durdurma parolası
  hem uygulamada hem **serviste** doğrulanır. Uygulama, parolayı doğruladıktan sonra
  kontrol dosyasına parolanın **SHA-256 hash'ini** yazar; SYSTEM servisi de bu hash'i
  kendi gömülü hash'iyle karşılaştırır ve eşleşmezse durdurmayı **reddeder** (`AgentWorker`).
  Böylece `ProgramData` yazılabilir olsa bile, dosyayı elle oluşturan ama parolayı
  bilmeyen bir kullanıcı servisi durduramaz — yalnızca dosyanın varlığına güvenmeyiz.
  (Üretimde hash karşılaştırması yerine bir API çağrısı konabilir; her iki taraf da
  tek bir doğrulama noktası kullanır.)

### SYSTEM yerine alternatifler

| Yaklaşım | Açıklama | Bu proje için |
|---|---|---|
| **Yetkiyi düşürmek** (LocalService vb.) | Servis-DACL ve `Program Files` kilitlerini admin'e karşı kuramaz. | ❌ Korumalar 2/4'ü çalışmaz hale getirir. |
| **SYSTEM'de kal + attack surface'i küçült** | Servis kodunu minimumda tut, `stop.request` IPC'sini sıkılaştır, riskli işleri (ağ/parsing) ayrı düşük-yetkili sürece taşı. | ✅ **Önerilen, gerçekçi best practice.** |
| **Yetki ayrıştırması (privilege separation)** | Küçük denetlenmiş bir SYSTEM "helper" yalnız ACL kilitler/açar; asıl iş mantığı LocalService ile çalışır. EDR/AV mimarisi. | ➖ İdeal ama bu demo için aşırı. |
| **Kernel sürücüsü (minifilter / ObCallbacks)** | Mutlak, kurcalanamaz koruma. | ➖ İmzalama/bakım/risk maliyeti çok yüksek; gereksiz. |

**Özet:** SYSTEM'den kaçış bu senaryoda yok; doğru yol "SYSTEM'i hak etmek" —
yani servisi küçük tutmak ve `stop.request` kanalını güvenli hale getirmek.

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
