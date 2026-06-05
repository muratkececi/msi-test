# Adım 2 — Task Manager "End task" engelleme (process DACL)

Arka plan service'inin Task Manager > Details > End task ile öldürülmesini
engeller. Bir background Windows service gerektirir; yoksa bu adım onu da ekler.

## Prompt

```text
Bu projeye, arka plan service'inin Task Manager "End task" ile öldürülmesini
engelleyen bir koruma eklemeni istiyorum.

ÖNCE KEŞFET (varsayım yapma — repo'yu tara, sonra uygula):
- Installer WiX mi (.wixproj / *.wxs)? DEĞİLSE, SCM recovery ve MSI service kurulumu
  adımları aynen geçmez; bana installer tipini söyle, service kurulumu/recovery'yi
  oraya nasıl uyarlayacağını özetleyip ONAY AL. Process DACL koruması (kod tarafı)
  installer'dan bağımsız çalışır.
- Installer'ın bitness'ini çıkar (x64 → ProgramFiles64Folder, Wix4...._X64; x86 →
  _X86). Aşağıdaki örnek adlar x64 içindir; x86 ise düzelt.
- Projede zaten bir Windows service / arka plan süreci var mı? Varsa onu kullan;
  yoksa LocalSystem altında çalışan, otomatik başlayan bir Worker service ekle.
- Service projesinin TFM'ini kontrol et. DÜZ `net9.0` olmalı, `net9.0-windows` DEĞİL:
  -windows TFM'i Microsoft.WindowsDesktop.App bağımlılığı ekler ve Desktop runtime
  yoksa service SCM başlangıcında managed kod çalışmadan çöker. DİKKAT: mevcut bir
  service'i kullanıyorsan ve TFM'i `-windows` ise, düz `net9.0`'a çevirmek WPF/WinForms
  referanslarını KIRABİLİR — önce o bağımlılıkları kontrol et, kıracaksa bana bildir.

EKLE — PROCESS DACL KORUMASI:
- Service başlarken kendi PROCESS DACL'ine, Interactive grup (S-1-5-4) için
  PROCESS_TERMINATE (0x0001) iznini REDDEDEN bir ACE ekle (P/Invoke:
  GetSecurityInfo → SetEntriesInAcl → SetSecurityInfo, SE_KERNEL_OBJECT +
  DACL_SECURITY_INFORMATION). Mevcut DACL'i oku, deny ACE'yi EKLE (var olan
  izinleri silme).
- Sonuç: oturum açmış kullanıcı/admin Task Manager'dan "End task" yapınca
  "Access Denied" alır. SYSTEM interactive olmadığı için etkilenmez; SCM service'i
  yine yönetir, temiz durdurma (SERVICE_CONTROL_STOP) PROCESS_TERMINATE gerektirmez.
- Bu kodu yalnızca Windows'ta çalıştır: OperatingSystem.IsWindows() ile sarmalı,
  sınıfa [SupportedOSPlatform("windows")] koy (CA1416 uyarısını önler).

SCM RECOVERY (öldürülürse geri gelsin):
- MSI'da service'e recovery ekle (WiX util:ServiceConfig): ilk/ikinci/sonraki
  hatada 5 sn sonra restart, sayaç 1 günde sıfırlanır. Böylece Task Manager kill /
  taskkill /F sonrası service ~5 sn'de geri gelir.
- NOT: services.msc'den BİLİNÇLİ "Durdur" recovery'yi tetiklemez (Windows bunu hata
  saymaz). O senaryo için Adım 3'teki service-SDDL koruması gerekir.

MSI SERVICE KURULUMU:
- Service'i Start="auto" ile kur ama MSI'ın StartServices'iyle BAŞLATMA
  (Start="install" KOYMA): başlamazsa bastırılamayan "Service failed to start /
  Retry/Ignore" diyaloğu çıkar (Wait="no" bunu engellemez). Bunun yerine kurulum
  sonrası deferred + Impersonate="no" (SYSTEM) bir custom action ile `sc start` yap,
  Return="ignore". Start="auto" bir sonraki açılışta fallback olur.

MSI PAKETLEME DİKKAT:
- Framework-dependent yayında dosyaları TEK TEK listeleme; <Files Include="...\**" />
  ile tüm publish klasörünü harvest et (açık Id'li bir ComponentGroup içinde,
  Feature'dan referansla). Yoksa NuGet DLL'leri eksik kalır, service
  FileNotFoundException ile çöker. App ve service publish'lerini AYRI alt klasörlere
  koy (aynı adlı DLL çakışmasın).

KURALLAR:
- Kod yorumları ve commit'ler İngilizce; Conventional Commits; Co-Authored-By EKLEME.
- Commit/push/tag işlemini ben onaylamadan yapma.
- WiX macOS'ta derlenmez; CI (windows-latest) yeşil olmadan doğrulanmış sayma.
- Bu DETERRENT bir korumadır; kararlı bir admin DACL'i geri alabilir. README'de belirt.
```

## Referans dosyalar (bu projede)

- `MyApp.Service/ProcessProtection.cs` — process DACL deny-terminate (P/Invoke)
- `MyApp.Service/AgentWorker.cs` — başlangıçta korumayı uygulayan worker
- `MyApp.Service/MyApp.Service.csproj` — düz `net9.0` TFM
- `Installer/Package.wxs` — ServiceInstall + util:ServiceConfig recovery + `sc start` CA

## Neden bu detaylar?

| Detay | Neden |
| --- | --- |
| Düz `net9.0` | `-windows` TFM'i Desktop runtime bağımlılığı ekler; service SCM'de çöker |
| Mevcut DACL'i oku, ekle | Var olan izinleri silmeden sadece deny ACE eklemek için |
| Interactive (S-1-5-4) hedefle | SYSTEM/SCM'yi etkilemeden sadece kullanıcıyı engellemek için |
| `Start="install"` yok | MSI service başlatma başarısızlığı bastırılamayan diyalog çıkarır |
| `<Files>` harvest | Elle dosya saymak NuGet DLL'lerini atlar → FileNotFoundException |
