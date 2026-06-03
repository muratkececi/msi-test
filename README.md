# MyApp — Parola Korumalı Uninstall'a Sahip MSI Örneği

Basit bir WPF uygulaması ve onu kuran bir MSI. **Kullanıcı uygulamayı
kaldırmak istediğinde master parola sorulur**; yanlışsa kaldırma iptal edilir.

## Proje yapısı

```
msi-test/
├── MyApp/            → WPF uygulaması (.NET 8)
├── UninstallGuard/   → Parola soran WiX custom action (.NET Framework 4.7.2)
└── Installer/        → WiX 5 MSI projesi
```

## Master parola

- Parola: `Admin123!`
- Kodda parolanın kendisi DEĞİL, SHA-256 hash'i gömülüdür
  (`UninstallGuard/CustomActions.cs` içindeki `MasterPasswordHash`).
- Değiştirmek için yeni hash üretin (PowerShell):

  ```powershell
  $p = "YeniParola"
  [BitConverter]::ToString(
    [System.Security.Cryptography.SHA256]::Create().ComputeHash(
      [Text.Encoding]::UTF8.GetBytes($p))
  ).Replace("-", "")
  ```

## Önkoşullar (Windows)

1. **.NET 8 SDK** — https://dotnet.microsoft.com/download
2. **WiX araçları, dotnet'in bir parçası olarak NuGet'ten gelir.**
   Ek olarak global WiX CLI'ı kurmak isterseniz:
   ```powershell
   dotnet tool install --global wix --version 5.0.2
   ```
   (Bu projede `WixToolset.Sdk` PackageReference olduğundan global kurulum şart değildir.)

## Derleme (Windows)

```powershell
# Kök dizinde
dotnet build MyApp\MyApp.csproj -c Release
dotnet build UninstallGuard\UninstallGuard.csproj -c Release
dotnet build Installer\Installer.wixproj -c Release
```

MSI çıktısı:
```
Installer\bin\Release\MyAppSetup.msi
```

## Test

```powershell
# Kurulum (çift tık da olur)
msiexec /i Installer\bin\Release\MyAppSetup.msi

# Kaldırma — burada PAROLA EKRANI çıkar:
msiexec /x Installer\bin\Release\MyAppSetup.msi
# veya Denetim Masası > Programlar ve Özellikler > MyApp > Kaldır
```

## Güvenlik notu (önemli)

- Bu koruma **normal kullanıcıyı** (Denetim Masası / çift tık / `msiexec /x`)
  durdurur.
- Custom action sessiz mod (`/qn`) algılarsa kaldırmayı **engeller**
  (`ActionResult.Failure`). Davranışı `CustomActions.cs` içinden değiştirebilirsiniz.
- %100 garanti değildir: MSI'ı zorla kaldırmanın (ör. dosyaları elle silme,
  registry düzenleme) yolları vardır. Gerçek güvenlik için parola doğrulamasını
  bir sunucu API'sine taşıyın ve hassas veriyi sunucuda tutun.

## Üretim için: API ile doğrulama

`CustomActions.cs` içindeki `Verify()` yerine bir HTTP çağrısı koyun:

```csharp
private static bool Verify(string password)
{
    using (var client = new System.Net.Http.HttpClient())
    {
        var resp = client.PostAsync(
            "https://api.icredible.com/uninstall/validate",
            new System.Net.Http.StringContent(password)).Result;
        return resp.IsSuccessStatusCode;
    }
}
```
