# Adim 5 - Desktop uygulamasindan servisi durdurma/baslatma (master parola ile)

Masaustu (WPF) uygulamasina, korumali arka plan service'ini **durduran** (master
parola ister) ve **baslatan** (parola istemez) bir ozellik ekler.
**Adim 3'un (services.msc stop korumasi) uzerine** kurulur.

> **Puf noktasi:** Service, Interactive kullanicilara `SERVICE_STOP` reddi
> uyguladigi icin (Adim 3), WPF uygulamasi dogrudan `sc stop` YAPAMAZ. Bu yuzden
> durdurma, service'in kendi kendini durdurmasi yoluyla yapilir. Baslatma ise
> serbesttir (deny ACE yalnizca STOP'u engeller, START'i degil).

## Prompt

```text
Masaustu uygulamama, korumali arka plan service'ini durduran ve baslatan bir
ozellik eklemeni istiyorum.

ONCE KESFET (varsayim yapma - repo'yu tara, sonra uygula):
- Masaustu uygulamasinin tipi (WPF/WinForms) ve service'in adi/exe yolu. Masaustu
  uygulamasi YOKSA (or. yalniz service'li bir urun) bu adim uygulanamaz - bunu bana
  soyle.
- App ile service'in PAYLASTIGI ProgramData yolunu belirle (or. C:\ProgramData\<App>).
  Ikisi AYNI yolu kullanmali (IPC kontrol dosyasi icin); mevcut projede farkli bir
  ortak konum/Id varsa onu kullan.
- Uninstall parola hash'i (Adim 1) bu projede zaten var mi? Stop dogrulamasi ONUNLA
  AYNI hash'i kullanmali - ayri bir sabit URETME, mevcut olani paylas.
- Service Interactive kullanicilara SERVICE_STOP reddi uyguluyor mu (Adim 3)? Evetse
  asagidaki "kendi kendine durdurma" yaklasimi SART; aksi halde uygulama sc stop
  yapamaz.

DURDURMA (master parola ister):
- Uygulamada "Servisi durdur" butonu master parola sorsun. Parolayi, uninstall
  korumasiyla AYNI SHA-256 hash ile dogrula. Dogrulamayi TEK bir metoda koy
  (or. ValidatePassword) - ileride bir API cagrisina gecisi kolaylastirmak icin.
- Parola dogruysa, uygulama servise "dur" sinyali versin. Service SERVICE_STOP
  reddi uyguladigi icin uygulama dogrudan durduramaz; bunun yerine:
  - Uygulama, service'in izledigi ortak bir konuma (or.
    C:\ProgramData\<App>\stop.request) bir KONTROL DOSYASI yazsin.
  - Service (SYSTEM altinda) bu dosyayi kisa araliklarla (or. 2 sn) yoklar; gorunce
    kendi SERVICE_STOP deny ACE'sini kaldirir (Adim 3'teki AllowStop), dosyayi siler
    ve host'u temiz kapatir (IHostApplicationLifetime.StopApplication).
  - Temiz kapanis SCM recovery'yi TETIKLEMEZ (Windows bunu hata saymaz), yani
    service geri gelmez - istenen davranis budur.
- Uygulama, ServiceController ile servisin Stopped'a dusmesini kisa bir timeout ile
  beklesin ve sonucu kullaniciya gostersin.

BASLATMA (parola istemez):
- "Servisi baslat" butonu dogrudan ServiceController.Start() ile baslatsin.
- ONEMLI: Bir servisin VARSAYILAN DACL'i Interactive kullanicilara SERVICE_START
  vermez; bu yuzden normal kullanicinin ServiceController.Start() cagrisi
  "Cannot open '<service>' service" hatasi verir. Cozum: STOP korumasini eklerken
  (Adim 3) servis SDDL'ine Interactive icin bir ALLOW ACE de ekle:
  (A;;RPLCRC;;;IU) - RP=SERVICE_START, LC=QUERY_STATUS, RC=READ_CONTROL.
  Boylece kullanici UAC olmadan baslatabilir; WP (STOP) vermedigimiz icin durdurma
  yine engellidir. (Deny ACE'ler DACL'in basinda, bu allow ACE allow'lar arasinda.)
- Baslatmadan once varsa eski stop.request dosyasini sil (yoksa service acilir acilmaz
  tekrar durur). Service yeniden basladiginda STOP korumasini tekrar uygular (Adim 3).

NOTLAR:
- ServiceController, .NET'te ayri bir pakettedir:
  <PackageReference Include="System.ServiceProcess.ServiceController" />
- Parola dogrulama ileride API'ye tasinacak: tek metotta tut, hash'i oradan cikar.
- Bu mekanizma UAC/yukseltme gerektirmez; named pipe karmasikligina da gerek yoktur.

KURALLAR:
- Kod yorumlari ve commit'ler Ingilizce; Conventional Commits; Co-Authored-By EKLEME.
- Commit/push/tag islemini ben onaylamadan yapma.
- WiX/WPF macOS'ta derlenmez; CI (windows-latest) yesil olmadan dogrulanmis sayma.
```

## Referans dosyalar (bu projede)

- `MyApp/ServiceControlClient.cs` - parola dogrulama + stop.request yazma + start
- `MyApp/PasswordPrompt.cs` - WPF parola penceresi
- `MyApp/MainWindow.xaml(.cs)` - Stop/Start butonlari ve durum metni
- `MyApp.Service/AgentWorker.cs` - `stop.request`'i yoklayan dongu + self-stop
- `MyApp.Service/ServiceProtection.cs` - `AllowStop` (deny ACE'yi kaldirma)

## Neden bu detaylar?

| Detay | Neden |
| --- | --- |
| Kontrol dosyasi ile self-stop | WPF, SERVICE_STOP reddi yuzunden dogrudan durduramaz |
| Service kendi AllowStop'unu cagirir | Yalnizca SYSTEM olan service kendi SDDL'ini degistirebilir |
| Temiz kapanis | SCM recovery'yi tetiklemez -> service istenmeden geri gelmez |
| Start parola istemez | STOP'u (`WP`) reddederiz, START'a (`RP`) izin veririz |
| START icin allow ACE sart | Varsayilan servis DACL'i Interactive'e START vermez -> "Cannot open service" |
| Start'tan once stop.request sil | Bayat istek dosyasi servisi aninda tekrar durdurur |
| Dogrulamayi tek metotta tut | Ileride API'ye gecisi tek noktadan yapmak icin |
