using System.Text.Json.Serialization;
using FinLearn.Api.Endpoints;
using FinLearn.Api.Services;
using FinLearn.Core;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Filters;
using Serilog.Formatting.Compact;

// Bootstrap logger — replaced once host is built. Console-only so we don't need config.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // preserveStaticLogger: true keeps the bootstrap logger as static Log.Logger so multiple
    // WebApplicationFactory<Program> fixtures don't collide on ReloadableLogger.Freeze().
    // Footgun: production code must resolve ILogger<T> from DI — calling Serilog.Log.* directly
    // hits only the bootstrap console sink and silently misses the OrderLog rolling file sink.
    builder.Host.UseSerilog((ctx, services, lc) =>
    {
        var retainedFileCountLimit = ctx.Configuration.GetValue<int?>("OrderLog:RetainedFileCountLimit") ?? 7;
        lc.MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}")
            .WriteTo.Logger(sub => sub
                .Filter.ByIncludingOnly(Matching.FromSource<OrderLog>())
                .WriteTo.File(
                    formatter: new CompactJsonFormatter(),
                    path: "logs/orders-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: retainedFileCountLimit));
    }, preserveStaticLogger: true);

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    builder.Services.Configure<GameConfig>(builder.Configuration.GetSection("Game"));
    builder.Services.Configure<AdminConfig>(builder.Configuration.GetSection("Admin"));
    builder.Services.Configure<GameStoreConfig>(builder.Configuration.GetSection("GameStore"));

    // Existing consumers expect the POCO directly — expose IOptions<T>.Value as a singleton.
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<GameConfig>>().Value);
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AdminConfig>>().Value);
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<GameStoreConfig>>().Value);

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

    builder.Services.AddRazorPages();

    var app = builder.Build();

    app.UseCors();
    app.UseStaticFiles();
    app.MapRazorPages();
    app.MapGameEndpoints();
    app.MapAdminEndpoints();

    app.MapGet("/", () => Results.Content("""
        <!DOCTYPE html>
        <html lang="ja"><head><meta charset="utf-8"><title>FinLearn</title></head>
        <body style="font-family:system-ui;padding:2rem;">
            <h1>株売買シミュレーター</h1>
            <ul>
                <li><a href="/play">HTMX 版（同一プロセス）</a></li>
                <li>React 版: 別途 <code>npm run dev</code> 起動 → http://localhost:5173</li>
            </ul>
        </body></html>
        """, "text/html"));

    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
