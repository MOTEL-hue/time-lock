using TimeLock.Service;

if (args.Length > 0) return Installer.Handle(args);

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(o => o.ServiceName = Installer.ServiceName);
builder.Services.AddHostedService<Worker>();
builder.Build().Run();
return 0;
