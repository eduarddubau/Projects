using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Backend.Config;
using Backend.Data;
using Backend.Filters;
using Backend.Middleware;
using Backend.Models;
using Backend.Services;
using Backend.Services.Admin;
using Backend.Services.Admin.Interfaces;
using Backend.Services.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;
// HttpOverrides ships an IPNetwork of its own; KnownIPNetworks wants this one.
using IPNetwork = System.Net.IPNetwork;

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
                    // One JSON object per line on stdout: any collector can parse it
                    // without a regex.
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
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IWorkspaceAccessService, WorkspaceAccessService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IInvitationService, InvitationService>();

        // Unscoped by design — only the controllers under /api/admin may take these.
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAdminProjectService, AdminProjectService>();
        services.AddScoped<IAdminWorkspaceService, AdminWorkspaceService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();

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

        var throttle =
            config.GetSection(AuthProtectionOptions.SectionName).Get<AuthProtectionOptions>()
            ?? new AuthProtectionOptions();

        services
            .AddIdentityCore<User>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;

                // Lockout is per account, which is the layer that survives an attacker
                // rotating IPs. The per-IP limiter in AddAuthThrottling is the other half.
                options.Lockout.MaxFailedAccessAttempts = throttle.MaxFailedAttempts;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(
                    throttle.LockoutMinutes
                );
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            // AddIdentityCore leaves it out, and it owns the only password check that
            // records a failure against the account.
            .AddSignInManager()
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

    /// <summary>
    /// Per-IP throttling for the auth endpoints. Forwarded headers belong here too:
    /// behind a proxy, an unconfigured RemoteIpAddress is the proxy's, so every caller
    /// shares one partition and the limiter throttles the app as a single client.
    /// </summary>
    public static IServiceCollection AddAuthThrottling(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        services
            .AddOptions<AuthProtectionOptions>()
            .Bind(config.GetSection(AuthProtectionOptions.SectionName));

        var throttle =
            config.GetSection(AuthProtectionOptions.SectionName).Get<AuthProtectionOptions>()
            ?? new AuthProtectionOptions();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // An empty trust list reads as "don't check" to the middleware, not "trust
            // nothing" — it would then believe X-Forwarded-* from any caller, who could
            // pick their own rate-limit partition and their own scheme. Both headers stay
            // off together: neither is worth anything without a proxy vouching for it.
            options.ForwardedHeaders =
                throttle.TrustedProxyNetworks.Length > 0
                    ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                    : ForwardedHeaders.None;

            // Defaults trust a loopback proxy only, which a container never is.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            foreach (var network in throttle.TrustedProxyNetworks)
            {
                if (IPNetwork.TryParse(network, out var parsed))
                    options.KnownIPNetworks.Add(parsed);
                else
                    throw new InvalidOperationException(
                        $"AuthProtection:TrustedProxyNetworks contains '{network}', which is not CIDR."
                    );
            }

            // One hop: the reverse proxy. More lets a client prepend a forged address.
            options.ForwardLimit = 1;
        });

        services.AddRateLimiter(options =>
        {
            options.OnRejected = RateLimitRejectionHandler.WithFallbackWindow(
                throttle.WindowSeconds
            );

            options.AddPolicy(
                AppPolicies.AuthThrottle,
                httpContext =>
                    SlidingWindowPerClient(
                        httpContext,
                        throttle.PermitPerWindow,
                        throttle.WindowSeconds
                    )
            );

            options.AddPolicy(
                AppPolicies.SessionThrottle,
                httpContext =>
                    SlidingWindowPerClient(
                        httpContext,
                        throttle.SessionPermitPerWindow,
                        throttle.WindowSeconds
                    )
            );
        });

        return services;
    }

    /// <summary>One budget per caller. Sliding, not fixed: a fixed window lets an attacker
    /// spend a full quota either side of the boundary for double the rate.</summary>
    private static RateLimitPartition<string> SlidingWindowPerClient(
        HttpContext httpContext,
        int permitLimit,
        int windowSeconds
    ) =>
        RateLimitPartition.GetSlidingWindowLimiter(
            ClientKey(httpContext),
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                SegmentsPerWindow = 4,
                QueueLimit = 0,
            }
        );

    /// <summary>Falls back to one shared bucket when there is no address — that
    /// throttles those callers collectively, the safe direction to fail.</summary>
    private static string ClientKey(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AppPolicies.AdminOnly, policy => policy.RequireRole(AppRoles.Admin));

            // Admin is excluded: an administrator administers, and holds no projects
            // or workspaces of their own.
            options.AddPolicy(
                AppPolicies.StandardUser,
                policy => policy.RequireRole(AppRoles.User)
            );
        });

        return services;
    }
}
