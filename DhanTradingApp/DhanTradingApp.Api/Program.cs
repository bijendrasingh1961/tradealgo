using DhanTradingApp.Api.Data;
using DhanTradingApp.Api.Hubs;
using DhanTradingApp.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<TradingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact",
        builder => builder.WithOrigins("http://localhost:5173", "https://localhost:5173")
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials());
});

builder.Services.AddHttpClient<IDhanApiService, DhanApiService>();
builder.Services.AddSingleton<DhanWebSocketService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DhanWebSocketService>());
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowReact");
app.UseAuthorization();
app.MapControllers();

app.MapHub<TradingHub>("/tradingHub");

app.Run();
