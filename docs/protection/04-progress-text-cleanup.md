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
ActionText satırlarını düzeltmek bunu KALDIRMAZ (çünkü kaynak ActionText değil).
Çözüm: PrepareDlg'i, WixUI'nin standart tanımıyla aynı ama **ActionData Control'ü
çıkarılmış** kendi sürümünle override etmek. (NOT: ProgressDlg yalnızca ActionText'e
abonedir, sorun PrepareDlg'dedir — EventMapping tablosunu dökerek doğrula.)

> Kaynak 1'in en görünür örneği `InstallFiles`'tır; Kaynak 2 ise install VE repair
> sırasında "File: [1]" olarak görünen kalıcı token'dır.

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
- KAYNAK 2 için: WixUI'nin PrepareDlg'ini override et. Standart PrepareDlg tanımının
  AYNISINI yaz (aynı Id/boyut/loc string'leri — WiX kaynağından kopyala) ama
  "ActionData" olayına abone <Control>'ü ÇIKAR. Aynı Id'li Dialog, extension'ınkini
  ezer. Böylece "File: [1]" hiç render edilmez. (PrepareDlg kaynağı:
  github.com/wixtoolset/wix → src/ext/UI/wixlib/PrepareDlg.wxs)
- Doğrula: yeniden derle; `msiinfo export <msi> ActionText` → hiçbir Template'te token
  ('[') kalmasın; `msiinfo export <msi> EventMapping` → PrepareDlg'in ActionData satırı
  kalmasın. Gerçek kurulum + onarım (repair) ile gözle de doğrula.
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
| PrepareDlg override | "File: [1]" ActionData'dan gelir; ActionText düzeltmesiyle gitmez |
| Önce MSI'ı dök | Tahminle değil; ActionText + EventMapping tablolarıyla kanıtla bul |
| Extension CA adını doğrula | `Wix4...` adları sürüm/bitness'e göre değişir; yanlışsa derleme patlar |
