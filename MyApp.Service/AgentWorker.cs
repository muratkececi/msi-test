using System.Diagnostics;

namespace MyApp.Service;

/// <summary>
/// Arka plan ajanı. Sürekli çalışır; gerçek bir ajanda burada izleme/yedekleme
/// gibi işler yapılır. Örnek olarak periyodik bir "heartbeat" log'u yazar.
/// Başlangıçta kendi sürecini Task Manager'dan sonlandırmaya karşı korur.
/// </summary>
public class AgentWorker : BackgroundService
{
    private readonly ILogger<AgentWorker> _logger;

    // Service SYSTEM altında çalıştığı için ProgramData yazılabilir ve kalıcıdır.
    private static readonly string LogDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MyApp");
    private static readonly string LogFile = Path.Combine(LogDir, "agent.log");

    public AgentWorker(ILogger<AgentWorker> logger)
    {
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            Write("Service başlıyor.");

            // Protect against Task Manager "End task": remove PROCESS_TERMINATE
            // from the process DACL (SYSTEM can still manage it). Windows-only (P/Invoke).
            if (OperatingSystem.IsWindows())
            {
                ProcessProtection.DenyTerminate();
                Write("Süreç sonlandırma koruması uygulandı (PROCESS_TERMINATE kaldırıldı).");

                // Also block services.msc / `sc stop` for interactive users by
                // denying SERVICE_STOP on the service object. SCM/SYSTEM keep
                // their rights, so proper shutdown and recovery still work.
                try
                {
                    ServiceProtection.DenyInteractiveStop("MyAppAgent");
                    Write("Servis durdurma koruması uygulandı (SERVICE_STOP reddedildi). "
                        + ServiceProtection.RevertHint);
                }
                catch (Exception ex)
                {
                    Write("Servis durdurma koruması uygulanamadı: " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Write("Başlangıç koruması uygulanamadı: " + ex.Message);
        }

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Write($"Ajan çalışıyor — PID {Environment.ProcessId}.");
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        // Buraya SADECE SCM düzgün durdurduğunda (services.msc / sc stop) gelinir.
        // Task Manager'dan zorla öldürmede bu çalışmaz; o durumda SCM recovery
        // ayarı service'i yeniden başlatır.
        Write("Service düzgün şekilde durduruluyor (SCM).");
        return base.StopAsync(cancellationToken);
    }

    private void Write(string message)
    {
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}";
        _logger.LogInformation(message);
        try { File.AppendAllText(LogFile, line + Environment.NewLine); } catch { }
    }
}
