# Adım 4 — Kurulumdaki ham `[1] [2]` metnini temizleme

> ⚠️ **DURUM: HENÜZ DOĞRULANMADI.** Bu adımdaki yaklaşım `msi-test` projesinde
> uygulandı ama gerçek kurulumda `[1] [2]`'nin tamamen gittiği **henüz teyit
> edilmedi**. Aşağıdaki teşhis doğru ama "kesin çözüm" demeden önce kendi kurulumunda
> doğrula. Doğrulandığında bu dosya kesinleştirilecek.

Kurulum sırasında ilerleme çubuğunda görünen ham `Service: [1] [2]` /  `[1]` gibi
yer-tutucu metinleri temizler. Tamamen kozmetik bir sorundur.

## Teşhis (msi-test'te kanıtlandı)

MSI'ın `ActionText` tablosu doğrudan incelendiğinde (macOS'ta `msitools`:
`msiinfo export MyAppSetup.msi ActionText`) şu görüldü:

- Standart `InstallServices`/`StartServices`/`StopServices`/`DeleteServices`
  aksiyonlarına temiz ActionText satırı eklendiğinde onlarda `[1] [2]` KALMADI.
- Ham `[1] [2]`, ActionText satırı OLMAYAN aksiyonlardan geliyordu: kendi deferred
  custom action'larımız (sc start, vb.) ve WiX util'in service-config CA'sı
  (`Wix4SchedServiceConfig_X64`). Bir aksiyonun ActionText satırı yoksa Windows
  Installer yerleşik, `[1]`/`[2]` içeren bir varsayılan şablon gösterir.

## Prompt

```text
Kurulum sırasında ilerleme çubuğunda görünen ham "[1] [2]" / "Service: [1]"
metnini temizlemeni istiyorum (kozmetik). WiX MSI varsayıyorum.

ÖNCE TEŞHİS ET (tahmin etme — MSI'ın içine bak):
- MSI'ı derle, sonra ActionText tablosunu dök ve [1]/[2] içeren ya da ActionText
  satırı OLMAYAN aksiyonları bul. (macOS'ta: brew install msitools; sonra
  `msiinfo export <msi> ActionText` ve `msiinfo export <msi> InstallExecuteSequence`.
  Windows'ta Orca.) Install sırasında çalışan TÜM aksiyonları listele; hangilerinin
  ActionText satırı var, hangilerinin yok tespit et.

DÜZELT:
- Install/uninstall sırasında çalışan HER aksiyona (standart servis aksiyonları,
  kendi custom action'ların VE WiX util'in service-config CA'ları gibi extension
  aksiyonları dahil) <UI> içinde token'sız bir <ProgressText> satırı ekle:
    <ProgressText Action="X" Message="..." Template="..." />
  Hem Message (üstteki açıklama satırı = ActionText.Description) hem Template
  (detay satırı = ActionText.Template) token'sız (içinde [1]/[2] olmayan) olsun.
  ÖNEMLİ: [1] [2]'yi üreten Template sütunudur; sadece Message yetmez.
- Doğrula: yeniden derle, ActionText'i tekrar dök, HİÇBİR satırda [1]/[2] kalmadığını
  ve ActionText satırı olmayan (install sırasında çalışan) aksiyon kalmadığını gör.
- DİKKAT: extension CA adları sürüme/bitness'e bağlıdır (ör. Wix4SchedServiceConfig_X64
  vs _X86). Adı build çıktısından doğrula; tanımsız aksiyona ProgressText referansı
  derlemede "undefined symbol" verebilir.

KURALLAR:
- Kod yorumları ve commit'ler İngilizce; Conventional Commits; Co-Authored-By EKLEME.
- Commit/push/tag işlemini ben onaylamadan yapma.
- WiX macOS'ta derlenmez; CI yeşil olmadan doğrulanmış sayma. Düzeltmeyi gerçek bir
  kurulumda gözle doğrula (geçici olarak adımlara kısa bir gecikme ekleyip metni
  okunur kılmak yardımcı olur; sonra gecikmeyi kaldır).
```

## Referans dosyalar (bu projede)

- `Installer/Package.wxs` — `<UI>` içindeki `<ProgressText>` override'ları

## Neden bu detaylar?

| Detay | Neden |
| --- | --- |
| `Template` attribute | `[1] [2]`'yi üreten ActionText.Template sütunudur; Message (Description) değil |
| Tüm CA'lara satır | ActionText satırı olmayan aksiyon yerleşik `[1]` şablonu gösterir |
| Extension CA adını doğrula | `Wix4...` adları sürüm/bitness'e göre değişir; yanlışsa derleme patlar |
| Önce MSI'ı dök | Hangi aksiyonun metni sızdırdığını tahminle değil kanıtla bul |
