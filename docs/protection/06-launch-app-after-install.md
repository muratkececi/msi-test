# Adım 6 — Kurulum sonrası uygulamayı otomatik başlatma

Kurulum sihirbazının son sayfasına "Launch <App>" seçeneği (varsayılan işaretli)
ekler ve kullanıcı "Finish" deyince uygulamayı **kullanıcı bağlamında** başlatır.
WiX MSI + WixUI varsayar.

> **Püf noktası:** Uygulamayı düz bir exe custom action ile başlatma — o, yükseltilmiş
> (SYSTEM/elevated) installer bağlamında çalışır ve UI düzgün açılmaz / yetki
> sorunları çıkar. WixUtil'in `WixShellExec`'ini `Impersonate="yes"` ile kullan;
> uygulama oturum açmış KULLANICI olarak başlar.

## Prompt

```text
Kurulum tamamlanınca (sihirbazın son sayfasında) uygulamayı otomatik başlatma
seçeneği eklemeni istiyorum. WiX MSI + WixUI kullanıyorum.

EKLE:
- Son sayfada (ExitDialog) varsayılan İŞARETLİ bir "Launch <App>" onay kutusu:
    <Property Id="WIXUI_EXITDIALOGOPTIONALCHECKBOXTEXT" Value="Launch <App>" />
    <Property Id="WIXUI_EXITDIALOGOPTIONALCHECKBOX" Value="1" />
- Uygulamayı KULLANICI bağlamında başlatan bir custom action (WixUtil WixShellExec):
    <Property Id="WixShellExecTarget" Value="[INSTALLFOLDER]<App>.exe" />
    <CustomAction Id="LaunchApplication" BinaryRef="Wix4UtilCA_X64"
                  DllEntry="WixShellExec" Impersonate="yes"
                  Execute="immediate" Return="ignore" />
- "Finish" butonuna, kutu işaretliyse ve YENİ kurulumsa çalışacak şekilde bağla:
    <UI>
      <Publish Dialog="ExitDialog" Control="Finish" Event="DoAction"
               Value="LaunchApplication"
               Condition="WIXUI_EXITDIALOGOPTIONALCHECKBOX = 1 AND NOT Installed" />
    </UI>

DİKKAT:
- WixShellExecTarget'ı [#FileId] ile değil [INSTALLFOLDER]<App>.exe yolu ile ver —
  dosyalar <Files> ile harvest edildiyse elle bir File Id yoktur.
- Impersonate="yes" ŞART: aksi halde uygulama SYSTEM olarak açılır (UI sorunları,
  ServiceController gibi kullanıcı-bağlamı işleri patlar).
- WixUtil binary adı bitness'e bağlı: x64 build'de Wix4UtilCA_X64. Build çıktısından
  doğrula; util:ServiceConfig vb. zaten kullanıyorsan referans mevcuttur.
- Sadece NOT Installed (yeni kurulum) koşuluyla; onarım/kaldırmada başlatma.

KURALLAR:
- Kod yorumları ve commit'ler İngilizce; Conventional Commits; Co-Authored-By EKLEME.
- Commit/push/tag işlemini ben onaylamadan yapma.
- WiX macOS'ta derlenmez; CI yeşil olmadan doğrulanmış sayma.
```

## Referans dosyalar (bu projede)

- `Installer/Package.wxs` — `WixShellExecTarget` property, `LaunchApplication` CA ve
  ExitDialog `Publish` bağlaması

## Neden bu detaylar?

| Detay | Neden |
| --- | --- |
| `WixShellExec` + `Impersonate="yes"` | Uygulamayı kullanıcı bağlamında açar; elevated installer bağlamı UI/yetki sorunları çıkarır |
| `[INSTALLFOLDER]...exe` yolu | `<Files>` harvest'te elle File Id yoktur, `[#Id]` çözülmez |
| `NOT Installed` koşulu | Yalnızca yeni kurulumda başlat; onarım/kaldırmada değil |
| Bitness'li binary adı | `Wix4UtilCA_X64` (x64); yanlışsa derleme/çalışma patlar |
