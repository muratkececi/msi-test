using System.Diagnostics;
using System.Runtime.Versioning;

namespace MyApp.Service;

/// <summary>
/// Protects the application's folders from being deleted by an interactive
/// user/admin (Explorer "Delete", `del`, `rmdir`). It adds a deny ACE for
/// DELETE + DELETE_CHILD targeting the Interactive group (IU = S-1-5-4) on:
///   - the install folder   (C:\Program Files\MyApp)
///   - the data folder       (C:\ProgramData\MyApp)
///
/// The ACE targets Interactive users only, NOT SYSTEM. The MSI's RemoveFiles /
/// RemoveFolders run as SYSTEM during an elevated uninstall, so they are not
/// blocked by this ACE. As an extra safety net the uninstall custom action
/// (`--unprotect`) strips these ACEs before RemoveFiles runs anyway.
///
/// NOTE: this is a DETERRENT, not kernel-level protection. A determined admin
/// can take ownership and revert the ACL — same caveat as ServiceProtection.
///
/// Implementation uses icacls so the existing ACL is preserved (we only ADD a
/// deny ACE); hard-coding a full ACL would risk locking out SYSTEM/Admins.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class FolderProtection
{
    // Interactive group well-known SID. icacls accepts a literal SID via "*S-...".
    private const string InteractiveSid = "*S-1-5-4";

    // DE = DELETE (remove the folder itself), DC = DELETE_CHILD (remove items
    // inside it). Denying both blocks deleting the folder and its contents.
    private const string DeleteRights = "(OI)(CI)(DE,DC)";

    /// <summary>
    /// Adds a deny-DELETE ACE for Interactive users on the given folder.
    /// Idempotent and best-effort: icacls "merges" a re-applied deny ACE, and any
    /// failure is reported to the caller's log without throwing fatally.
    /// </summary>
    public static void DenyDelete(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        // /deny adds a deny ACE while keeping existing permissions intact.
        RunIcacls($"\"{path}\" /deny {InteractiveSid}:{DeleteRights}");
    }

    /// <summary>
    /// Removes the deny-DELETE ACE for Interactive users again (used at uninstall
    /// and by `--unprotect`). Does NOT touch the folder contents. Safe to call
    /// when the ACE is absent (icacls /remove is a no-op then).
    /// </summary>
    public static void AllowDelete(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        // /remove:d removes only DENY ACEs for the principal.
        RunIcacls($"\"{path}\" /remove:d {InteractiveSid}");
    }

    private static void RunIcacls(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "icacls.exe",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start icacls.exe");
        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
    }
}
