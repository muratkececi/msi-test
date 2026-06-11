using MyApp.Service;

// Maintenance mode: "--unprotect" lifts both protections so the service can be
// stopped/removed and the install folder deleted normally (used by the MSI
// during uninstall). It does NOT start the host; it just reverts protection and
// exits. It does NOT delete any files — the data folder and its logs stay.
if (args.Length > 0 && args[0].Equals("--unprotect", StringComparison.OrdinalIgnoreCase))
{
    if (OperatingSystem.IsWindows())
    {
        try { ServiceProtection.AllowStop("MyAppAgent"); }
        catch { /* best effort: uninstall must not fail because of this */ }

        // Strip the deny-DELETE ACEs so the MSI (and the user) can remove the
        // install folder. ProgramData is unprotected too but left on disk.
        try
        {
            string? installDir = System.IO.Directory
                .GetParent(AppContext.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar))?.FullName;
            if (!string.IsNullOrEmpty(installDir))
                FolderProtection.AllowDelete(installDir);

            string dataDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MyApp");
            FolderProtection.AllowDelete(dataDir);
        }
        catch { /* best effort */ }
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
