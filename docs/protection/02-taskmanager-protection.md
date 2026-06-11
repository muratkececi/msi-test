# Adim 2 - Task Manager "End task" engelleme (process DACL)

Arka plan service'inin Task Manager > Details > End task ile oldurulmesini
engeller. Bir background Windows service gerektirir; yoksa bu adim onu da ekler.

## Prompt

```text
Bu projeye, arka plan service'inin Task Manager "End task" ile oldurulmesini
engelleyen bir koruma eklemeni istiyorum.

ONCE KESFET (varsayim yapma - repo'yu tara, sonra uygula):
- Installer WiX mi (.wixproj / *.wxs)? DEGILSE, SCM recovery ve MSI service kurulumu
  adimlari aynen gecmez; bana installer tipini soyle, service kurulumu/recovery'yi
  oraya nasil uyarlayacagini ozetleyip ONAY AL. Process DACL korumasi (kod tarafi)
  installer'dan bagimsiz calisir.
- Installer'in bitness'ini cikar (x64 -> ProgramFiles64Folder, Wix4...._X64; x86 ->
  _X86). Asagidaki ornek adlar x64 icindir; x86 ise duzelt.
- Projede zaten bir Windows service / arka plan sureci var mi? Varsa onu kullan;
  yoksa LocalSystem altinda calisan, otomatik baslayan bir Worker service ekle.
- Service projesinin TFM'ini kontrol et. DUZ `net9.0` olmali, `net9.0-windows` DEGIL:
  -windows TFM'i Microsoft.WindowsDesktop.App bagimliligi ekler ve Desktop runtime
  yoksa service SCM baslangicinda managed kod calismadan coker. DIKKAT: mevcut bir
  service'i kullaniyorsan ve TFM'i `-windows` ise, duz `net9.0`'a cevirmek WPF/WinForms
  referanslarini KIRABILIR - once o bagimliliklari kontrol et, kiracaksa bana bildir.

EKLE - PROCESS DACL KORUMASI:
- Service baslarken kendi PROCESS DACL'ine, Interactive grup (S-1-5-4) icin
  PROCESS_TERMINATE (0x0001) iznini REDDEDEN bir ACE ekle (P/Invoke:
  GetSecurityInfo -> SetEntriesInAcl -> SetSecurityInfo, SE_KERNEL_OBJECT +
  DACL_SECURITY_INFORMATION). Mevcut DACL'i oku, deny ACE'yi EKLE (var olan
  izinleri silme).
- Sonuc: oturum acmis kullanici/admin Task Manager'dan "End task" yapinca
  "Access Denied" alir. SYSTEM interactive olmadigi icin etkilenmez; SCM service'i
  yine yonetir, temiz durdurma (SERVICE_CONTROL_STOP) PROCESS_TERMINATE gerektirmez.
- Bu kodu yalnizca Windows'ta calistir: OperatingSystem.IsWindows() ile sarmali,
  sinifa [SupportedOSPlatform("windows")] koy (CA1416 uyarisini onler).

SCM RECOVERY (oldurulurse geri gelsin):
- MSI'da service'e recovery ekle (WiX util:ServiceConfig): ilk/ikinci/sonraki
  hatada 5 sn sonra restart, sayac 1 gunde sifirlanir. Boylece Task Manager kill /
  taskkill /F sonrasi service ~5 sn'de geri gelir.
- NOT: services.msc'den BILINCLI "Durdur" recovery'yi tetiklemez (Windows bunu hata
  saymaz). O senaryo icin Adim 3'teki service-SDDL korumasi gerekir.

MSI SERVICE KURULUMU:
- Service'i Start="auto" ile kur ama MSI'in StartServices'iyle BASLATMA
  (Start="install" KOYMA): baslamazsa bastirilamayan "Service failed to start /
  Retry/Ignore" diyalogu cikar (Wait="no" bunu engellemez). Bunun yerine kurulum
  sonrasi deferred + Impersonate="no" (SYSTEM) bir custom action ile `sc start` yap,
  Return="ignore". Start="auto" bir sonraki acilista fallback olur.

MSI PAKETLEME DIKKAT:
- Framework-dependent yayinda dosyalari TEK TEK listeleme; <Files Include="...\**" />
  ile tum publish klasorunu harvest et (acik Id'li bir ComponentGroup icinde,
  Feature'dan referansla). Yoksa NuGet DLL'leri eksik kalir, service
  FileNotFoundException ile coker. App ve service publish'lerini AYRI alt klasorlere
  koy (ayni adli DLL cakismasin).

KURALLAR:
- Kod yorumlari ve commit'ler Ingilizce; Conventional Commits; Co-Authored-By EKLEME.
- Commit/push/tag islemini ben onaylamadan yapma.
- WiX macOS'ta derlenmez; CI (windows-latest) yesil olmadan dogrulanmis sayma.
- Bu DETERRENT bir korumadir; kararli bir admin DACL'i geri alabilir. README'de belirt.
```

## Referans dosyalar (bu projede)

- `MyApp.Service/ProcessProtection.cs` - process DACL deny-terminate (P/Invoke)
- `MyApp.Service/AgentWorker.cs` - baslangicta korumayi uygulayan worker
- `MyApp.Service/MyApp.Service.csproj` - duz `net9.0` TFM
- `Installer/Package.wxs` - ServiceInstall + util:ServiceConfig recovery + `sc start` CA

## Neden bu detaylar?

| Detay | Neden |
| --- | --- |
| Duz `net9.0` | `-windows` TFM'i Desktop runtime bagimliligi ekler; service SCM'de coker |
| Mevcut DACL'i oku, ekle | Var olan izinleri silmeden sadece deny ACE eklemek icin |
| Interactive (S-1-5-4) hedefle | SYSTEM/SCM'yi etkilemeden sadece kullaniciyi engellemek icin |
| `Start="install"` yok | MSI service baslatma basarisizligi bastirilamayan diyalog cikarir |
| `<Files>` harvest | Elle dosya saymak NuGet DLL'lerini atlar -> FileNotFoundException |
