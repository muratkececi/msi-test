# Adim 7 - Uygulama klasorlerinin silinmesini engelleme (folder DACL)

Interactive kullanici/admin'in kurulum klasorunu (`C:\Program Files\<App>`) ve
veri klasorunu (`C:\ProgramData\<App>`) Gezgin "Sil" / `del` / `rmdir` ile
silmesini engeller. **Adim 2'nin (background service) uzerine** kurulur; service
SYSTEM altinda calisir ve klasorlere deny-DELETE ACE'i o ekler.

> **Onemli:** Bu, silmeye karsi **caydirici** bir korumadir, kernel duzeyi degil.
> Kararli bir admin sahipligi (ownership) alip ACL'i geri alabilir ve silebilir -
> tipki service/process DACL korumalari gibi. README'de durustce belirt.

> **Uninstall'i bozar mi?** Hayir - ACE yalnizca **Interactive** kullaniciyi (IU =
> S-1-5-4) hedef alir, SYSTEM'i degil. MSI'in `RemoveFiles`/`RemoveFolders`
> aksiyonlari elevated kurulumda SYSTEM olarak calisir, deny-DELETE'ten
> etkilenmez. Yine de guvenlik agi olarak uninstall sirasinda ACE'ler kaldirilir.

## Prompt

```text
Bu projeye, kurulum klasorunun (C:\Program Files\<App>) ve veri klasorunun
(C:\ProgramData\<App>) Interactive kullanicilar tarafindan silinmesini engelleyen
bir koruma eklemeni istiyorum. (Background service ve onu kuran installer mevcut -
Adim 2/3 yapilmis olmali.)

ONCE KESFET (varsayim yapma - repo'yu tara, sonra uygula):
- Background service var mi, adi/exe yolu ne, hangi klasorden calisiyor? Kurulum
  klasorunu exe konumundan TURET (hard-code etme); or. service bir "Agent" alt
  klasorundeyse kurulum koku onun ustudur.
- Uygulamanin ProgramData yolu (or. C:\ProgramData\<App>) - service zaten log/IPC
  icin kullaniyor olabilir; aynisini kullan.
- Installer WiX mi? DEGILSE, uninstall-time ACE temizligini oraya nasil
  uyarlayacagini (kaldirma scriptine pre-remove adimi) ozetle, ONAY AL.

EKLE - FOLDER DELETE KORUMASI:
- Service baslarken (SYSTEM altinda), her iki klasore Interactive (IU = S-1-5-4)
  icin DELETE (DE) + DELETE_CHILD (DC) REDDEDEN bir ACE ekle. icacls KULLAN
  (mevcut ACL'i korur, sadece deny ACE ekler - tum ACL'i hard-code ETME):
    icacls "<path>" /deny *S-1-5-4:(OI)(CI)(DE,DC)
  (OI)(CI) = klasor + alt ogeler icin kalitim. Idempotent (tekrar uygulanabilir).
- Kaldirma icin ters islem:
    icacls "<path>" /remove:d *S-1-5-4
  /remove:d yalnizca DENY ACE'leri kaldirir; DOSYALARI SILMEZ.
- Bu kodu yalnizca Windows'ta calistir (OperatingSystem.IsWindows() + sinifa
  [SupportedOSPlatform("windows")]).

KALDIRMA GUVENLIGI (zorunlu):
- Service exe'nin "--unprotect" bakim moduna (Adim 3'te eklendi) folder-unprotect'i
  de ekle: SERVICE_STOP deny'ini kaldirirken AYNI ANDA her iki klasorun deny-DELETE
  ACE'ini de kaldir. SADECE ACE kaldirilsin; klasor/dosyalar (loglar) DISKTE KALSIN.
- MSI'da bu zaten uninstall sirasinda StopServices'ten ONCE calisan deferred SYSTEM
  custom action ile cagriliyor (Adim 3). StopServices, RemoveFiles'tan once geldigi
  icin ACE'ler kurulum klasoru silinmeden kaldirilmis olur - ek bir sequence
  GEREKMEZ. Yalnizca ayni --unprotect cagrisinin folder ACE'lerini de temizledigini
  dogrula.

DAVRANIS (istenen):
- Uygulama KURULUYKEN: kullanici her iki klasoru Gezgin'den silmeye calisinca
  "Access Denied" alir.
- UNINSTALL'da: ACE'ler otomatik kalkar; MSI Program Files'i siler; ProgramData
  klasoru ve loglari silinmeden DISKTE KALIR (MSI ona dokunmaz).

KURALLAR:
- Kod yorumlari ve commit'ler Ingilizce; Conventional Commits; Co-Authored-By EKLEME.
- Commit/push/tag islemini ben onaylamadan yapma.
- WiX macOS'ta derlenmez; CI (windows-latest) yesil olmadan dogrulanmis sayma.
- Bu DETERRENT bir korumadir; admin ownership alip geri alabilir. README'de belirt.
```

## Referans dosyalar (bu projede)

- `MyApp.Service/FolderProtection.cs` - `DenyDelete` / `AllowDelete` (icacls)
- `MyApp.Service/AgentWorker.cs` - baslangicta her iki klasoru koruma
- `MyApp.Service/Program.cs` - `--unprotect` moduna folder-unprotect eklenmesi
- `Installer/Package.wxs` - `UnprotectAgentService` CA (StopServices'ten once,
  ek sequence gerekmez)

## Neden bu detaylar?

| Detay | Neden |
| --- | --- |
| Interactive (S-1-5-4) hedefle | SYSTEM etkilenmez -> MSI RemoveFiles silebilir, uninstall bozulmaz |
| `DE` + `DC` reddet | DE klasorun kendisini, DC icindeki ogeleri silmeyi engeller |
| icacls `/deny` (full ACL degil) | Mevcut izinleri korur; hard-code ACL SYSTEM/Admin'i kilitleyebilir |
| `(OI)(CI)` kalitim | Klasor + alt ogeler icin gecerli olsun |
| `--unprotect` ACE'i kaldirir, dosyayi degil | Uninstall sonrasi loglar/ProgramData diskte kalsin |
| Ek sequence yok | --unprotect zaten StopServices (-> RemoveFiles) oncesi calisiyor |
| Kurulum kokunu exe'den turet | Ozel kurulum yolu da calissin; hard-code yol kirilir |
