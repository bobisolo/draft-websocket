using test_worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<ToolboxClientService>();

var host = builder.Build();
host.Run();