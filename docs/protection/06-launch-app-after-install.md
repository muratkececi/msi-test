# Adim 6 - Kurulum sonrasi uygulamayi otomatik baslatma

Kurulum sihirbazinin son sayfasina "Launch <App>" secenegi (varsayilan isaretli)
ekler ve kullanici "Finish" deyince uygulamayi **kullanici baglaminda** baslatir.
WiX MSI + WixUI varsayar.

> **Puf noktasi:** Uygulamayi duz bir exe custom action ile baslatma - o, yukseltilmis
> (SYSTEM/elevated) installer baglaminda calisir ve UI duzgun acilmaz / yetki
> sorunlari cikar. WixUtil'in `WixShellExec`'ini `Impersonate="yes"` ile kullan;
> uygulama oturum acmis KULLANICI olarak baslar.

## Prompt

```text
Kurulum tamamlaninca (sihirbazin son sayfasinda) uygulamayi otomatik baslatma
secenegi eklemeni istiyorum.

ONCE KESFET (varsayim yapma):
- Installer WiX MSI mi VE bir WixUI dialog seti (or. WixUI_InstallDir) kullaniyor mu?
  ExitDialog/WIXUI_EXITDIALOGOPTIONALCHECKBOX yaklasimi buna bagli. WiX degilse veya
  WixUI yoksa (UI'siz/ozel UI), bana soyle - baslatmayi oraya nasil baglayacagini
  ozetleyip ONAY AL.
- Installer'in bitness'ini cikar: asagidaki Wix4UtilCA_X64 x64 icindir; x86 ise _X86.
- Baslatilacak exe'nin gercek adini ve INSTALLFOLDER Id'sini repo'dan dogrula
  (<App> ve [INSTALLFOLDER]'i buna gore degistir).

EKLE:
- Son sayfada (ExitDialog) varsayilan ISARETLI bir "Launch <App>" onay kutusu:
    <Property Id="WIXUI_EXITDIALOGOPTIONALCHECKBOXTEXT" Value="Launch <App>" />
    <Property Id="WIXUI_EXITDIALOGOPTIONALCHECKBOX" Value="1" />
- Uygulamayi KULLANICI baglaminda baslatan bir custom action (WixUtil WixShellExec):
    <Property Id="WixShellExecTarget" Value="[INSTALLFOLDER]<App>.exe" />
    <CustomAction Id="LaunchApplication" BinaryRef="Wix4UtilCA_X64"
                  DllEntry="WixShellExec" Impersonate="yes"
                  Execute="immediate" Return="ignore" />
- "Finish" butonuna, kutu isaretliyse ve YENI kurulumsa calisacak sekilde bagla:
    <UI>
      <Publish Dialog="ExitDialog" Control="Finish" Event="DoAction"
               Value="LaunchApplication"
               Condition="WIXUI_EXITDIALOGOPTIONALCHECKBOX = 1 AND NOT Installed" />
    </UI>

DIKKAT:
- WixShellExecTarget'i [#FileId] ile degil [INSTALLFOLDER]<App>.exe yolu ile ver -
  dosyalar <Files> ile harvest edildiyse elle bir File Id yoktur.
- Impersonate="yes" SART: aksi halde uygulama SYSTEM olarak acilir (UI sorunlari,
  ServiceController gibi kullanici-baglami isleri patlar).
- WixUtil binary adi bitness'e bagli: x64 build'de Wix4UtilCA_X64. Build ciktisindan
  dogrula; util:ServiceConfig vb. zaten kullaniyorsan referans mevcuttur.
- Sadece NOT Installed (yeni kurulum) kosuluyla; onarim/kaldirmada baslatma.

KURALLAR:
- Kod yorumlari ve commit'ler Ingilizce; Conventional Commits; Co-Authored-By EKLEME.
- Commit/push/tag islemini ben onaylamadan yapma.
- WiX macOS'ta derlenmez; CI yesil olmadan dogrulanmis sayma.
```

## Referans dosyalar (bu projede)

- `Installer/Package.wxs` - `WixShellExecTarget` property, `LaunchApplication` CA ve
  ExitDialog `Publish` baglamasi

## Neden bu detaylar?

| Detay | Neden |
| --- | --- |
| `WixShellExec` + `Impersonate="yes"` | Uygulamayi kullanici baglaminda acar; elevated installer baglami UI/yetki sorunlari cikarir |
| `[INSTALLFOLDER]...exe` yolu | `<Files>` harvest'te elle File Id yoktur, `[#Id]` cozulmez |
| `NOT Installed` kosulu | Yalnizca yeni kurulumda baslat; onarim/kaldirmada degil |
| Bitness'li binary adi | `Wix4UtilCA_X64` (x64); yanlissa derleme/calisma patlar |
