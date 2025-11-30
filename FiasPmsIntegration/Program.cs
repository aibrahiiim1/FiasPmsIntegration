using FiasPmsIntegration.Models;
using FiasPmsIntegration.Services;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Configure FIAS Server Options from appsettings.json
builder.Services.Configure<FiasServerOptions>(
    builder.Configuration.GetSection("FiasServer"));

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Register FIAS services as Singletons (they persist for app lifetime)
builder.Services.AddSingleton<LogService>();
builder.Services.AddSingleton<GuestDataStore>();
builder.Services.AddSingleton<FiasProtocolService>();
builder.Services.AddSingleton<FiasSocketServer>();

// Register the background service - THIS RUNS INDEPENDENTLY
// The FIAS server will start when the application starts and run continuously
// It doesn't depend on any web page being open
builder.Services.AddHostedService<FiasServerBackgroundService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});



var app = builder.Build();

// Log startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("=== FIAS PMS Integration Starting ===");
logger.LogInformation("Background Service will run independently of web requests");

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Handle graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    logger.LogInformation("=== Application is shutting down ===");
});

logger.LogInformation("=== Application started - FIAS server running in background ===");

app.Run();