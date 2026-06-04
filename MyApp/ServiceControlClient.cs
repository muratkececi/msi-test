using System;
using System.IO;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;

namespace MyApp;

/// <summary>
/// Lets the desktop app stop/start the protected background service after a
/// master-password check.
///
/// Stopping is special: the service denies SERVICE_STOP to interactive users
/// (see MyApp.Service/ServiceProtection.cs), so the app cannot call `sc stop`
/// directly. Instead it drops a "stop request" control file under ProgramData;
/// the service (running as SYSTEM) watches for it, lifts its own stop protection,
/// and shuts itself down. Starting needs no such trick — the deny ACE blocks only
/// STOP, not START — so the app starts the service via ServiceController.
///
/// The password is validated against the same SHA-256 hash used by UninstallGuard.
/// TODO: replace ValidatePassword with a server API call when ready (keep this the
/// single validation point so the switch is one method).
/// </summary>
internal static class ServiceControlClient
{
    public const string ServiceName = "MyAppAgent";

    // Same master password as uninstall ("Admin123!") — SHA-256, UPPERCASE hex.
    // Demo value; change for production or move validation to an API.
    private const string MasterPasswordHash =
        "3EB3FE66B31E3B4D10FA70B5CAD49C7112294AF6AE4E476A1C405155D45AA121";

    // Control file the service watches for. Must match the path the service uses.
    private static readonly string ControlDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MyApp");
    private static readonly string StopRequestFile = Path.Combine(ControlDir, "stop.request");

    /// <summary>Validates the master password. Single point to later swap for an API.</summary>
    public static bool ValidatePassword(string password)
    {
        using var sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password ?? string.Empty));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
            sb.Append(b.ToString("X2"));
        return string.Equals(sb.ToString(), MasterPasswordHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True if the service is currently running.</summary>
    public static bool IsRunning()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            return sc.Status == ServiceControllerStatus.Running
                || sc.Status == ServiceControllerStatus.StartPending;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Requests a stop by writing the control file the SYSTEM service watches for.
    /// Returns true once the service actually reaches Stopped (waits briefly).
    /// </summary>
    public static bool RequestStop(out string? error)
    {
        error = null;
        try
        {
            Directory.CreateDirectory(ControlDir);
            File.WriteAllText(StopRequestFile, DateTime.Now.ToString("o"));

            using var sc = new ServiceController(ServiceName);
            // The service self-stops within its poll interval; give it a window.
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
            return sc.Status == ServiceControllerStatus.Stopped;
        }
        catch (System.ServiceProcess.TimeoutException)
        {
            error = "The service did not stop within the timeout.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Starts the service. The stop-protection deny ACE blocks only STOP, so an
    /// interactive user can start it normally via the SCM.
    /// </summary>
    public static bool Start(out string? error)
    {
        error = null;
        try
        {
            // Remove any stale stop request so the service doesn't immediately re-stop.
            if (File.Exists(StopRequestFile))
                File.Delete(StopRequestFile);

            using var sc = new ServiceController(ServiceName);
            if (sc.Status == ServiceControllerStatus.Running)
                return true;

            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
