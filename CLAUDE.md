# CLAUDE.md

Guidance for working in this repository.

## Commit conventions

- Write commit messages **and code comments** in **English**.
- Follow **Conventional Commits**: `feat:`, `fix:`, `docs:`, `refactor:`, `chore:`, `build:`, `ci:` …
- Use imperative mood ("add", "fix", not "added"/"fixes").
- **Do NOT add a `Co-Authored-By:` trailer.**
- Commit or push only when asked. If on `main`, branch first unless told otherwise.
- Write everything in **English** — comments, commits, AND user-facing strings
  (MSI dialog/error messages, log lines).

## What this project is

A sample WPF app shipped via a WiX 5 MSI, with two protection layers:

1. **Uninstall protection** — a master-password prompt blocks unauthorized uninstalls.
2. **Stop protection** — a Windows service runs as SYSTEM and resists being stopped:
   - Task Manager "End task" → process DACL denies `PROCESS_TERMINATE` to Interactive
     users (`ProcessProtection.cs`); SCM recovery restarts it if force-killed.
   - services.msc / `sc stop` → the service DACL denies `SERVICE_STOP` (SDDL `WP`) to
     Interactive users (`ServiceProtection.cs`). A `--unprotect` maintenance mode and an
     uninstall-time custom action strip that ACE so removal is never blocked.
3. **App-controlled stop/start** — the WPF app stops the service after a master-password
   check (same hash as uninstall). Since the app can't `sc stop` a protected service, it
   drops a `stop.request` control file under `ProgramData`; the SYSTEM service polls for
   it, lifts its own deny ACE, and self-stops (`ServiceControlClient.cs` ↔ `AgentWorker`).
   Start is not password-gated, but a service's default DACL denies SERVICE_START to
   interactive users, so `ServiceProtection` also adds an allow ACE `(A;;RPLCRC;;;IU)`
   (START/QUERY) — without it `ServiceController.Start()` fails with "Cannot open service".

> A password CANNOT be prompted on Task Manager "End task" — the kernel calls
> `TerminateProcess` and no app code runs. Passwords only gate paths the app
> controls (uninstall, an in-app exit). The service approach is a deterrent, not
> kernel-level protection; a determined admin can still revert the DACL.

## Layout

| Project | TFM | Role |
|---|---|---|
| `MyApp/` | `net9.0-windows` | WPF app (framework-dependent); stop/start the service after a password check (`ServiceControlClient.cs`) |
| `MyApp.Service/` | `net9.0` | Worker service "MyAppAgent"; kill protection (`ProcessProtection.cs`) + stop protection (`ServiceProtection.cs`) |
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

- **Harvest the whole publish folder with `<Files>` — never list files by hand.**
  A framework-dependent .NET publish drops many NuGet dependency DLLs (e.g.
  `Microsoft.Extensions.Hosting.dll`) next to the exe. Listing files individually
  silently omits them, and the app/service then crashes at startup with
  `FileNotFoundException`. `Package.wxs` uses `<Files Include="...\**" />` inside an
  explicitly-Id'd `<ComponentGroup>` (referenced from the `Feature`), so deps are
  always complete. The WPF app and the service publish into **separate folders**
  (`INSTALLFOLDER` vs `INSTALLFOLDER\Agent`) to avoid same-named-DLL collisions.
- `<ProgressText>` overrides the built-in service actions
  (`InstallServices`/`StartServices`/`StopServices`/`DeleteServices`) so the raw
  `Service: [1] [2]` placeholder text does not leak onto the progress dialog. Note:
  the message goes in the **`Message` attribute**, not as inner text (WiX rejects inner text).
- The service is **not** started by MSI's `StartServices` (no `Start="install"`).
  MSI always shows a "Service failed to start … Retry/Ignore" dialog on failure and
  `Wait="no"` does NOT suppress it. Instead, a deferred SYSTEM custom action
  (`StartAgentService`, `Return="ignore"`) runs `sc start` after `InstallServices`;
  `Start="auto"` is the fallback at next boot.
- Custom action DLL is `UninstallGuard.CA.dll`; it needs `CustomAction.config`
  (`useLegacyV2RuntimeActivationPolicy`) or it fails to load the CLR (1603 / 0x80131700).
- `Package.wxs` uses `Codepage="65001"` (UTF-8) for message text.
- `InstallerPlatform=x64` is required (components live under `ProgramFiles64Folder`).
- Launch-after-install uses WixUtil `WixShellExec` with `Impersonate="yes"` so the
  app runs as the user, not the elevated installer. `WixShellExecTarget` is set to
  `[INSTALLFOLDER]MyApp.exe` (a path, not `[#FileId]`) because the files are harvested
  with `<Files>` and have no hand-authored File Id.
- The leaking `File: [1]` on the install/repair progress line comes from **PrepareDlg's
  ActionData Text control** (subscribed to the ActionData event), NOT the ActionText
  table and NOT ProgressDlg — verified by dumping the MSI's EventMapping table with
  msitools. ActionText overrides removed `Directory: [9]` / `Size: [6]` but NOT
  `File: [1]`, which is **still present** (accepted as a cosmetic limitation).
  Removing it is non-trivial: while `ui:WixUI` is used you CANNOT redefine
  `<Dialog Id="PrepareDlg">` (WiX errors with "Duplicate Control ... PrepareDlg/...").
  The only clean fix is to drop `ui:WixUI` and inline the full WixUI_InstallDir dialog
  set with a PrepareDlg that omits the ActionData control — deliberately not done, as
  the cost outweighs a cosmetic token.
- **WiX does not build on macOS/Linux** — the `Installer` project only compiles on
  Windows (CI). Don't treat WiX edits as verified until the CI build is green.

## Docs

- `README.md` — overview, build, security notes.
- `TESTING.md` — manual test checklist (install, both protections, uninstall, upgrade).
  Keep both in sync when behavior or versions change.
- `docs/PROTECTION-PROMPT.md` — single combined prompt to port the protection layers
  into another project.
- `docs/protection/` — the same prompt split into per-step files (uninstall password,
  Task Manager protection, services.msc protection, progress-text cleanup, app-driven
  stop/start) to apply one at a time.

## Security note (public repo)

This repository is public. The master password `Admin123!` is a **demo** value,
intentionally documented for testing. In production, change it (regenerate the
SHA-256 hash in `UninstallGuard/CustomActions.cs`) or move validation to a server
API. Keep example URLs generic (`api.example.com`), not real internal hosts.
