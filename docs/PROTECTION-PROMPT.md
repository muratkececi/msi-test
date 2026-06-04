# Koruma Katmanlarını Başka Bir Projeye Ekleme — Hazır Prompt

Bu dosya, bu projedeki iki koruma katmanını (**durdurma koruması** ve
**kaldırma parolası**) mevcut başka bir projeye eklemek için kullanılan hazır
bir prompt içerir. Hedef projede Claude Code'a (veya bir ajana) aşağıdaki metni
verebilirsin.

> **İpucu:** Bu projedeki şu dosyalar birebir referanstır; istersen prompt'a
> iliştir ya da hedef projeye kopyala:
> `MyApp.Service/ProcessProtection.cs`, `MyApp.Service/ServiceProtection.cs`,
> `UninstallGuard/CustomActions.cs`, `Installer/Package.wxs`.

---

## Prompt

```
Bu projeye iki koruma katmanı eklemeni istiyorum. Referans olarak benim
github.com/muratkececi/msi-test projemde çalışan bir örnek var; oradaki
yaklaşımı bu projeye uyarla.

ÖNCE KEŞFET (varsayım yapma):
1. Bu projenin installer'ını tespit et: WiX mi (.wixproj / *.wxs / WixToolset.Sdk),
   Inno Setup mu (.iss), MSIX mi, yoksa başka bir şey mi? Bulduğunu bana söyle.
2. Uygulamanın TFM'ini, framework-dependent mi self-contained mı yayınlandığını,
   ve halihazırda bir Windows service ya da arka plan süreci olup olmadığını çıkar.
3. Installer WiX DEĞİLSE, koruma yaklaşımının nasıl uyarlanacağını (veya WiX'e
   geçiş gerekip gerekmediğini) önce bana özetle, kod yazmadan önce onay al.

EKLENECEK 1 — DURDURMA KORUMASI (arka plan service):
- LocalSystem altında çalışan, otomatik başlayan bir Windows service ekle (ya da
  varsa mevcut service'i kullan). Service projesi düz `net9.0` hedeflesin,
  `net9.0-windows` DEĞİL — `-windows` TFM'i Microsoft.WindowsDesktop.App bağımlılığı
  ekler ve service Desktop runtime yoksa SCM başlangıcında çöker.
- Service başlarken iki şeyi yapsın (Windows-only, OperatingSystem.IsWindows() ile
  sarmalı; P/Invoke):
  (a) Kendi PROCESS DACL'inden Interactive (S-1-5-4) için PROCESS_TERMINATE'i
      reddet → Task Manager "End task" engellenir (Access Denied).
  (b) Kendi SERVICE güvenlik tanımlayıcısına Interactive için SERVICE_STOP (SDDL
      'WP') reddi ekle (sc sdshow ile oku, deny ACE'yi DACL'in başına ekle,
      sc sdset ile yaz) → services.msc / sc stop engellenir. SYSTEM/SCM
      etkilenmesin ki düzgün durdurma ve recovery çalışsın.
- Service exe'ye bir `--unprotect` bakım modu ekle: bu modda host'u başlatmadan
  sadece SERVICE_STOP deny ACE'sini kaldırıp çıksın.
- MSI/installer'da: service'i SCM recovery (ilk/ikinci/sonraki hatada 5 sn'de
  restart) ile kur. Service'i KURULUM sırasında MSI'ın StartServices'iyle BAŞLATMA
  (Start="install" KOYMA) — başarısız olursa bastırılamayan Retry/Ignore diyaloğu
  çıkar. Bunun yerine kurulum sonrası deferred SYSTEM bir custom action ile
  `sc start` yap, Return="ignore"; Start="auto" da fallback olsun.
- KALDIRMA sırasında StopServices'ten ÖNCE, deferred SYSTEM bir custom action ile
  service exe'yi `--unprotect` çağır (Return="ignore") — yoksa SERVICE_STOP reddi
  kaldırmayı bozar.

EKLENECEK 2 — KALDIRMA PAROLASI:
- Uninstall sırasında master parola soran bir WiX DTF managed custom action ekle
  (.NET Framework 4.7.2 hedefli ayrı proje; DTF custom action'ları bunu gerektirir).
- Parolanın kendisi DEĞİL, SHA-256 hash'i kodda gömülü olsun. Yanlış parolada
  (Return="check" ile) uninstall iptal olsun. 3 yanlış denemede kaldırmayı durdur.
- Koşulu kaldırmayla sınırla (Installed AND REMOVE="ALL" AND upgrade değil) ki
  sürüm yükseltmesinde parola SORULMASIN.
- Sessiz (/qn) kaldırma parola sorulamayacağı için engellensin.
- Custom action DLL'i CustomAction.config ile gelsin (useLegacyV2RuntimeActivation
  Policy) yoksa CLR yüklenmez (1603 / 0x80131700).

MSI PAKETLEME (WiX ise) DİKKAT:
- Framework-dependent yayında dosyaları TEK TEK listeleme; <Files Include="...\**" />
  ile tüm publish klasörünü harvest et (açık Id'li bir ComponentGroup içinde,
  Feature'dan referansla). Yoksa NuGet bağımlılık DLL'leri eksik kalır ve uygulama/
  service FileNotFoundException ile çöker.
- İki ayrı publish çıktısını (app + service) AYNI klasöre koyma; ayrı alt klasörlere
  koy ki aynı adlı DLL'ler çakışmasın.

GENEL KURALLAR:
- WiX macOS/Linux'ta derlenmez; değişiklikleri CI (windows-latest) yeşil olana
  kadar doğrulanmış sayma.
- Bu DETERRENT (caydırıcı) bir korumadır, kernel düzeyi değildir; kararlı bir admin
  DACL/SDDL'i geri alabilir. README'de bunu ve korumayı elle kaldırma adımlarını
  dürüstçe belgele.
- Kod yorumları ve commit mesajları İngilizce; commit'ler Conventional Commits,
  Co-Authored-By trailer EKLEME. Commit/push/tag işlemini ben onaylamadan yapma.
- Her adımda önce mevcut yapıyı oku, sonra uyarla — bu projenin kendi
  konvansiyonlarına (TFM, klasör düzeni, isimlendirme) uy.
```

---

## Neden bu adımlar? (kısa gerekçeler)

Bu projede her madde gerçek bir sorundan doğdu:

| Madde | Neden |
|---|---|
| Service düz `net9.0` | `-windows` TFM'i Desktop runtime bağımlılığı ekler; SCM'de service çöker |
| `<Files>` ile harvest | Dosyaları elle saymak NuGet DLL'lerini atlar → `FileNotFoundException` |
| Ayrı alt klasör | İki publish çıktısının aynı adlı DLL'leri çakışır |
| `Start="install"` yok | MSI service başlamazsa bastırılamayan Retry/Ignore diyaloğu çıkarır |
| `--unprotect` (uninstall) | SERVICE_STOP reddi, kaldırmadaki StopServices'i de bloklar |
| `CustomAction.config` | Olmadan DTF custom action CLR'ı yükleyemez (1603) |
| ProgressText `Template` | `[1] [2]` metni Description'dan değil ActionText Template sütunundan gelir |
