using MyApp.Service;

var builder = Host.CreateApplicationBuilder(args);

// Windows Service olarak çalış (SCM ile entegre).
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "MyAppAgent";
});

builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
host.Run();
