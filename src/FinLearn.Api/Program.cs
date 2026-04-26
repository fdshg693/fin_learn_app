using FinLearn.Api.Endpoints;
using FinLearn.Api.Services;
using FinLearn.Core;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}")
    .WriteTo.File(
        formatter: new CompactJsonFormatter(),
        path: "logs/finlearn-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddSingleton<GameConfig>();
    builder.Services.AddSingleton<GameStore>();
    builder.Services.AddSingleton<IExchangeFactory, SimpleExchangeFactory>();
    builder.Services.AddTransient<TurnProcessor>(sp =>
    {
        var exchangeFactory = sp.GetRequiredService<IExchangeFactory>();
        return new TurnProcessor(new ComputerTrader(Random.Shared), new Market(), new RandomPriceFluctuator(Random.Shared), exchangeFactory);
    });

    var corsOrigins = builder.Configuration["CORS_ALLOWED_ORIGINS"]?
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        ?? ["http://localhost:5173"];

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });

    var app = builder.Build();

    app.UseCors();
    app.MapGameEndpoints();
    app.MapAdminEndpoints();

    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
