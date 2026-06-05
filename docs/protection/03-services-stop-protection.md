# Adım 3 — services.msc / `sc stop` ile durdurmayı engelleme (service SDDL)

Interactive kullanıcı/admin'in service'i `services.msc` veya `sc stop` ile
durdurmasını engeller. **Adım 2'nin (background service) üzerine** kurulur.

> **Önemli:** Bu adım kaldırmayı bozabilir; bu yüzden uninstall sırasında korumayı
> kaldıran bir mekanizma da ekleniyor. Bu detay zorunludur, atlama.

## Prompt

```text
Bu projedeki arka plan service'ine, Interactive kullanıcıların services.msc /
sc stop ile DURDURMASINI engelleyen bir koruma eklemeni istiyorum. (Task Manager
koruması ayrı; bu services.msc tarafı için.)

ÖNCE KEŞFET (varsayım yapma — repo'yu tara, sonra uygula):
- Background service ve onu kuran installer mevcut mu (Adım 2 yapılmış olmalı).
  Service'in adını ve exe yolunu çıkar.
- Installer WiX mi? DEĞİLSE, "uninstall sırasında --unprotect çağıran custom action"
  kısmını oraya nasıl uyarlayacağını (kaldırma scriptine bir pre-stop adımı) bana
  özetle, ONAY AL. Service-SDDL koruması (kod tarafı) installer'dan bağımsızdır.
- Installer'ın bitness'ini çıkar (CA binary adları x64/x86'ya göre değişir).

EKLE — SERVICE SDDL KORUMASI:
- Service başlarken (SYSTEM altında), kendi SERVICE güvenlik tanımlayıcısına,
  Interactive (IU = S-1-5-4) için SERVICE_STOP (SDDL hak harfi 'WP', 0x0020) iznini
  REDDEDEN bir ACE ekle.
- Yöntem: `sc sdshow <ServiceName>` ile mevcut SDDL'i OKU, deny ACE'yi "(D;;WP;;;IU)"
  DACL'in (D: bölümünün) EN BAŞINA ekle (deny ACE'ler allow ACE'lerden önce gelmeli),
  sonra `sc sdset <ServiceName> <yeniSDDL>` ile yaz. Tüm SDDL'i hardcode ETME; canlı
  descriptor'ı okuyup eklemek SYSTEM/Admin/SCM haklarını korur.
- Zaten ekliyse tekrar ekleme (idempotent). sc sdset çıktısında "SUCCESS" yoksa hata say.
- Sonuç: normal/admin kullanıcı services.msc'den "Durdur" diyemez. SYSTEM ve SCM
  etkilenmez → düzgün durdurma ve recovery çalışmaya devam eder, START engellenmez.
- Bu kodu yalnızca Windows'ta çalıştır (OperatingSystem.IsWindows()).

KALDIRMA GÜVENLİĞİ (zorunlu — yoksa uninstall bozulur):
- Service exe'ye bir "--unprotect" bakım modu ekle: bu argümanla çalıştırıldığında
  HOST'u başlatmadan SADECE SERVICE_STOP deny ACE'sini kaldırıp çıksın (sc sdshow oku,
  "(D;;WP;;;IU)" parçasını çıkar, sc sdset ile yaz; best-effort, hata yutulsun).
- MSI'da KALDIRMA sırasında, StopServices'ten ÖNCE çalışan deferred + Impersonate="no"
  (SYSTEM) bir custom action ekle: service exe'yi "--unprotect" ile çağırsın,
  Return="ignore", koşul REMOVE="ALL". Aksi halde STOP reddi MSI'ın service'i
  durdurmasını da bloklar ve kaldırma takılır.

README'YE EKLE — korumayı elle kaldırma yolları (yönetici):
1) MSI'ı kaldır/yeniden kur (otomatik temizlenir / sıfırlanır).
2) Bakım modu: "<kurulum yolu>\MyApp.Service.exe" --unprotect
3) Elle: sc sdshow <Name> → çıktıdaki (D;;WP;;;IU)'yu çıkar → sc sdset <Name> "<temiz SDDL>"

KURALLAR:
- Kod yorumları ve commit'ler İngilizce; Conventional Commits; Co-Authored-By EKLEME.
- Commit/push/tag işlemini ben onaylamadan yapma.
- WiX macOS'ta derlenmez; CI yeşil olmadan doğrulanmış sayma.
- DİKKAT: yanlış service SDDL servisi yönetilemez hale getirebilir; SY (SYSTEM) ve
  BA (Administrators) allow ACE'lerini ASLA kaldırma. Mutlaka test et.
```

## Referans dosyalar (bu projede)

- `MyApp.Service/ServiceProtection.cs` — `DenyInteractiveStop` + `AllowStop` (sc sdshow/sdset)
- `MyApp.Service/Program.cs` — `--unprotect` bakım modu
- `MyApp.Service/AgentWorker.cs` — başlangıçta `DenyInteractiveStop` çağrısı
- `Installer/Package.wxs` — `UnprotectAgentService` CA, StopServices'ten önce sequence

## Neden bu detaylar?

| Detay | Neden |
| --- | --- |
| SERVICE_STOP = `WP` | services.msc "Durdur"u engelleyen tam SDDL hakkı budur |
| Deny ACE'yi başa ekle | SDDL'de deny ACE'ler allow'lardan önce gelmeli |
| Canlı SDDL'i oku, ekle | SYSTEM/Admin/SCM haklarını silmemek için (hardcode riskli) |
| `--unprotect` + uninstall CA | STOP reddi, kaldırmadaki StopServices'i de bloklar |
| SY/BA allow'larına dokunma | Aksi halde service tamamen yönetilemez hale gelir |
