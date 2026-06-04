# Adım 1 — Kaldırma (uninstall) master parola koruması

Kaldırma sırasında master parola soran, yanlış parolada kaldırmayı iptal eden bir
katman ekler. WiX MSI varsayar.

## Prompt

```text
Bu projenin MSI'ına bir KALDIRMA PAROLASI koruması eklemeni istiyorum.

ÖNCE KEŞFET (varsayım yapma):
- Installer WiX mi (.wixproj / *.wxs / WixToolset.Sdk)? Değilse, nasıl uyarlanacağını
  bana özetle, kod yazmadan önce onay al.
- Mevcut WiX sürümünü, hedef TFM'leri ve klasör düzenini çıkar.

EKLE:
- Uninstall sırasında master parola soran bir WiX DTF managed custom action ekle.
  Ayrı bir proje olsun ve .NET Framework 4.7.2 hedeflesin — DTF custom action'ları
  bunu gerektirir (modern .NET ile çalışmaz).
- Parolanın kendisini DEĞİL, SHA-256 hash'ini (UPPERCASE hex) kodda göm. Doğrulama
  girilen parolanın hash'ini bu sabitle karşılaştırsın.
- Parola WinForms ile basit bir pencerede sorulsun. Pencereyi AYRI bir STA thread'inde
  aç (MSI custom action thread'i STA olmayabilir; aksi halde pencere görünmeden çöker).
  Pencere TopMost + Activate + BringToFront ile en önde açılsın (MSI'ın arkasında kalmasın).
- En fazla 3 deneme; 3 yanlışta kaldırmayı iptal et.
- Custom action Return="check" olsun; yanlış parola/iptal/çökme durumunda
  ActionResult.Failure dönerek TÜM uninstall'ı iptal etsin.
- Sequence: InstallExecuteSequence'te InstallValidate'ten ÖNCE çalışsın (dosyalar
  silinmeden sorsun). Koşulu kaldırmayla sınırla:
  Installed AND REMOVE="ALL" AND NOT UPGRADINGPRODUCTCODE AND NOT WIX_UPGRADE_DETECTED
  → böylece sürüm yükseltmesinde parola SORULMAZ (regresyon).
- Sessiz mod (/qn) tespiti: UILevel == "2" (INSTALLUILEVEL_NONE) ise parola sorulamaz;
  güvenlik gereği bu durumda uninstall'ı ENGELLE (Failure dön).
- Güvenlik gereği: parola kontrolü herhangi bir nedenle çökerse de uninstall'ı ENGELLE.

WiX PAKETLEME DİKKAT:
- Custom action DLL'i yanına bir CustomAction.config koy (useLegacyV2RuntimeActivation
  Policy) — yoksa CLR yüklenmez ve action 1603 / 0x80131700 ile patlar.

KURALLAR:
- Kod yorumları ve commit'ler İngilizce; Conventional Commits; Co-Authored-By EKLEME.
- Commit/push/tag işlemini ben onaylamadan yapma.
- WiX macOS'ta derlenmez; CI (windows-latest) yeşil olmadan doğrulanmış sayma.
- Parola kontrolünü üretimde bir sunucu API'sine taşımayı README'de öner.
```

## Referans dosyalar (bu projede)

- `UninstallGuard/CustomActions.cs` — parola kontrolü + sequence mantığı
- `UninstallGuard/PasswordPrompt.cs` — WinForms parola penceresi
- `UninstallGuard/CustomAction.config` — `useLegacyV2RuntimeActivationPolicy`
- `Installer/Package.wxs` — `CheckUninstallPassword` CA tanımı ve sequence

## Neden bu detaylar?

| Detay | Neden |
| --- | --- |
| .NET Framework 4.7.2 | WiX DTF custom action'ları modern .NET'te çalışmaz |
| `CustomAction.config` | Olmadan CLR yüklenmez (1603 / 0x80131700) |
| STA thread | MSI CA thread'i STA değilse WinForms penceresi çöker |
| `REMOVE="ALL"` koşulu | Upgrade sırasında parola sorulmasını önler |
| `/qn` engelleme | UI yoksa parola sorulamaz → güvenlik açığını kapatır |
