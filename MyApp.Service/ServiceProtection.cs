using System.Diagnostics;
using System.Runtime.Versioning;

namespace MyApp.Service;

/// <summary>
/// Protects the Windows service itself from being stopped via services.msc or
/// `sc stop` by an interactive user/admin. It adds a deny ACE for SERVICE_STOP
/// (SDDL right "WP") targeting the Interactive group (IU = S-1-5-4) to the
/// service's security descriptor.
///
/// - SYSTEM (SY) and Administrators (BA) keep their own allow ACEs, so SCM can
///   still manage the service (proper shutdown, recovery restart keep working).
/// - The deny ACE blocks only STOP; query/start are left intact.
///
/// NOTE: This is a DETERRENT, not kernel-level protection. A determined admin
/// can revert it (see RevertCommand) — the manual way is documented for the user.
///
/// Implementation: read the current SDDL with `sc sdshow`, prepend the deny ACE
/// to the DACL, and write it back with `sc sdset`. Reading the live descriptor
/// (instead of hard-coding a full SDDL) keeps SYSTEM/Admin/SCM rights intact
/// across Windows versions.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ServiceProtection
{
    // Deny SERVICE_STOP (WP) to Interactive users (IU). Deny ACEs must precede
    // allow ACEs in the DACL, so this is inserted right after the "D:" prefix.
    private const string DenyStopAce = "(D;;WP;;;IU)";

    /// <summary>
    /// The exact command an admin can run to REMOVE this protection later by
    /// stripping the deny ACE. Logged so it is discoverable from agent.log.
    /// </summary>
    public const string RevertHint =
        "To allow stopping again, run as admin: " +
        "for /f \"tokens=2 delims=:\" %s in ('sc sdshow MyAppAgent') do " +
        "sc sdset MyAppAgent <SDDL-without-(D;;WP;;;IU)>  " +
        "(or simply reinstall the MSI — reinstalling resets the service security).";

    public static void DenyInteractiveStop(string serviceName)
    {
        string current = RunSc($"sdshow {serviceName}");
        if (string.IsNullOrWhiteSpace(current))
            throw new InvalidOperationException("sc sdshow returned no security descriptor.");

        // sdshow output is an SDDL string like "D:(A;;...)...S:(...)".
        string sddl = current.Trim();

        // Already protected? Don't add the deny ACE twice.
        if (sddl.Contains(DenyStopAce, StringComparison.OrdinalIgnoreCase))
            return;

        // Insert the deny ACE at the very start of the DACL (right after "D:").
        int daclIndex = sddl.IndexOf("D:", StringComparison.Ordinal);
        if (daclIndex < 0)
            throw new InvalidOperationException("Unexpected SDDL: no DACL (D:) section found.");

        // Skip flags that may follow "D:" (e.g. "D:PAI(...)"): insert after any
        // flag characters but before the first ACE "(".
        int insertAt = daclIndex + 2;
        while (insertAt < sddl.Length && sddl[insertAt] != '(' && sddl[insertAt] != 'S')
            insertAt++;

        string updated = sddl.Insert(insertAt, DenyStopAce);

        // sc sdset needs the SDDL quoted as a single argument.
        string result = RunSc($"sdset {serviceName} {updated}");

        // sc prints "[SC] SetServiceObjectSecurity SUCCESS" on success.
        if (!result.Contains("SUCCESS", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("sc sdset did not report SUCCESS: " + result);
    }

    /// <summary>
    /// Removes the SERVICE_STOP deny ACE so the service can be stopped/removed
    /// normally again. Safe to call when the ACE is absent (no-op). Used by the
    /// MSI during uninstall and available to an admin via `MyApp.Service.exe --unprotect`.
    /// </summary>
    public static void AllowStop(string serviceName)
    {
        string current = RunSc($"sdshow {serviceName}");
        if (string.IsNullOrWhiteSpace(current))
            return;

        string sddl = current.Trim();
        if (!sddl.Contains(DenyStopAce, StringComparison.OrdinalIgnoreCase))
            return; // nothing to remove

        string updated = sddl.Replace(DenyStopAce, string.Empty);
        RunSc($"sdset {serviceName} {updated}");
    }

    private static string RunSc(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start sc.exe");
        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (stdout + stderr).Trim();
    }
}
