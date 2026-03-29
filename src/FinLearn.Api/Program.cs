using FinLearn.Api.Endpoints;
using FinLearn.Api.Services;
using FinLearn.Core;

var builder = WebApplication.CreateBuilder(args);

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

app.Run();

public partial class Program { }
