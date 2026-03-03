using Backend.Data;
using Backend.Extensions;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());

// Identity
builder.Services.AddIdentityServices(builder.Configuration);

// Application Services
builder.Services.AddApplicationServices(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddCors(options => {
    options.AddPolicy("AllowFrontend", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Middleware
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Custom Health Check 
app.MapHealthChecks("/health");

// Run Migrations and Seeding
await app.ApplyMigrationsAndSeedAsync();

app.Run();