# MyApp — Kaldırılması ve Durdurulması Korumalı MSI Örneği

Basit bir WPF uygulaması ve onu kuran bir MSI. İki koruma katmanı vardır:

1. **Kaldırma koruması (uninstall):** Kullanıcı uygulamayı kaldırmak istediğinde
   master parola sorulur; yanlışsa kaldırma iptal edilir.
2. **Durdurma koruması (Task Manager):** Birlikte kurulan bir Windows **service**
   (arka plan ajanı) SYSTEM altında çalışır ve Task Manager'dan "End task" ile
   sonlandırılmaya karşı korunur; öldürülürse SCM tarafından otomatik yeniden
   başlatılır.

## Proje yapısı

```
msi-test/
├── MyApp/            → WPF uygulaması (.NET 8, framework-dependent)
├── MyApp.Service/    → Arka plan ajanı / Windows service (.NET 8 Worker)
│                       süreç sonlandırma koruması (DACL) burada
├── UninstallGuard/   → Parola soran WiX custom action (.NET Framework 4.7.2)
└── Installer/        → WiX 5 MSI projesi (her şeyi paketler)
```

## İki koruma katmanı

### 1) Kaldırma (uninstall) — master parola

- Kaldırma sırasında MSI custom action devreye girer ve parola sorar.
- Parola: `Admin123!`
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
- Service başlarken kendi sürecinin DACL'ine, **Interactive** kullanıcılar için
  `PROCESS_TERMINATE` iznini **reddeden** bir ACE ekler
  (`MyApp.Service/ProcessProtection.cs`). Böylece oturum açmış kullanıcı/admin
  Task Manager'dan "End task" yapamaz (Access Denied).
- Yine de öldürülürse, MSI'da tanımlı **SCM recovery** ayarı (util:ServiceConfig)
  service'i 5 sn içinde yeniden başlatır.

> **Önemli — neden Task Manager'da parola SORULMUYOR?**
> Task Manager'ın "End task" işlemi çekirdek düzeyinde `TerminateProcess` çağırır;
> süreç anında ölür ve uygulamanın kodu hiç çalışmaz. Bu yüzden bir uygulamanın
> kendi zorla öldürülüşünü yakalayıp "parola?" diye sorması **mümkün değildir**.
> Parola yalnızca uygulamanın kendi kontrol ettiği yollarda (kaldırma, uygulama
> içi "Çıkış" düğmesi) sorulabilir. Task Manager'a karşı yapılabilecek olan
> "engelleme + yeniden başlatma"dır — bu projedeki yaklaşım da budur.

## Önkoşullar (Windows)

1. **.NET 8 SDK** (derleme için) — https://dotnet.microsoft.com/download
2. **Hedef makinede .NET 8 Desktop Runtime** (çalıştırma için). MSI bunu bir
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
            "https://api.icredible.com/uninstall/validate",
            new System.Net.Http.StringContent(password)).Result;
        return resp.IsSuccessStatusCode;
    }
}
```
