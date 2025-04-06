using Microsoft.EntityFrameworkCore;
using AiMarketplaceApi.Data;
using AiMarketplaceApi.Services;
using DotNetEnv;
using OpenAI.Extensions;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AiMarketplaceApi.Data.Seeders;

// Load environment variables from .env file
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        builder =>
        {
            builder
                .WithOrigins("http://localhost:3000") 
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
});

// Build MySQL connection string from environment variables
var connectionString = $"Server={Environment.GetEnvironmentVariable("MYSQL_HOST")};" +
                      $"Database={Environment.GetEnvironmentVariable("MYSQL_DATABASE")};" +
                      $"User={Environment.GetEnvironmentVariable("MYSQL_USER")};" +
                      $"Password={Environment.GetEnvironmentVariable("MYSQL_PASSWORD")};";

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add DbContext
builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)
    ));

// Add OpenAI service
builder.Services.AddHttpClient();
builder.Services.AddOpenAIService(settings => { 
    settings.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
});

// Add VendorService
builder.Services.AddScoped<IVendorService, VendorService>();

// Add JWT configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    Environment.GetEnvironmentVariable("JWT_KEY") ?? 
                    throw new InvalidOperationException("JWT_KEY not set in environment")
                )
            ),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

// Add the points calculator service
builder.Services.AddScoped<IPointsCalculator, PointsCalculator>();

var app = builder.Build();

// Use CORS before other middleware
app.UseCors("AllowReactApp");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<GameDbContext>();
    
    // Ensure database is created and migrations are applied
    await context.Database.MigrateAsync();
    
    // Seed data
    await UserSeeder.SeedTestUser(context);
    await LevelSeeder.SeedLevels(context);
}

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
