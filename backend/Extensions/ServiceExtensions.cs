using System.Text.Json.Serialization;
using Backend.Services;
using Backend.Middleware;
using Backend.Models;
using Backend.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Backend.Config;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration config)
    {
        // JSON and Controllers
        services.AddControllers()
            .AddJsonOptions(options => {
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                options.JsonSerializerOptions.WriteIndented = true;
            });

        // Custom Services
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IUserService, UserService>();

        // Health Checks
        services.AddHealthChecks()
            .AddNpgSql(config.GetConnectionString("DefaultConnection")!);

        // Exception Handling
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        // Token Service
        services.AddScoped<ITokenService, TokenService>();

        return services;
    }

    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddDataProtection();

        // Identity Configuration
        services.AddIdentityCore<User>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        // JWT Authentication
        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var jwtOptions = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>();

        if (jwtOptions == null || string.IsNullOrEmpty(jwtOptions.Key))
        {
            throw new InvalidOperationException("JWT settings are missing!");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        return services;
    }
}