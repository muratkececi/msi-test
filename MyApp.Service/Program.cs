using MyApp.Service;

// Maintenance mode: "--unprotect" removes the SERVICE_STOP deny ACE so the
// service can be stopped/removed normally (used by the MSI during uninstall).
// It does NOT start the host; it just reverts protection and exits.
if (args.Length > 0 && args[0].Equals("--unprotect", StringComparison.OrdinalIgnoreCase))
{
    if (OperatingSystem.IsWindows())
    {
        try { ServiceProtection.AllowStop("MyAppAgent"); }
        catch { /* best effort: uninstall must not fail because of this */ }
    }
    return;
}

var builder = Host.CreateApplicationBuilder(args);

// Run as a Windows Service (integrated with SCM).
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "MyAppAgent";
});

builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
host.Run();
