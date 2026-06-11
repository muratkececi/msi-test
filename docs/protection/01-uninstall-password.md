# Adim 1 - Kaldirma (uninstall) master parola korumasi

Kaldirma sirasinda master parola soran, yanlis parolada kaldirmayi iptal eden bir
katman ekler. WiX MSI varsayar.

## Prompt

```text
Bu projenin MSI'ina bir KALDIRMA PAROLASI korumasi eklemeni istiyorum.

ONCE KESFET (varsayim yapma - repo'yu tara, sonra uygula):
- Installer WiX mi (.wixproj / *.wxs / WixToolset.Sdk)? DEGILSE (MSIX, Inno Setup,
  Squirrel, ClickOnce, NSIS, ya da hic installer yok), bu prompt OLDUGU GIBI
  uygulanamaz: bana mevcut installer tipini ve bu korumanin oraya nasil uyarlanacagini
  (veya uyarlanamiyorsa nedenini) ozetle, kod yazmadan ONAY AL.
- Mevcut WiX surumunu, installer'in bitness'ini (x64/x86 -> klasor ve CA binary adlari
  degisir), hedef TFM'leri ve klasor duzenini cikar.
- Bu projede zaten bir parola/lisans hash'i sabiti var mi diye ara. Varsa, Adim 5
  (uygulamadan durdurma) ile AYNI hash kullanilmali - uninstall ve app-stop parolalari
  ayrismasin. Yoksa yeni bir SHA-256 uret ve nerede tanimlandigini bana bildir.

EKLE:
- Uninstall sirasinda master parola soran bir WiX DTF managed custom action ekle.
  Ayri bir proje olsun ve .NET Framework 4.7.2 hedeflesin - DTF custom action'lari
  bunu gerektirir (modern .NET ile calismaz).
- Parolanin kendisini DEGIL, SHA-256 hash'ini (UPPERCASE hex) kodda gom. Dogrulama
  girilen parolanin hash'ini bu sabitle karsilastirsin.
- Parola WinForms ile basit bir pencerede sorulsun. Pencereyi AYRI bir STA thread'inde
  ac (MSI custom action thread'i STA olmayabilir; aksi halde pencere gorunmeden coker).
  Pencere TopMost + Activate + BringToFront ile en onde acilsin (MSI'in arkasinda kalmasin).
- En fazla 3 deneme; 3 yanlista kaldirmayi iptal et.
- Custom action Return="check" olsun; yanlis parola/iptal/cokme durumunda
  ActionResult.Failure donerek TUM uninstall'i iptal etsin.
- Sequence: InstallExecuteSequence'te InstallValidate'ten ONCE calissin (dosyalar
  silinmeden sorsun). Kosulu kaldirmayla sinirla:
  Installed AND REMOVE="ALL" AND NOT UPGRADINGPRODUCTCODE AND NOT WIX_UPGRADE_DETECTED
  -> boylece surum yukseltmesinde parola SORULMAZ (regresyon).
- Sessiz mod (/qn) tespiti: UILevel == "2" (INSTALLUILEVEL_NONE) ise parola sorulamaz;
  guvenlik geregi bu durumda uninstall'i ENGELLE (Failure don).
- Guvenlik geregi: parola kontrolu herhangi bir nedenle cokerse de uninstall'i ENGELLE.

WiX PAKETLEME DIKKAT:
- Custom action DLL'i yanina bir CustomAction.config koy (useLegacyV2RuntimeActivation
  Policy) - yoksa CLR yuklenmez ve action 1603 / 0x80131700 ile patlar.

KURALLAR:
- Kod yorumlari ve commit'ler Ingilizce; Conventional Commits; Co-Authored-By EKLEME.
- Commit/push/tag islemini ben onaylamadan yapma.
- WiX macOS'ta derlenmez; CI (windows-latest) yesil olmadan dogrulanmis sayma.
- Parola kontrolunu uretimde bir sunucu API'sine tasimayi README'de oner.
```

## Referans dosyalar (bu projede)

- `UninstallGuard/CustomActions.cs` - parola kontrolu + sequence mantigi
- `UninstallGuard/PasswordPrompt.cs` - WinForms parola penceresi
- `UninstallGuard/CustomAction.config` - `useLegacyV2RuntimeActivationPolicy`
- `Installer/Package.wxs` - `CheckUninstallPassword` CA tanimi ve sequence

## Neden bu detaylar?

| Detay | Neden |
| --- | --- |
| .NET Framework 4.7.2 | WiX DTF custom action'lari modern .NET'te calismaz |
| `CustomAction.config` | Olmadan CLR yuklenmez (1603 / 0x80131700) |
| STA thread | MSI CA thread'i STA degilse WinForms penceresi coker |
| `REMOVE="ALL"` kosulu | Upgrade sirasinda parola sorulmasini onler |
| `/qn` engelleme | UI yoksa parola sorulamaz -> guvenlik acigini kapatir |
