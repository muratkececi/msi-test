# Elle Test Rehberi — MyApp

Bu döküman MSI'ın kurulumunu ve iki koruma katmanını Windows üzerinde elle
doğrulamak içindir:

1. **Kaldırma koruması** — kaldırırken master parola.
2. **Durdurma koruması** — service'in Task Manager'dan öldürülememesi + otomatik
   yeniden başlama.

Her ikisi de UI/sistem etkileşimi gerektirdiğinden otomatize edilmemiştir.

> **Ortam:** Windows 10/11. Test için tek kullan-at bir VM önerilir
> (snapshot alıp her testten sonra geri dönmek en temizi).
> **Demo master parolası:** `Admin123!` (yalnızca örnek; üretimde değiştirin)

---

## 0. Hazırlık

- [ ] MSI'yi edin: ya `Installer\bin\Release\MyAppSetup.msi` (yerel build)
      ya da GitHub Actions çalışmasındaki **Artifacts > MyAppSetup-msi**
      (veya `v*` etiketli Release).
- [ ] PowerShell'i **Yönetici olarak** aç (perMachine kurulum + service yönetici ister).
- [ ] Hedef makinede **.NET 9 Desktop Runtime** kurulu olsun (yoksa kurulum
      bilinçli olarak durur — bkz. Bölüm 8).
- [ ] (Önerilir) MSI loglarını görmek için her komuta `/l*v gunluk.log` ekle.

---

## 1. Kurulum çalışıyor mu?

```powershell
msiexec /i MyAppSetup.msi /l*v install.log
```

- [ ] Kurulum sihirbazı açılıyor, klasör seçtirip tamamlanıyor.
- [ ] `C:\Program Files\MyApp\` içinde `MyApp.exe`, `MyApp.dll`,
      `MyApp.runtimeconfig.json`, `MyApp.deps.json` (+ yan dosyaları) var; servis ise
      `C:\Program Files\MyApp\Agent\MyApp.Service.exe` (+ yan dosyaları) altında.
- [ ] Son sayfada **"Launch MyApp"** seçeneği işaretli; "Finish" deyince uygulama
      otomatik açılıyor (yönetici/UAC istemeden, normal kullanıcı penceresi olarak).
- [ ] Başlat menüsünde **MyApp** kısayolu var.
- [ ] Kısayola tıklayınca "Hello 👋" penceresi açılıyor.

---

## 2. Service kuruldu ve çalışıyor mu?

```powershell
sc query MyAppAgent
```

- [ ] Service var ve `STATE: RUNNING`.
- [ ] `C:\ProgramData\MyApp\agent.log` oluşmuş; içinde
      `Service starting.` ve
      `Process termination protection applied (PROCESS_TERMINATE denied).`
      satırları var ve ~30 sn'de bir heartbeat (`Agent running — PID ...`) ekleniyor.

---

## 3. DURDURMA KORUMASI — Task Manager'dan öldürülemiyor

1. **Task Manager > Details (Ayrıntılar)** > `MyApp.Service.exe` > sağ tık > **End task**.
- [ ] **"Access is denied / Erişim reddedildi"** hatası çıkıyor; süreç ölmüyor.

> Neden? Service başlarken kendi DACL'inden Interactive kullanıcılar için
> `PROCESS_TERMINATE` iznini kaldırır. SYSTEM etkilenmediği için SCM service'i
> hâlâ düzgün yönetebilir.

---

## 3b. DURDURMA KORUMASI — services.msc / `sc stop` ile durdurulamıyor

1. **services.msc** > **MyApp Agent** > sağ tık > **Durdur** (veya yönetici
   PowerShell'de `sc stop MyAppAgent`).
- [ ] **"Erişim reddedildi / Access is denied"** alıyorsun; service duruyor değil.
- [ ] `agent.log`'ta `Service stop protection applied (SERVICE_STOP denied).`
      satırı var.

> Neden? Service, kendi servis güvenlik tanımlayıcısına Interactive kullanıcılar
> için `SERVICE_STOP` (SDDL `WP`) reddi ekler. SYSTEM/SCM etkilenmez.
> Korumayı elle kaldırmak için README'deki "STOP korumasını elle kaldırmak" adımları.

---

## 4. DURDURMA KORUMASI — Öldürülse bile geri geliyor (SCM recovery)

DACL'i baypas edebilen bir admin (örn. `taskkill /F` SYSTEM yetkisiyle) için bile
service geri gelmelidir.

```powershell
# Yönetici PowerShell'de PID'i bul ve zorla öldürmeyi dene:
taskkill /F /IM MyApp.Service.exe
# Birkaç saniye bekle, sonra:
sc query MyAppAgent
```

- [ ] Öldürme başarılı olsa bile ~5 sn içinde service tekrar `RUNNING` oluyor
      (SCM recovery: FailureAction=restart).
- [ ] `agent.log`'a yeni bir `Service starting.` satırı ekleniyor.

---

## 4b. UYGULAMADAN DURDURMA / BAŞLATMA — master parola

Başlat menüsünden **MyApp**'i aç.

1. **"Stop service"** butonuna bas.
- [ ] Master parola soran bir pencere çıkıyor.
2. **Yanlış** parola gir.
- [ ] "Wrong password." uyarısı çıkıyor; service çalışmaya devam ediyor
      (`sc query MyAppAgent` → RUNNING).
3. Tekrar "Stop service" → `Admin123!` gir.
- [ ] "Service stopped." mesajı çıkıyor; `sc query MyAppAgent` → STOPPED.
- [ ] Service KENDİLİĞİNDEN geri gelmiyor (temiz durdurma recovery tetiklemez).
- [ ] `agent.log`'ta `Stop requested by the desktop app ...` satırı var.
- [ ] `C:\ProgramData\MyApp\stop.request` dosyası işlendikten sonra silinmiş.
4. **"Start service"** butonuna bas.
- [ ] Parola SORULMUYOR; "Service started." çıkıyor; `sc query MyAppAgent` → RUNNING.
- [ ] Service yeniden korumalı: services.msc'den durdurmayı dene → Erişim reddedildi
      (Bölüm 3b yeniden geçerli).

> Neden start parola istemiyor? STOP reddi (SDDL `WP`) yalnızca durdurmayı engeller;
> START (`RP`) Interactive kullanıcıya açıktır.

---

## 5. KALDIRMA — Yanlış parola kaldırmayı engeller

Denetim Masası üzerinden:

1. **Ayarlar > Uygulamalar** (veya `appwiz.cpl`) > **MyApp** > Kaldır.
- [ ] Parola soran bir pencere açılıyor.
2. **Yanlış** bir parola gir (örn. `yanlis`) → Onayla.
- [ ] "Hatalı parola, tekrar deneyin" mesajı çıkıyor.
3. Üst üste 3 kez yanlış gir.
- [ ] "Çok fazla hatalı deneme, kaldırma iptal edildi" uyarısı çıkıyor.
- [ ] **Uygulama KALDIRILMADI** — dosyalar ve service yerinde.

---

## 6. KALDIRMA — Doğru parola kaldırmaya izin verir

1. Tekrar Kaldır'a bas, parola ekranında `Admin123!` gir → Onayla.
- [ ] Kaldırma normal şekilde devam ediyor.
- [ ] `C:\Program Files\MyApp\` klasörü silinmiş.
- [ ] `sc query MyAppAgent` → service artık **yok** (kaldırma sırasında durdurulup silindi).
- [ ] Başlat menüsü kısayolu kaybolmuş.

---

## 7. KALDIRMA — İptal davranışı

1. Yeniden kur (Bölüm 1), Kaldır'a bas, parola ekranında **İptal**'e tıkla.
- [ ] Kaldırma durdu, uygulama ve service yerinde kaldı.

---

## 8. .NET Runtime kontrolü (LaunchCondition)

Framework-dependent olduğumuz için hedef makinede .NET 9 Desktop Runtime yoksa
kurulum bilinçli olarak durmalıdır.

1. Runtime kurulu olmayan temiz bir makinede/VM'de kur.
- [ ] Kurulum, .NET 9 Desktop Runtime indirme bağlantısını içeren bir mesajla
      durduruluyor (dosya kopyalanmadan önce).

---

## 9. Sessiz mod kaldırma engelleniyor mu? (güvenlik kontrolü)

Tasarım gereği UI'sız (`/qn`) kaldırma **engellenir** — çünkü parola sorulamaz.

```powershell
msiexec /x MyAppSetup.msi /qn /l*v silent.log
```

- [ ] Kaldırma **başarısız** oluyor (exit code 0 değil), uygulama yerinde kalıyor.
- [ ] `silent.log` içinde sessiz modun algılanıp kaldırmanın engellendiğine dair
      `UninstallGuard:` satırı görünüyor.

> Exit code: `echo $LASTEXITCODE` (PowerShell). Başarılı kaldırma 0, engellenen
> kaldırma 0'dan farklı bir değer döndürür.

---

## 10. Upgrade parola sormamalı (regresyon kontrolü)

Custom action koşulu kaldırmayı `REMOVE="ALL"` ile sınırlar; sürüm yükseltmesi
sırasında parola **sorulmamalıdır**.

1. Sürümü artır: `MyApp/MyApp.csproj`, `MyApp.Service/MyApp.Service.csproj` ve
   `Installer/Package.wxs` içindeki sürümü (örn. `1.0.4` → `1.1.0`), yeniden build et.
2. Üstüne kur: `msiexec /i MyAppSetup.msi /l*v upgrade.log`
- [ ] Kurulum sırasında **parola sorulmadı**.
- [ ] Uygulama yeni sürüme güncellendi; service çalışmaya devam ediyor.

---

## Sorun giderme

| Belirti | Olası neden |
|---|---|
| Parola ekranı hiç çıkmıyor | Custom action MSI'a paketlenmemiş; `Package.wxs`'teki `Binary SourceFile` yolunu (`UninstallGuard.CA.dll`) ve build sırasını kontrol et. |
| Uninstall'da uygulama açılıp kapanıyor, parola yok, "1603" | Custom action CLR'ı yükleyemiyor (`SFXCA ... 0x80131700`); `UninstallGuard/CustomAction.config` paketlenmiş mi bak. `/l*v` logundaki `SFXCA:` satırlarını incele. |
| Doğru parola da reddediliyor | `CustomActions.cs`'teki hash, kullandığın paroladan üretilmemiş. Hash'i README'deki PowerShell ile yeniden üret. |
| Service `RUNNING` olmuyor | .NET 9 runtime eksik olabilir; `agent.log` ve Olay Görüntüleyici'ye bak. |
| Task Manager yine de öldürebiliyor | SYSTEM yetkili/admin bir bağlamdan deniyorsun; bu beklenen sınırdır (caydırıcı koruma). SCM recovery (Bölüm 4) yine de geri getirmeli. |
| Klasörde sadece `MyApp.dll` var | .NET 9 exe+dll+runtimeconfig+deps gerektirir; MSI'ın tüm publish çıktısını kurduğundan emin ol. |
