# Adım 4 — Kurulumdaki ham `[1] [2] [9] [6]` metnini temizleme

Kurulum/kaldırma sırasında ilerleme çubuğunda görünen ham yer-tutucu metinleri
temizler, örn:

```text
Status:  Copying new files   File: [1], Directory: [9], Size: [6]
```

Tamamen kozmetik bir sorundur ama profesyonel görünüm için giderilir.

## Kök neden (msi-test'te kanıtlandı)

Bu metin **kendi custom action'larından değil, STANDART MSI aksiyonlarından**
gelir. Windows Installer'ın `InstallFiles`, `RemoveFiles`, `WriteRegistryValues`
gibi standart aksiyonları için **yerleşik (built-in) bir ActionText şablonu**
vardır ve bu şablon `[1] [9] [6]` gibi token'lar içerir (dosya adı, dizin, boyut).
Senin MSI'ında o aksiyon için bir ActionText satırı YOKSA, Windows Installer kendi
token'lı varsayılanını gösterir. Çözüm: o aksiyonlara token'sız ActionText satırı
eklemek.

> En sık atlanan ve en görünür olanı **`InstallFiles`**'tır ("Copying new files"),
> çünkü dosya kopyalama kurulumun en uzun ve en görünür adımıdır.

## Prompt

```text
Kurulum/kaldırma sırasında ilerleme çubuğunda görünen ham "[1]", "File: [1],
Directory: [9], Size: [6]" gibi yer-tutucu metinleri temizlemeni istiyorum
(kozmetik). WiX MSI varsayıyorum.

ÖNCE TEŞHİS ET (tahmin etme — MSI'ın İÇİNE bak):
- MSI'ı derle. macOS'ta: `brew install msitools`. Sonra:
    msiinfo export <msi> InstallExecuteSequence   # çalışan TÜM aksiyonlar + sıra
    msiinfo export <msi> ActionText               # ActionText satırı OLAN aksiyonlar
  (Windows'ta Orca ile aynı tablolara bak.)
- İki listeyi karşılaştır: InstallExecuteSequence'te çalışan ama ActionText'te
  satırı OLMAYAN her STANDART aksiyon, kendi token'lı yerleşik şablonunu sızdırabilir.
  Tipik suçlular: InstallFiles, RemoveFiles, CreateFolders, RemoveFolders,
  WriteRegistryValues, RemoveRegistryValues, CreateShortcuts, RemoveShortcuts,
  ProcessComponents, RegisterProduct, RegisterUser, PublishProduct, PublishFeatures,
  UnpublishFeatures, RemoveExistingProducts, MigrateFeatureStates. Ayrıca kendi
  custom action'ların ve extension CA'ları (ör. WixUtil'in Wix4SchedServiceConfig_X64).

DÜZELT:
- ActionText satırı olmayan ve install/uninstall sırasında çalışan HER aksiyona
  <UI> içinde token'sız bir <ProgressText> ekle:
    <ProgressText Action="InstallFiles" Message="Copying files" Template="Copying application files" />
  Hem Message (üstteki açıklama = ActionText.Description) hem Template (detay satırı =
  ActionText.Template) token'sız olsun. ÖNEMLİ: ekrandaki "[1] [9] [6]"yı üreten
  Template sütunudur; sadece Message'ı set etmek yetmez.
- Doğrula: yeniden derle, `msiinfo export <msi> ActionText` ile HİÇBİR Template'te
  token ('[') kalmadığını VE install sırasında çalışan ActionText'siz standart
  aksiyon kalmadığını gör.
- DİKKAT: extension CA adları sürüme/bitness'e bağlıdır (Wix4SchedServiceConfig_X64
  vs _X86). Adı build çıktısından doğrula; tanımsız aksiyona ProgressText
  derlemeyi "undefined symbol" ile patlatabilir.

KURALLAR:
- Kod yorumları ve commit'ler İngilizce; Conventional Commits; Co-Authored-By EKLEME.
- Commit/push/tag işlemini ben onaylamadan yapma.
- WiX macOS'ta derlenmez; CI yeşil olmadan doğrulanmış sayma. Gerçek kurulumda gözle
  doğrula (geçici olarak adımlara kısa bir gecikme ekleyip metni okunur kılmak
  yardımcı olur; sonra gecikmeyi kaldır).
```

## msiinfo ile teşhis — pratik tek komut

```bash
# Token içeren (ekrana [1] sızdırabilecek) ActionText satırları:
msiinfo export MyAppSetup.msi ActionText | awk -F'\t' 'NR>3 && $3 ~ /\[/ {print $1" => "$3}'

# Çalışan aksiyonlar (sıralı) — ActionText'te olmayanları gözle ayıkla:
msiinfo export MyAppSetup.msi InstallExecuteSequence \
  | awk -F'\t' 'NR>3 {print $3"\t"$1}' | sort -n | awk -F'\t' '{print $2}'
```

## Referans dosyalar (bu projede)

- `Installer/Package.wxs` — `<UI>` içindeki `<ProgressText>` override'ları (standart
  dosya/registry/shortcut/product aksiyonları + servis aksiyonları + custom action'lar)

## Neden bu detaylar?

| Detay | Neden |
| --- | --- |
| `InstallFiles` en görünür | "Copying new files File: [1], Directory: [9], Size: [6]" en uzun adımdır |
| `Template` attribute | Ekrandaki token'ları üreten ActionText.Template sütunudur; Message değil |
| Standart aksiyonların yerleşik şablonu | Satır yoksa Windows Installer kendi token'lı varsayılanını gösterir |
| Önce MSI'ı dök | Tahminle değil; InstallExecuteSequence vs ActionText farkıyla kanıtla bul |
| Extension CA adını doğrula | `Wix4...` adları sürüm/bitness'e göre değişir; yanlışsa derleme patlar |
