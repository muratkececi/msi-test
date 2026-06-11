using System.Diagnostics;

namespace MyApp.Service;

/// <summary>
/// Background agent. Runs continuously; a real agent would do monitoring/backup
/// work here. As an example it writes a periodic "heartbeat" log. At startup it
/// protects its own process against being terminated from Task Manager.
/// </summary>
public class AgentWorker : BackgroundService
{
    private readonly ILogger<AgentWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    // The service runs as SYSTEM, so ProgramData is writable and persistent.
    private static readonly string LogDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MyApp");
    private static readonly string LogFile = Path.Combine(LogDir, "agent.log");

    // The desktop app drops this file (after a master-password check) to ask the
    // service to stop itself. Path must match MyApp/ServiceControlClient.cs.
    private static readonly string StopRequestFile = Path.Combine(LogDir, "stop.request");

    // The install folder (e.g. C:\Program Files\MyApp). The service runs from the
    // "Agent" subfolder, so the install root is the parent of BaseDirectory.
    // Derived at runtime (not hard-coded) so a custom install path still works.
    private static readonly string? InstallDir =
        Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.FullName;

    public AgentWorker(ILogger<AgentWorker> logger, IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _lifetime = lifetime;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            Write("Service starting.");

            // Protect against Task Manager "End task": remove PROCESS_TERMINATE
            // from the process DACL (SYSTEM can still manage it). Windows-only (P/Invoke).
            if (OperatingSystem.IsWindows())
            {
                ProcessProtection.DenyTerminate();
                Write("Process termination protection applied (PROCESS_TERMINATE denied).");

                // Also block services.msc / `sc stop` for interactive users by
                // denying SERVICE_STOP on the service object. SCM/SYSTEM keep
                // their rights, so proper shutdown and recovery still work.
                try
                {
                    ServiceProtection.DenyInteractiveStop("MyAppAgent");
                    Write("Service stop protection applied (SERVICE_STOP denied). "
                        + ServiceProtection.RevertHint);
                }
                catch (Exception ex)
                {
                    Write("Service stop protection could not be applied: " + ex.Message);
                }

                // Block interactive users from deleting the app folders (Explorer
                // "Delete", del, rmdir). SYSTEM is unaffected, so the MSI can still
                // remove the install folder during uninstall (and the uninstall CA
                // strips these ACEs beforehand as a safety net).
                try
                {
                    if (!string.IsNullOrEmpty(InstallDir))
                        FolderProtection.DenyDelete(InstallDir);
                    FolderProtection.DenyDelete(LogDir);
                    Write("Folder deletion protection applied (DELETE denied to interactive users).");
                }
                catch (Exception ex)
                {
                    Write("Folder deletion protection could not be applied: " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Write("Startup protection could not be applied: " + ex.Message);
        }

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int ticks = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            // Poll for a desktop-app stop request every ~2s; log a heartbeat
            // about every 30s.
            if (CheckStopRequest())
                return; // service is shutting itself down

            if (ticks % 15 == 0)
                Write($"Agent running — PID {Environment.ProcessId}.");
            ticks++;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// If the desktop app requested a stop, lift the stop protection (so SCM can
    /// stop us) and trigger a clean shutdown. Returns true if a stop was started.
    /// </summary>
    private bool CheckStopRequest()
    {
        try
        {
            if (!File.Exists(StopRequestFile))
                return false;

            Write("Stop requested by the desktop app (master password verified there).");
            File.Delete(StopRequestFile);

            // We run as SYSTEM, so we can lift our own SERVICE_STOP deny ACE.
            if (OperatingSystem.IsWindows())
                ServiceProtection.AllowStop("MyAppAgent");

            // Ask the host to stop; StopAsync then runs and the process exits
            // cleanly. A clean stop does NOT trigger SCM recovery.
            _lifetime.StopApplication();
            return true;
        }
        catch (Exception ex)
        {
            Write("Failed to handle stop request: " + ex.Message);
            return false;
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        // This is reached ONLY on a clean SCM stop (services.msc / sc stop).
        // It does NOT run on a forced kill from Task Manager; in that case the
        // SCM recovery setting restarts the service.
        Write("Service stopping cleanly (SCM).");
        return base.StopAsync(cancellationToken);
    }

    private void Write(string message)
    {
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}";
        _logger.LogInformation(message);
        try { File.AppendAllText(LogFile, line + Environment.NewLine); } catch { }
    }
}
