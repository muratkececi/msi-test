# Elle Test Rehberi — MyApp Parola Korumalı Uninstall

Bu döküman, MSI'ın kurulumunu ve özellikle **parola korumalı kaldırma**
davranışını Windows üzerinde elle doğrulamak içindir. Parola ekranı bir
UI etkileşimi olduğundan bu senaryolar otomatize edilmemiştir.

> **Ortam:** Windows 10/11. Test için tek kullan-at bir VM önerilir
> (sanal makine snapshot'ı alıp her testten sonra geri dönmek en temizi).
> **Master parola:** `Admin123!`

---

## 0. Hazırlık

- [ ] MSI'yi edin: ya `Installer\bin\Release\MyAppSetup.msi` (yerel build)
      ya da GitHub Actions çalışmasındaki **Artifacts > MyAppSetup-msi**.
- [ ] PowerShell'i **Yönetici olarak** aç (perMachine kurulum yönetici ister).
- [ ] (Önerilir) MSI loglarını görmek için her komuta `/l*v gunluk.log` ekle.

---

## 1. Kurulum çalışıyor mu?

```powershell
msiexec /i MyAppSetup.msi /l*v install.log
```

- [ ] Kurulum sihirbazı açılıyor, klasör seçtirip tamamlanıyor.
- [ ] `C:\Program Files\MyApp\MyApp.exe` oluşmuş.
- [ ] Başlat menüsünde **MyApp** kısayolu var.
- [ ] Kısayola tıklayınca "Merhaba 👋" penceresi açılıyor.

---

## 2. ANA SENARYO — Yanlış parola kaldırmayı engeller

Denetim Masası üzerinden:

1. **Ayarlar > Uygulamalar** (veya `appwiz.cpl`) > **MyApp** > Kaldır.
- [ ] Parola soran bir pencere açılıyor.
2. **Yanlış** bir parola gir (örn. `yanlis`) → Onayla.
- [ ] "Hatalı parola, tekrar deneyin (2/3)" mesajı çıkıyor.
3. Üst üste 3 kez yanlış gir.
- [ ] "Çok fazla hatalı deneme, kaldırma iptal edildi" uyarısı çıkıyor.
- [ ] **Uygulama KALDIRILMADI** — `MyApp.exe` hâlâ yerinde, kısayol duruyor.

---

## 3. ANA SENARYO — Doğru parola kaldırmaya izin verir

1. Tekrar Kaldır'a bas, parola ekranında `Admin123!` gir → Onayla.
- [ ] Kaldırma normal şekilde devam ediyor.
- [ ] `C:\Program Files\MyApp\` klasörü silinmiş.
- [ ] Başlat menüsü kısayolu kaybolmuş.

---

## 4. İptal davranışı

1. Yeniden kur (Bölüm 1), Kaldır'a bas, parola ekranında **İptal**'e tıkla.
- [ ] Kaldırma durdu, uygulama yerinde kaldı.

---

## 5. Komut satırından kaldırma (msiexec /x)

```powershell
msiexec /x MyAppSetup.msi /l*v uninstall.log
```

- [ ] Parola ekranı yine çıkıyor (Denetim Masası ile aynı davranış).
- [ ] Doğru parola → kaldırılıyor; yanlış/iptal → kalıyor.

---

## 6. Sessiz mod kaldırma engelleniyor mu? (güvenlik kontrolü)

Tasarım gereği UI'sız (`/qn`) kaldırma **engellenir** — çünkü parola sorulamaz.

```powershell
msiexec /x MyAppSetup.msi /qn /l*v silent.log
```

- [ ] Kaldırma **başarısız** oluyor (exit code 0 değil), uygulama yerinde kalıyor.
- [ ] `silent.log` içinde `UninstallGuard: Sessiz mod algılandı. Uninstall engellendi.`
      satırı görünüyor.

> Exit code'u görmek için: `echo $LASTEXITCODE` (PowerShell).
> Başarılı kaldırma 0, engellenen kaldırma 0'dan farklı bir değer döndürür.

---

## 7. Upgrade parola sormamalı (regresyon kontrolü)

Custom action koşulu kaldırmayı `REMOVE="ALL"` ile sınırlar; bir sürüm
yükseltmesi sırasında parola **sorulmamalıdır**.

1. Sürümü artır: `MyApp.csproj` ve `Package.wxs` içindeki `1.0.0` → `1.1.0`,
   yeniden build et.
2. Üstüne kur: `msiexec /i MyAppSetup-1.1.0.msi /l*v upgrade.log`
- [ ] Kurulum sırasında **parola sorulmadı**.
- [ ] Uygulama 1.1.0'a güncellendi.

---

## Sorun giderme

| Belirti | Olası neden |
|---|---|
| Parola ekranı hiç çıkmıyor | Custom action MSI'a paketlenmemiş; `Package.wxs`'teki `Binary SourceFile` yolunu (`UninstallGuard.CA.dll`) ve build sırasını kontrol et. |
| "1603" hatası | Yönetici olarak çalıştırmadın ya da custom action exception attı; `/l*v` logunda `UninstallGuard:` satırlarına bak. |
| Doğru parola da reddediliyor | `CustomActions.cs`'teki hash, kullandığın paroladan üretilmemiş. Hash'i README'deki PowerShell ile yeniden üret. |
| Sessiz kaldırma yine de siliyor | `UILevel` kontrolü beklediğin gibi davranmıyor olabilir; logdaki `UILevel` değerine bak. |
```
