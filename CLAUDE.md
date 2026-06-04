# CLAUDE.md

Guidance for working in this repository.

## Commit conventions

- Write commit messages in **English**.
- Follow **Conventional Commits**: `feat:`, `fix:`, `docs:`, `refactor:`, `chore:`, `build:`, `ci:` …
- Use imperative mood ("add", "fix", not "added"/"fixes").
- **Do NOT add a `Co-Authored-By:` trailer.**
- Commit or push only when asked. If on `main`, branch first unless told otherwise.

## What this project is

A sample WPF app shipped via a WiX 5 MSI, with two protection layers:

1. **Uninstall protection** — a master-password prompt blocks unauthorized uninstalls.
2. **Stop protection** — a Windows service runs as SYSTEM and resists Task Manager
   "End task" (process DACL denies `PROCESS_TERMINATE` to Interactive users); SCM
   recovery restarts it if killed.

> A password CANNOT be prompted on Task Manager "End task" — the kernel calls
> `TerminateProcess` and no app code runs. Passwords only gate paths the app
> controls (uninstall, an in-app exit). The service approach is a deterrent, not
> kernel-level protection; a determined admin can still revert the DACL.

## Layout

| Project | TFM | Role |
|---|---|---|
| `MyApp/` | `net9.0-windows` | WPF app (framework-dependent) |
| `MyApp.Service/` | `net9.0` | Worker service "MyAppAgent"; process-kill protection (`ProcessProtection.cs`) |
| `UninstallGuard/` | `net472` | WiX DTF managed custom action; password prompt (`CustomActions.cs`) |
| `Installer/` | WiX 5 (`WixToolset.Sdk/5.0.2`) | MSI that packages everything (`Package.wxs`) |

- Target framework is **.NET 9** across managed projects. `MyApp.Service` is plain
  `net9.0` (not `-windows`) on purpose: `-windows` adds a `Microsoft.WindowsDesktop.App`
  dependency that makes the service crash on SCM start if the Desktop runtime is absent.
- `UninstallGuard` stays on `.NET Framework 4.7.2` — WiX DTF custom actions require it.

## Build

Exes are packaged from the **publish** output, so `MyApp` and `MyApp.Service` must be
`dotnet publish`-ed (not just built) before the installer:

```powershell
dotnet publish MyApp\MyApp.csproj -c Release
dotnet publish MyApp.Service\MyApp.Service.csproj -c Release
dotnet build   UninstallGuard\UninstallGuard.csproj -c Release
dotnet build   Installer\Installer.wixproj -c Release    # -> Installer\bin\Release\MyAppSetup.msi
```

CI (`.github/workflows/build-msi.yml`) does this on `windows-latest` with the `9.0.x`
SDK. A `v*` tag push also publishes a GitHub Release with the MSI attached.

## Releasing a new version

Keep the version in sync in all three places, then tag:

- `MyApp/MyApp.csproj` → `<Version>`
- `MyApp.Service/MyApp.Service.csproj` → `<Version>`
- `Installer/Package.wxs` → `Package Version`

```bash
git tag vX.Y.Z && git push origin vX.Y.Z   # triggers the Release
```

## MSI specifics (gotchas)

- The service is **not** started by MSI's `StartServices` (no `Start="install"`).
  MSI always shows a "Service failed to start … Retry/Ignore" dialog on failure and
  `Wait="no"` does NOT suppress it. Instead, a deferred SYSTEM custom action
  (`StartAgentService`, `Return="ignore"`) runs `sc start` after `InstallServices`;
  `Start="auto"` is the fallback at next boot.
- Custom action DLL is `UninstallGuard.CA.dll`; it needs `CustomAction.config`
  (`useLegacyV2RuntimeActivationPolicy`) or it fails to load the CLR (1603 / 0x80131700).
- `Package.wxs` uses `Codepage="65001"` (UTF-8) for Turkish characters in messages.
- `InstallerPlatform=x64` is required (components live under `ProgramFiles64Folder`).

## Docs

- `README.md` — overview, build, security notes.
- `TESTING.md` — manual test checklist (install, both protections, uninstall, upgrade).
  Keep both in sync when behavior or versions change.
