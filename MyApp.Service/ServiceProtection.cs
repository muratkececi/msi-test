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

    // Allow Interactive users (IU) to START, QUERY and read the service:
    // RP=SERVICE_START, LC=SERVICE_QUERY_STATUS, RC=READ_CONTROL.
    // A standard service's default DACL does NOT grant SERVICE_START to interactive
    // users, so the desktop app's ServiceController.Start() fails with
    // "Cannot open 'MyAppAgent' service". Granting RP (but NOT WP) lets a normal
    // user start the service without elevation while STOP stays password-gated.
    private const string AllowStartAce = "(A;;RPLCRC;;;IU)";

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

        // Already protected? Don't add our ACEs twice.
        if (sddl.Contains(DenyStopAce, StringComparison.OrdinalIgnoreCase))
            return;

        // Locate the DACL ("D:") and the start of the security ACL ("S:"), if any.
        int daclIndex = sddl.IndexOf("D:", StringComparison.Ordinal);
        if (daclIndex < 0)
            throw new InvalidOperationException("Unexpected SDDL: no DACL (D:) section found.");

        // The deny ACE must come FIRST in the DACL (deny ACEs precede allow ACEs).
        // Skip any flag characters after "D:" (e.g. "D:PAI(...)") up to the first ACE.
        int denyAt = daclIndex + 2;
        while (denyAt < sddl.Length && sddl[denyAt] != '(' && sddl[denyAt] != 'S')
            denyAt++;
        string updated = sddl.Insert(denyAt, DenyStopAce);

        // The allow-start ACE can go at the end of the DACL, i.e. just before the
        // SACL ("S:") if present, otherwise at the end of the string. Order among
        // allow ACEs does not matter.
        int saclIndex = updated.IndexOf("S:", denyAt, StringComparison.Ordinal);
        if (saclIndex < 0)
            updated += AllowStartAce;
        else
            updated = updated.Insert(saclIndex, AllowStartAce);

        // sc sdset needs the SDDL as a single argument.
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
