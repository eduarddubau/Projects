using Backend.Data;
using Backend.Extensions;
using Backend.Middleware;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLoggingServices(builder.Configuration, builder.Environment);

builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseSnakeCaseNamingConvention()
);

builder.Services.AddApplicationServices(builder.Configuration, builder.Environment);
builder.Services.AddIdentityServices(builder.Configuration);
builder.Services.AddAuthorizationPolicies();
builder.Services.AddAuthThrottling(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowFrontend",
        p =>
        {
            if (builder.Environment.IsDevelopment())
                p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            else
                p.WithOrigins(
                        builder.Configuration["AllowedOrigins"]
                            ?? throw new InvalidOperationException(
                                "AllowedOrigins is not configured."
                            )
                    )
                    .AllowAnyMethod()
                    .AllowAnyHeader();
        }
    );
});

var app = builder.Build();

// Before the request log and the limiter, both of which read the client address.
app.UseForwardedHeaders();

app.UseExceptionHandler();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseCors("AllowFrontend");

// Ahead of authentication: the auth endpoints are the thing being brute-forced, so
// the limiter has to reject before any password hashing work is done.
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health");

await app.ApplyMigrationsAndSeedAsync();

app.Run();
