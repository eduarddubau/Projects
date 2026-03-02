using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Backend.Data;
using System.Text.Json.Serialization;
using Backend.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;
using backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Get connection string from appsettings.json or Environment Variables
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");


// Register OpenAPI
builder.Services.AddOpenApi();

// Register Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Register the CurrentUserService
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Register the DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention());

// Add the Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowFrontend");

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    // This ensures the JSON is available for Scalar to read
    app.MapOpenApi();
    // This creates the UI at /scalar/v1
    app.MapScalarApiReference(); 
}

// Map the health check endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() })
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
});

app.Run();