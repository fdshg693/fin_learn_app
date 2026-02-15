using FinLearnApp.Api.Data;
using FinLearnApp.Api.Mappers;
using FinLearnApp.Api.Services;
using FinLearnApp.Application.Actions;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton(_ => SeedData.Create());
builder.Services.AddSingleton<PortfolioMapper>();
builder.Services.AddSingleton<IActionExecutionStore, InMemoryActionExecutionStore>();
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(BuyNowCommand).Assembly));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapControllers();


app.Run();
