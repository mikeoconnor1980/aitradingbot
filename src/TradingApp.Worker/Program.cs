using TradingApp.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();

app.Run();
