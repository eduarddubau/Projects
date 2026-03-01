using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Backend.Data;
using System.Text.Json.Serialization;
using Backend.Middleware;

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
app.MapHealthChecks("/health");

app.Run();