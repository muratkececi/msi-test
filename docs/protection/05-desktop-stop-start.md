# Adım 5 — Desktop uygulamasından servisi durdurma/başlatma (master parola ile)

Masaüstü (WPF) uygulamasına, korumalı arka plan service'ini **durduran** (master
parola ister) ve **başlatan** (parola istemez) bir özellik ekler.
**Adım 3'ün (services.msc stop koruması) üzerine** kurulur.

> **Püf noktası:** Service, Interactive kullanıcılara `SERVICE_STOP` reddi
> uyguladığı için (Adım 3), WPF uygulaması doğrudan `sc stop` YAPAMAZ. Bu yüzden
> durdurma, service'in kendi kendini durdurması yoluyla yapılır. Başlatma ise
> serbesttir (deny ACE yalnızca STOP'u engeller, START'ı değil).

## Prompt

```text
Masaüstü uygulamama, korumalı arka plan service'ini durduran ve başlatan bir
özellik eklemeni istiyorum.

ÖNCE KEŞFET (varsayım yapma — repo'yu tara, sonra uygula):
- Masaüstü uygulamasının tipi (WPF/WinForms) ve service'in adı/exe yolu. Masaüstü
  uygulaması YOKSA (ör. yalnız service'li bir ürün) bu adım uygulanamaz — bunu bana
  söyle.
- App ile service'in PAYLAŞTIĞI ProgramData yolunu belirle (ör. C:\ProgramData\<App>).
  İkisi AYNI yolu kullanmalı (IPC kontrol dosyası için); mevcut projede farklı bir
  ortak konum/Id varsa onu kullan.
- Uninstall parola hash'i (Adım 1) bu projede zaten var mı? Stop doğrulaması ONUNLA
  AYNI hash'i kullanmalı — ayrı bir sabit ÜRETME, mevcut olanı paylaş.
- Service Interactive kullanıcılara SERVICE_STOP reddi uyguluyor mu (Adım 3)? Evetse
  aşağıdaki "kendi kendine durdurma" yaklaşımı ŞART; aksi halde uygulama sc stop
  yapamaz.

DURDURMA (master parola ister):
- Uygulamada "Servisi durdur" butonu master parola sorsun. Parolayı, uninstall
  korumasıyla AYNI SHA-256 hash ile doğrula. Doğrulamayı TEK bir metoda koy
  (ör. ValidatePassword) — ileride bir API çağrısına geçişi kolaylaştırmak için.
- Parola doğruysa, uygulama servise "dur" sinyali versin. Service SERVICE_STOP
  reddi uyguladığı için uygulama doğrudan durduramaz; bunun yerine:
  - Uygulama, service'in izlediği ortak bir konuma (ör.
    C:\ProgramData\<App>\stop.request) bir KONTROL DOSYASI yazsın.
  - Service (SYSTEM altında) bu dosyayı kısa aralıklarla (ör. 2 sn) yoklar; görünce
    kendi SERVICE_STOP deny ACE'sini kaldırır (Adım 3'teki AllowStop), dosyayı siler
    ve host'u temiz kapatır (IHostApplicationLifetime.StopApplication).
  - Temiz kapanış SCM recovery'yi TETİKLEMEZ (Windows bunu hata saymaz), yani
    service geri gelmez — istenen davranış budur.
- Uygulama, ServiceController ile servisin Stopped'a düşmesini kısa bir timeout ile
  beklesin ve sonucu kullanıcıya göstersin.

BAŞLATMA (parola istemez):
- "Servisi başlat" butonu doğrudan ServiceController.Start() ile başlatsın.
- ÖNEMLİ: Bir servisin VARSAYILAN DACL'i Interactive kullanıcılara SERVICE_START
  vermez; bu yüzden normal kullanıcının ServiceController.Start() çağrısı
  "Cannot open '<service>' service" hatası verir. Çözüm: STOP korumasını eklerken
  (Adım 3) servis SDDL'ine Interactive için bir ALLOW ACE de ekle:
  (A;;RPLCRC;;;IU) — RP=SERVICE_START, LC=QUERY_STATUS, RC=READ_CONTROL.
  Böylece kullanıcı UAC olmadan başlatabilir; WP (STOP) vermediğimiz için durdurma
  yine engellidir. (Deny ACE'ler DACL'in başında, bu allow ACE allow'lar arasında.)
- Başlatmadan önce varsa eski stop.request dosyasını sil (yoksa service açılır açılmaz
  tekrar durur). Service yeniden başladığında STOP korumasını tekrar uygular (Adım 3).

NOTLAR:
- ServiceController, .NET'te ayrı bir pakettedir:
  <PackageReference Include="System.ServiceProcess.ServiceController" />
- Parola doğrulama ileride API'ye taşınacak: tek metotta tut, hash'i oradan çıkar.
- Bu mekanizma UAC/yükseltme gerektirmez; named pipe karmaşıklığına da gerek yoktur.

KURALLAR:
- Kod yorumları ve commit'ler İngilizce; Conventional Commits; Co-Authored-By EKLEME.
- Commit/push/tag işlemini ben onaylamadan yapma.
- WiX/WPF macOS'ta derlenmez; CI (windows-latest) yeşil olmadan doğrulanmış sayma.
```

## Referans dosyalar (bu projede)

- `MyApp/ServiceControlClient.cs` — parola doğrulama + stop.request yazma + start
- `MyApp/PasswordPrompt.cs` — WPF parola penceresi
- `MyApp/MainWindow.xaml(.cs)` — Stop/Start butonları ve durum metni
- `MyApp.Service/AgentWorker.cs` — `stop.request`'i yoklayan döngü + self-stop
- `MyApp.Service/ServiceProtection.cs` — `AllowStop` (deny ACE'yi kaldırma)

## Neden bu detaylar?

| Detay | Neden |
| --- | --- |
| Kontrol dosyası ile self-stop | WPF, SERVICE_STOP reddi yüzünden doğrudan durduramaz |
| Service kendi AllowStop'unu çağırır | Yalnızca SYSTEM olan service kendi SDDL'ini değiştirebilir |
| Temiz kapanış | SCM recovery'yi tetiklemez → service istenmeden geri gelmez |
| Start parola istemez | STOP'u (`WP`) reddederiz, START'a (`RP`) izin veririz |
| START için allow ACE şart | Varsayılan servis DACL'i Interactive'e START vermez → "Cannot open service" |
| Start'tan önce stop.request sil | Bayat istek dosyası servisi anında tekrar durdurur |
| Doğrulamayı tek metotta tut | İleride API'ye geçişi tek noktadan yapmak için |
