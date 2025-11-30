using FiasPmsIntegration.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Register FIAS services
builder.Services.AddSingleton<LogService>();

builder.Services.AddSingleton<FiasProtocolService>();
builder.Services.AddSingleton<FiasSocketServer>();
builder.Services.AddSingleton<GuestDataStore>();
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
