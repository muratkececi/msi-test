# Adım 4 — Kurulumdaki ham `[1] [2] [9] [6]` metnini temizleme

Kurulum/kaldırma sırasında ilerleme çubuğunda görünen ham yer-tutucu metinleri
temizler, örn:

```text
Status:  Copying new files   File: [1], Directory: [9], Size: [6]
```

Tamamen kozmetik bir sorundur ama profesyonel görünüm için giderilir.

## Kök neden — İKİ AYRI KAYNAK var (msi-test'te kanıtlandı)

Bu token'lar **iki farklı yerden** gelir; ikisini de kapatman gerekir:

**Kaynak 1 — ActionText.Template (Directory/Size gibi).** Standart MSI aksiyonları
(`InstallFiles`, `RemoveFiles`, `WriteRegistryValues` ...) için Windows Installer'ın
**yerleşik bir ActionText şablonu** vardır; `[1] [9] [6]` token'ları içerir. O aksiyon
için ActionText satırın YOKSA bu yerleşik şablon gösterilir. Çözüm: o aksiyonlara
token'sız bir `<ProgressText ... Template="...">` satırı eklemek.

**Kaynak 2 — PrepareDlg'in ActionData kontrolü ("File: [1]").** WixUI'nin **PrepareDlg**
diyaloğunda, `ActionData` olayına abone bir Text kontrolü vardır. ActionData'nın
ActionText gibi bir şablonu yoktur; motor ham veriyi `File: [1]` biçiminde basar.
ActionText satırlarını düzeltmek bunu KALDIRMAZ (kaynak ActionText değil). (NOT:
ProgressDlg yalnızca ActionText'e abonedir, sorun PrepareDlg'dedir — EventMapping
tablosunu dökerek doğrula.)

> ⚠️ **Kaynak 2'nin çözümü ZOR.** `ui:WixUI` (örn. WixUI_InstallDir) kullanırken
> aynı Id ile `<Dialog Id="PrepareDlg">` yeniden tanımLANAMAZ — WiX "Duplicate Control
> ... PrepareDlg/..." hatası verir; extension'ın dialog'unu Id ile ezemezsin. Tek
> temiz yol `ui:WixUI`'yi BIRAKIP tüm WixUI_InstallDir dialog setini (tüm dialog'lar +
> InstallUISequence + publish'ler) elle inline etmek ve içine ActionData kontrolü
> çıkarılmış kendi PrepareDlg'ini koymaktır — yani tek bir PrepareDlg tanımı olur.
> Bu invaziv ve kırılgandır; "File: [1]" tamamen kozmetik olduğundan bu projede
> KASITLI olarak yapılmadı, bilinen sınır olarak bırakıldı.

> Kaynak 1'in en görünür örneği `InstallFiles`'tır; Kaynak 2 ise install VE repair
> sırasında "File: [1]" olarak görünen, ActionText ile gitmeyen kalıcı token'dır.

## Prompt

```text
Kurulum/kaldırma sırasında ilerleme çubuğunda görünen ham "[1]", "File: [1],
Directory: [9], Size: [6]" gibi yer-tutucu metinleri temizlemeni istiyorum
(kozmetik). WiX MSI varsayıyorum.

ÖNCE TEŞHİS ET (tahmin etme — MSI'ın İÇİNE bak):
- MSI'ı derle. macOS'ta: `brew install msitools`. Sonra:
    msiinfo export <msi> InstallExecuteSequence   # çalışan TÜM aksiyonlar + sıra
    msiinfo export <msi> ActionText               # ActionText satırı OLAN aksiyonlar
    msiinfo export <msi> EventMapping              # ActionData/ActionText abonelikleri
  (Windows'ta Orca ile aynı tablolara bak.)
- KAYNAK 1: InstallExecuteSequence'te çalışan ama ActionText'te satırı OLMAYAN her
  STANDART aksiyon, kendi token'lı yerleşik şablonunu sızdırabilir. Tipik suçlular:
  InstallFiles, RemoveFiles, CreateFolders, RemoveFolders, WriteRegistryValues,
  RemoveRegistryValues, CreateShortcuts, RemoveShortcuts, ProcessComponents,
  RegisterProduct, RegisterUser, PublishProduct, PublishFeatures, UnpublishFeatures,
  RemoveExistingProducts, MigrateFeatureStates. Ayrıca kendi custom action'ların ve
  extension CA'ları (ör. WixUtil'in Wix4SchedServiceConfig_X64).
- KAYNAK 2: EventMapping'te "ActionData" olayına abone bir kontrol var mı bak —
  tipik olarak PrepareDlg'de bir ActionData Text kontrolü "File: [1]" basar. Bu,
  ActionText düzeltmeleriyle GİTMEZ.

DÜZELT:
- KAYNAK 1 için: ActionText satırı olmayan ve install/uninstall sırasında çalışan
  HER aksiyona <UI> içinde token'sız bir <ProgressText> ekle:
    <ProgressText Action="InstallFiles" Message="Copying files" Template="Copying application files" />
  Hem Message (= ActionText.Description) hem Template (= ActionText.Template) token'sız
  olsun. ÖNEMLİ: ekrandaki token'ları üreten Template sütunudur; sadece Message yetmez.
- KAYNAK 2 için (zor, opsiyonel): "File: [1]" PrepareDlg'in ActionData kontrolünden
  gelir. DİKKAT: `ui:WixUI` kullanırken aynı Id ile <Dialog Id="PrepareDlg"> yeniden
  TANIMLANAMAZ — WiX "Duplicate Control" hatası verir. Tek temiz yol `ui:WixUI`'yi
  bırakıp tüm WixUI_InstallDir dialog setini elle inline etmek (kaynak:
  github.com/wixtoolset/wix → src/ext/UI/wixlib/) ve içine ActionData kontrolü
  çıkarılmış PrepareDlg koymaktır. Invaziv/kırılgan; "File: [1]" tamamen kozmetik
  olduğundan çoğu projede bunu YAPMAMAK ve bilinen sınır olarak bırakmak makuldür.
- Doğrula: yeniden derle; `msiinfo export <msi> ActionText` → hiçbir Template'te token
  ('[') kalmasın. Kaynak 2'yi (PrepareDlg ActionData) çözmediysen "File: [1]" install
  ve repair'de görünmeye devam eder — bu bilinçli bir karar olabilir.
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

# KAYNAK 2: ActionData olayına abone kontroller (PrepareDlg "File: [1]" suçlusu):
msiinfo export MyAppSetup.msi EventMapping | grep -i ActionData
```

## Referans dosyalar (bu projede)

- `Installer/Package.wxs` — `<UI>` içindeki `<ProgressText>` override'ları (standart
  dosya/registry/shortcut/product aksiyonları + servis aksiyonları + custom action'lar)
  VE ActionData kontrolü çıkarılmış `PrepareDlg` override'ı

## Neden bu detaylar?

| Detay | Neden |
| --- | --- |
| İKİ kaynak var | ActionText.Template (Directory/Size) + PrepareDlg ActionData (File: [1]) |
| `Template` attribute | ActionText token'larını üretir; Message (Description) değil |
| Standart aksiyonların yerleşik şablonu | ActionText satırı yoksa motor token'lı varsayılanı gösterir |
| "File: [1]" ayrı/zor | PrepareDlg ActionData'dan gelir; ui:WixUI ile override edilemez (duplicate), inline dialog seti gerekir |
| Önce MSI'ı dök | Tahminle değil; ActionText + EventMapping tablolarıyla kanıtla bul |
| Extension CA adını doğrula | `Wix4...` adları sürüm/bitness'e göre değişir; yanlışsa derleme patlar |
