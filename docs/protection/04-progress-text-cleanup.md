# Adim 4 - Kurulumdaki ham `[1] [2] [9] [6]` metnini temizleme

Kurulum/kaldirma sirasinda ilerleme cubugunda gorunen ham yer-tutucu metinleri
temizler, orn:

```text
Status:  Copying new files   File: [1], Directory: [9], Size: [6]
```

Tamamen kozmetik bir sorundur ama profesyonel gorunum icin giderilir.

## Kok neden - IKI AYRI KAYNAK var (msi-test'te kanitlandi)

Bu token'lar **iki farkli yerden** gelir; ikisini de kapatman gerekir:

**Kaynak 1 - ActionText.Template (Directory/Size gibi).** Standart MSI aksiyonlari
(`InstallFiles`, `RemoveFiles`, `WriteRegistryValues` ...) icin Windows Installer'in
**yerlesik bir ActionText sablonu** vardir; `[1] [9] [6]` token'lari icerir. O aksiyon
icin ActionText satirin YOKSA bu yerlesik sablon gosterilir. Cozum: o aksiyonlara
token'siz bir `<ProgressText ... Template="...">` satiri eklemek.

**Kaynak 2 - PrepareDlg'in ActionData kontrolu ("File: [1]").** WixUI'nin **PrepareDlg**
diyalogunda, `ActionData` olayina abone bir Text kontrolu vardir. ActionData'nin
ActionText gibi bir sablonu yoktur; motor ham veriyi `File: [1]` biciminde basar.
ActionText satirlarini duzeltmek bunu KALDIRMAZ (kaynak ActionText degil). (NOT:
ProgressDlg yalnizca ActionText'e abonedir, sorun PrepareDlg'dedir - EventMapping
tablosunu dokerek dogrula.)

> (!) **Kaynak 2'nin cozumu ZOR.** `ui:WixUI` (orn. WixUI_InstallDir) kullanirken
> ayni Id ile `<Dialog Id="PrepareDlg">` yeniden tanimLANAMAZ - WiX "Duplicate Control
> ... PrepareDlg/..." hatasi verir; extension'in dialog'unu Id ile ezemezsin. Tek
> temiz yol `ui:WixUI`'yi BIRAKIP tum WixUI_InstallDir dialog setini (tum dialog'lar +
> InstallUISequence + publish'ler) elle inline etmek ve icine ActionData kontrolu
> cikarilmis kendi PrepareDlg'ini koymaktir - yani tek bir PrepareDlg tanimi olur.
> Bu invaziv ve kirilgandir; "File: [1]" tamamen kozmetik oldugundan bu projede
> KASITLI olarak yapilmadi, bilinen sinir olarak birakildi.

> Kaynak 1'in en gorunur ornegi `InstallFiles`'tir; Kaynak 2 ise install VE repair
> sirasinda "File: [1]" olarak gorunen, ActionText ile gitmeyen kalici token'dir.

## Prompt

```text
Kurulum/kaldirma sirasinda ilerleme cubugunda gorunen ham "[1]", "File: [1],
Directory: [9], Size: [6]" gibi yer-tutucu metinleri temizlemeni istiyorum
(kozmetik). Bu adim YALNIZCA WiX/MSI'a ozeldir - installer WiX degilse (MSIX, Inno,
vb.) bu sorun ve cozum gecerli degildir; o durumda bunu bana soyle ve bu adimi ATLA.

ONCE TESHIS ET (tahmin etme - MSI'in ICINE bak):
- MSI'i derle. macOS'ta: `brew install msitools`. Sonra:
    msiinfo export <msi> InstallExecuteSequence   # calisan TUM aksiyonlar + sira
    msiinfo export <msi> ActionText               # ActionText satiri OLAN aksiyonlar
    msiinfo export <msi> EventMapping              # ActionData/ActionText abonelikleri
  (Windows'ta Orca ile ayni tablolara bak.)
- KAYNAK 1: InstallExecuteSequence'te calisan ama ActionText'te satiri OLMAYAN her
  STANDART aksiyon, kendi token'li yerlesik sablonunu sizdirabilir. Tipik suclular:
  InstallFiles, RemoveFiles, CreateFolders, RemoveFolders, WriteRegistryValues,
  RemoveRegistryValues, CreateShortcuts, RemoveShortcuts, ProcessComponents,
  RegisterProduct, RegisterUser, PublishProduct, PublishFeatures, UnpublishFeatures,
  RemoveExistingProducts, MigrateFeatureStates. Ayrica kendi custom action'larin ve
  extension CA'lari (or. WixUtil'in Wix4SchedServiceConfig_X64).
- KAYNAK 2: EventMapping'te "ActionData" olayina abone bir kontrol var mi bak -
  tipik olarak PrepareDlg'de bir ActionData Text kontrolu "File: [1]" basar. Bu,
  ActionText duzeltmeleriyle GITMEZ.

DUZELT:
- KAYNAK 1 icin: ActionText satiri olmayan ve install/uninstall sirasinda calisan
  HER aksiyona <UI> icinde token'siz bir <ProgressText> ekle:
    <ProgressText Action="InstallFiles" Message="Copying files" Template="Copying application files" />
  Hem Message (= ActionText.Description) hem Template (= ActionText.Template) token'siz
  olsun. ONEMLI: ekrandaki token'lari ureten Template sutunudur; sadece Message yetmez.
- KAYNAK 2 icin (zor, opsiyonel): "File: [1]" PrepareDlg'in ActionData kontrolunden
  gelir. DIKKAT: `ui:WixUI` kullanirken ayni Id ile <Dialog Id="PrepareDlg"> yeniden
  TANIMLANAMAZ - WiX "Duplicate Control" hatasi verir. Tek temiz yol `ui:WixUI`'yi
  birakip tum WixUI_InstallDir dialog setini elle inline etmek (kaynak:
  github.com/wixtoolset/wix -> src/ext/UI/wixlib/) ve icine ActionData kontrolu
  cikarilmis PrepareDlg koymaktir. Invaziv/kirilgan; "File: [1]" tamamen kozmetik
  oldugundan cogu projede bunu YAPMAMAK ve bilinen sinir olarak birakmak makuldur.
- Dogrula: yeniden derle; `msiinfo export <msi> ActionText` -> hicbir Template'te token
  ('[') kalmasin. Kaynak 2'yi (PrepareDlg ActionData) cozmediysen "File: [1]" install
  ve repair'de gorunmeye devam eder - bu bilincli bir karar olabilir.
- DIKKAT: extension CA adlari surume/bitness'e baglidir (Wix4SchedServiceConfig_X64
  vs _X86). Adi build ciktisindan dogrula; tanimsiz aksiyona ProgressText
  derlemeyi "undefined symbol" ile patlatabilir.

KURALLAR:
- Kod yorumlari ve commit'ler Ingilizce; Conventional Commits; Co-Authored-By EKLEME.
- Commit/push/tag islemini ben onaylamadan yapma.
- WiX macOS'ta derlenmez; CI yesil olmadan dogrulanmis sayma. Gercek kurulumda gozle
  dogrula (gecici olarak adimlara kisa bir gecikme ekleyip metni okunur kilmak
  yardimci olur; sonra gecikmeyi kaldir).
```

## msiinfo ile teshis - pratik tek komut

```bash
# Token iceren (ekrana [1] sizdirabilecek) ActionText satirlari:
msiinfo export MyAppSetup.msi ActionText | awk -F'\t' 'NR>3 && $3 ~ /\[/ {print $1" => "$3}'

# Calisan aksiyonlar (sirali) - ActionText'te olmayanlari gozle ayikla:
msiinfo export MyAppSetup.msi InstallExecuteSequence \
  | awk -F'\t' 'NR>3 {print $3"\t"$1}' | sort -n | awk -F'\t' '{print $2}'

# KAYNAK 2: ActionData olayina abone kontroller (PrepareDlg "File: [1]" suclusu):
msiinfo export MyAppSetup.msi EventMapping | grep -i ActionData
```

## Referans dosyalar (bu projede)

- `Installer/Package.wxs` - `<UI>` icindeki `<ProgressText>` override'lari (standart
  dosya/registry/shortcut/product aksiyonlari + servis aksiyonlari + custom action'lar)
  VE ActionData kontrolu cikarilmis `PrepareDlg` override'i

## Neden bu detaylar?

| Detay | Neden |
| --- | --- |
| IKI kaynak var | ActionText.Template (Directory/Size) + PrepareDlg ActionData (File: [1]) |
| `Template` attribute | ActionText token'larini uretir; Message (Description) degil |
| Standart aksiyonlarin yerlesik sablonu | ActionText satiri yoksa motor token'li varsayilani gosterir |
| "File: [1]" ayri/zor | PrepareDlg ActionData'dan gelir; ui:WixUI ile override edilemez (duplicate), inline dialog seti gerekir |
| Once MSI'i dok | Tahminle degil; ActionText + EventMapping tablolariyla kanitla bul |
| Extension CA adini dogrula | `Wix4...` adlari surum/bitness'e gore degisir; yanlissa derleme patlar |
