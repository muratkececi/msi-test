# Koruma Katmanlari - Adim Adim Prompt'lar

Bu klasor, `msi-test` projesindeki korumalari **mevcut baska bir projeye** parca
parca eklemek icin hazirlanmis, birbirinden bagimsiz prompt dosyalari icerir.
Buyuk bir projeye hepsini birden vermek yerine, asagidaki sirayla **tek tek**
uygula; her dosya tek bir yetenek ekler.

| Sira | Dosya | Ne ekler |
| --- | --- | --- |
| 1 | [01-uninstall-password.md](01-uninstall-password.md) | Kaldirma sirasinda master parola soran koruma |
| 2 | [02-taskmanager-protection.md](02-taskmanager-protection.md) | Task Manager "End task" ile oldurmeyi engelleme (process DACL) |
| 3 | [03-services-stop-protection.md](03-services-stop-protection.md) | services.msc / `sc stop` ile durdurmayi engelleme (service SDDL) |
| 4 | [04-progress-text-cleanup.md](04-progress-text-cleanup.md) | Kurulumdaki ham `[1] [2] [9] [6]` metnini temizleme (InstallFiles vb.) |
| 5 | [05-desktop-stop-start.md](05-desktop-stop-start.md) | Masaustu uygulamasindan servisi durdurma (master parola) / baslatma |
| 6 | [06-launch-app-after-install.md](06-launch-app-after-install.md) | Kurulum sonrasi uygulamayi otomatik baslatma |

## Nasil kullanilir

1. Hedef projede Claude Code'u (veya bir ajani) ac.
2. Ilgili adimin `.md` dosyasindaki **prompt blogunu** kopyalayip ver.
3. Ajan once mevcut yapiyi kesfeder, sonra uyarlar. Her adimi bitirip test
   ettikten sonra bir sonrakine gec.

> Her prompt zaten "ONCE KESFET - varsayim yapma" ile baslar; uymayan bir durum
> (WiX degil, x86, mevcut service baska TFM'de) gorurse durup sana sorar. Yine de
> asagidaki tablo, yapistirmadan once uyumu bir bakista gormen icindir.

## Baslamadan once - hedef proje uyum kontrolu

| Soru | Onemi |
| --- | --- |
| Installer **WiX/MSI** mi? | Adim 1, 4, 6 dogrudan WiX'e bagli; 4 (kozmetik) WiX disinda ATLANIR. Degilse ajan durup uyarlama onerir. |
| Bitness **x64 mu x86 mi**? | `Wix4UtilCA_X64`/`ProgramFiles64Folder` x64 icindir; x86'da `_X86` ve farkli klasor. |
| Zaten bir **background service** var mi? TFM'i ne? | Adim 2 varsa onu kullanir; mevcut TFM `net9.0-windows` ise duz `net9.0`'a cevirmek WPF/WinForms referanslarini kirabilir. |
| Bir **masaustu uygulamasi** var mi (WPF/WinForms)? | Adim 5 buna bagli; yoksa app-driven stop/start uygulanamaz. |
| Tum adimlar **ayni parola hash'ini** mi kullanacak? | Adim 1 (uninstall) ve Adim 5 (app-stop) AYNI SHA-256'yi paylasmali; yoksa iki ayri parola olusur. |
| App + service **ayni ProgramData yolunu** mu kullaniyor? | Adim 5'teki IPC kontrol dosyasi icin SART (or. `C:\ProgramData\<App>`). |

## Genel kurallar (her adim icin gecerli)

Bu kurallari her prompt'a ekleyebilir ya da hedef projenin `CLAUDE.md`'sine
bir kez yazabilirsin:

- Once mevcut yapiyi oku (installer tipi, TFM, klasor duzeni), sonra uyarla.
- Kod yorumlari ve commit mesajlari **Ingilizce**; commit'ler Conventional
  Commits, `Co-Authored-By` trailer **ekleme**.
- Commit / push / tag islemini **onay almadan yapma**.
- WiX **macOS/Linux'ta derlenmez**; degisiklikleri CI (windows-latest) yesil
  olana kadar dogrulanmis sayma.
- Bu korumalar **caydiricidir**, kernel duzeyi degildir; kararli bir admin
  geri alabilir. README'de bunu durustce belgele.

> **Not:** Tam (birlesik) surum hala [../PROTECTION-PROMPT.md](../PROTECTION-PROMPT.md)
> dosyasinda duruyor. Bu klasor onun parcalanmis halidir.
