using FinLearnApp.Api.Data;
using FinLearnApp.Api.Mappers;
using FinLearnApp.Api.Services;
using FinLearnApp.Application.Actions;
using MediatR;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/finlearn-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddProblemDetails();
    builder.Services.AddControllers();
    builder.Services.AddSingleton(_ => SeedData.Create());
    builder.Services.AddSingleton<PortfolioMapper>();
    builder.Services.AddSingleton<IActionExecutionStore, InMemoryActionExecutionStore>();
    builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(BuyNowCommand).Assembly));

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var feature = context.Features.Get<IExceptionHandlerFeature>();
                if (feature is not null)
                {
                    Log.Error(feature.Error, "Unhandled exception.");
                }

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal Server Error",
                    Detail = "An unexpected error occurred.",
                };

                problem.Extensions["code"] = "common.unexpected_error";

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";

                await context.Response.WriteAsJsonAsync(problem);
            });
        });
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();
    app.UseRouting();

    app.UseAuthorization();

    app.MapControllers();


    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed.");
}
finally
{
    Log.CloseAndFlush();
}
