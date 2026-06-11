# Adim 3 - services.msc / `sc stop` ile durdurmayi engelleme (service SDDL)

Interactive kullanici/admin'in service'i `services.msc` veya `sc stop` ile
durdurmasini engeller. **Adim 2'nin (background service) uzerine** kurulur.

> **Onemli:** Bu adim kaldirmayi bozabilir; bu yuzden uninstall sirasinda korumayi
> kaldiran bir mekanizma da ekleniyor. Bu detay zorunludur, atlama.

## Prompt

```text
Bu projedeki arka plan service'ine, Interactive kullanicilarin services.msc /
sc stop ile DURDURMASINI engelleyen bir koruma eklemeni istiyorum. (Task Manager
korumasi ayri; bu services.msc tarafi icin.)

ONCE KESFET (varsayim yapma - repo'yu tara, sonra uygula):
- Background service ve onu kuran installer mevcut mu (Adim 2 yapilmis olmali).
  Service'in adini ve exe yolunu cikar.
- Installer WiX mi? DEGILSE, "uninstall sirasinda --unprotect cagiran custom action"
  kismini oraya nasil uyarlayacagini (kaldirma scriptine bir pre-stop adimi) bana
  ozetle, ONAY AL. Service-SDDL korumasi (kod tarafi) installer'dan bagimsizdir.
- Installer'in bitness'ini cikar (CA binary adlari x64/x86'ya gore degisir).

EKLE - SERVICE SDDL KORUMASI:
- Service baslarken (SYSTEM altinda), kendi SERVICE guvenlik tanimlayicisina,
  Interactive (IU = S-1-5-4) icin SERVICE_STOP (SDDL hak harfi 'WP', 0x0020) iznini
  REDDEDEN bir ACE ekle.
- Yontem: `sc sdshow <ServiceName>` ile mevcut SDDL'i OKU, deny ACE'yi "(D;;WP;;;IU)"
  DACL'in (D: bolumunun) EN BASINA ekle (deny ACE'ler allow ACE'lerden once gelmeli),
  sonra `sc sdset <ServiceName> <yeniSDDL>` ile yaz. Tum SDDL'i hardcode ETME; canli
  descriptor'i okuyup eklemek SYSTEM/Admin/SCM haklarini korur.
- Zaten ekliyse tekrar ekleme (idempotent). sc sdset ciktisinda "SUCCESS" yoksa hata say.
- Sonuc: normal/admin kullanici services.msc'den "Durdur" diyemez. SYSTEM ve SCM
  etkilenmez -> duzgun durdurma ve recovery calismaya devam eder, START engellenmez.
- Bu kodu yalnizca Windows'ta calistir (OperatingSystem.IsWindows()).

KALDIRMA GUVENLIGI (zorunlu - yoksa uninstall bozulur):
- Service exe'ye bir "--unprotect" bakim modu ekle: bu argumanla calistirildiginda
  HOST'u baslatmadan SADECE SERVICE_STOP deny ACE'sini kaldirip ciksin (sc sdshow oku,
  "(D;;WP;;;IU)" parcasini cikar, sc sdset ile yaz; best-effort, hata yutulsun).
- MSI'da KALDIRMA sirasinda, StopServices'ten ONCE calisan deferred + Impersonate="no"
  (SYSTEM) bir custom action ekle: service exe'yi "--unprotect" ile cagirsin,
  Return="ignore", kosul REMOVE="ALL". Aksi halde STOP reddi MSI'in service'i
  durdurmasini da bloklar ve kaldirma takilir.

README'YE EKLE - korumayi elle kaldirma yollari (yonetici):
1) MSI'i kaldir/yeniden kur (otomatik temizlenir / sifirlanir).
2) Bakim modu: "<kurulum yolu>\MyApp.Service.exe" --unprotect
3) Elle: sc sdshow <Name> -> ciktidaki (D;;WP;;;IU)'yu cikar -> sc sdset <Name> "<temiz SDDL>"

KURALLAR:
- Kod yorumlari ve commit'ler Ingilizce; Conventional Commits; Co-Authored-By EKLEME.
- Commit/push/tag islemini ben onaylamadan yapma.
- WiX macOS'ta derlenmez; CI yesil olmadan dogrulanmis sayma.
- DIKKAT: yanlis service SDDL servisi yonetilemez hale getirebilir; SY (SYSTEM) ve
  BA (Administrators) allow ACE'lerini ASLA kaldirma. Mutlaka test et.
```

## Referans dosyalar (bu projede)

- `MyApp.Service/ServiceProtection.cs` - `DenyInteractiveStop` + `AllowStop` (sc sdshow/sdset)
- `MyApp.Service/Program.cs` - `--unprotect` bakim modu
- `MyApp.Service/AgentWorker.cs` - baslangicta `DenyInteractiveStop` cagrisi
- `Installer/Package.wxs` - `UnprotectAgentService` CA, StopServices'ten once sequence

## Neden bu detaylar?

| Detay | Neden |
| --- | --- |
| SERVICE_STOP = `WP` | services.msc "Durdur"u engelleyen tam SDDL hakki budur |
| Deny ACE'yi basa ekle | SDDL'de deny ACE'ler allow'lardan once gelmeli |
| Canli SDDL'i oku, ekle | SYSTEM/Admin/SCM haklarini silmemek icin (hardcode riskli) |
| `--unprotect` + uninstall CA | STOP reddi, kaldirmadaki StopServices'i de bloklar |
| SY/BA allow'larina dokunma | Aksi halde service tamamen yonetilemez hale gelir |
