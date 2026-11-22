using AudioLab.Server;

WebApplication app = ServerHost.Build(args);
await app.RunAsync();
