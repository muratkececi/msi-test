# Koruma Katmanları — Adım Adım Prompt'lar

Bu klasör, `msi-test` projesindeki korumaları **mevcut başka bir projeye** parça
parça eklemek için hazırlanmış, birbirinden bağımsız prompt dosyaları içerir.
Büyük bir projeye hepsini birden vermek yerine, aşağıdaki sırayla **tek tek**
uygula; her dosya tek bir yetenek ekler.

| Sıra | Dosya | Ne ekler |
| --- | --- | --- |
| 1 | [01-uninstall-password.md](01-uninstall-password.md) | Kaldırma sırasında master parola soran koruma |
| 2 | [02-taskmanager-protection.md](02-taskmanager-protection.md) | Task Manager "End task" ile öldürmeyi engelleme (process DACL) |
| 3 | [03-services-stop-protection.md](03-services-stop-protection.md) | services.msc / `sc stop` ile durdurmayı engelleme (service SDDL) |
| 4 | [04-progress-text-cleanup.md](04-progress-text-cleanup.md) | Kurulumdaki ham `[1] [2] [9] [6]` metnini temizleme (InstallFiles vb.) |
| 5 | [05-desktop-stop-start.md](05-desktop-stop-start.md) | Masaüstü uygulamasından servisi durdurma (master parola) / başlatma |
| 6 | [06-launch-app-after-install.md](06-launch-app-after-install.md) | Kurulum sonrası uygulamayı otomatik başlatma |

## Nasıl kullanılır

1. Hedef projede Claude Code'u (veya bir ajanı) aç.
2. İlgili adımın `.md` dosyasındaki **prompt bloğunu** kopyalayıp ver.
3. Ajan önce mevcut yapıyı keşfeder, sonra uyarlar. Her adımı bitirip test
   ettikten sonra bir sonrakine geç.

## Genel kurallar (her adım için geçerli)

Bu kuralları her prompt'a ekleyebilir ya da hedef projenin `CLAUDE.md`'sine
bir kez yazabilirsin:

- Önce mevcut yapıyı oku (installer tipi, TFM, klasör düzeni), sonra uyarla.
- Kod yorumları ve commit mesajları **İngilizce**; commit'ler Conventional
  Commits, `Co-Authored-By` trailer **ekleme**.
- Commit / push / tag işlemini **onay almadan yapma**.
- WiX **macOS/Linux'ta derlenmez**; değişiklikleri CI (windows-latest) yeşil
  olana kadar doğrulanmış sayma.
- Bu korumalar **caydırıcıdır**, kernel düzeyi değildir; kararlı bir admin
  geri alabilir. README'de bunu dürüstçe belgele.

> **Not:** Tam (birleşik) sürüm hâlâ [../PROTECTION-PROMPT.md](../PROTECTION-PROMPT.md)
> dosyasında duruyor. Bu klasör onun parçalanmış halidir.
