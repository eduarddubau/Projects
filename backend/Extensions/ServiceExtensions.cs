using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Backend.Config;
using Backend.Data;
using Backend.Filters;
using Backend.Middleware;
using Backend.Models;
using Backend.Services;
using Backend.Services.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;

namespace Backend.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddLoggingServices(
        this IServiceCollection services,
        IConfiguration config,
        IWebHostEnvironment env
    )
    {
        services.AddSerilog(
            (provider, loggerConfig) =>
            {
                loggerConfig
                    .ReadFrom.Configuration(config)
                    .ReadFrom.Services(provider)
                    .Enrich.FromLogContext();

                if (env.IsDevelopment())
                    loggerConfig.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);
                else
                    // One JSON object per line on stdout: the container-native contract, so
                    // any collector can parse it without a regex.
                    loggerConfig.WriteTo.Console(new CompactJsonFormatter());

                loggerConfig.WriteTo.File(
                    new CompactJsonFormatter(),
                    "logs/api-.json",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    fileSizeLimitBytes: 50 * 1024 * 1024,
                    rollOnFileSizeLimit: true
                );
            }
        );

        return services;
    }

    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration config,
        IWebHostEnvironment env
    )
    {
        services
            .AddControllers(options => options.Filters.Add<FluentValidationFilter>())
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                options.JsonSerializerOptions.WriteIndented = env.IsDevelopment();
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.AddValidatorsFromAssemblyContaining<Program>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IWorkspaceAccessService, WorkspaceAccessService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IInvitationService, InvitationService>();

        // Test-only fixture seeding; the controller that uses it is gated to Development.
        services.AddScoped<ITestSeedService, TestSeedService>();

        services
            .AddOptions<ProjectRetentionOptions>()
            .Bind(config.GetSection(ProjectRetentionOptions.SectionName));

        services
            .AddOptions<AdminSeedOptions>()
            .Bind(config.GetSection(AdminSeedOptions.SectionName));

        services.AddHealthChecks().AddNpgSql(config.GetConnectionString("DefaultConnection")!);

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }

    public static IServiceCollection AddIdentityServices(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        services.AddDataProtection();

        services
            .AddIdentityCore<User>(options =>
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

        services
            .AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var jwtOptions = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>();

        if (jwtOptions == null || string.IsNullOrEmpty(jwtOptions.Key))
            throw new InvalidOperationException("JWT settings are missing!");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.Key)
                    ),
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };
            });

        return services;
    }

    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AppPolicies.AdminOnly, policy => policy.RequireRole(AppRoles.Admin));

            // Admin is excluded: an administrator administers, and holds no projects
            // or workspaces of their own. Everything they can reach is under /api/admin.
            options.AddPolicy(
                AppPolicies.StandardUser,
                policy => policy.RequireRole(AppRoles.User)
            );
        });

        return services;
    }
}
