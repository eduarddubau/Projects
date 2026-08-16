using System.Globalization;
using System.Net;
using Backend.Config;
using Backend.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Backend.Tests.Middleware;

/// <summary>
/// Drives the real AddAuthThrottling pipeline over a stub endpoint. The limiter is
/// middleware, so the controller unit tests never reach it — only a host does.
/// </summary>
public class AuthThrottleTests
{
    private const string StrictPath = "/strict";
    private const string SessionPath = "/session";
    private const int PermitLimit = 3;
    private const int SessionPermitLimit = 5;

    private static Task<IHost> StartHostAsync(params string[] trustedProxyNetworks)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AuthProtection:PermitPerWindow"] = PermitLimit.ToString(CultureInfo.InvariantCulture),
            ["AuthProtection:SessionPermitPerWindow"] = SessionPermitLimit.ToString(
                CultureInfo.InvariantCulture
            ),
            // Long enough that no test can outrun its own window.
            ["AuthProtection:WindowSeconds"] = "300",
        };

        for (var i = 0; i < trustedProxyNetworks.Length; i++)
            settings[$"AuthProtection:TrustedProxyNetworks:{i}"] = trustedProxyNetworks[i];

        return new HostBuilder()
            .ConfigureWebHost(web =>
                web.UseTestServer()
                    .ConfigureAppConfiguration(config => config.AddInMemoryCollection(settings))
                    .ConfigureServices(
                        (context, services) =>
                        {
                            services.AddRouting();
                            services.AddAuthThrottling(context.Configuration);
                        }
                    )
                    .Configure(app =>
                    {
                        app.UseForwardedHeaders();
                        app.UseRouting();
                        app.UseRateLimiter();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints
                                .MapGet(StrictPath, () => Results.Ok())
                                .RequireRateLimiting(AppPolicies.AuthThrottle);
                            endpoints
                                .MapGet(SessionPath, () => Results.Ok())
                                .RequireRateLimiting(AppPolicies.SessionThrottle);
                        });
                    })
            )
            .StartAsync();
    }

    private static Task<HttpContext> SendAsync(
        IHost host,
        string path,
        string clientIp,
        string? forwardedFor = null
    ) =>
        host.GetTestServer()
            .SendAsync(context =>
            {
                context.Request.Method = HttpMethods.Get;
                context.Request.Path = path;
                context.Connection.RemoteIpAddress = IPAddress.Parse(clientIp);

                if (forwardedFor is not null)
                    context.Request.Headers["X-Forwarded-For"] = forwardedFor;
            });

    [Fact]
    public async Task RequestsWithinTheLimit_AreAllowed()
    {
        using var host = await StartHostAsync();

        for (var i = 0; i < PermitLimit; i++)
        {
            var context = await SendAsync(host, StrictPath, "203.0.113.10");
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }
    }

    [Fact]
    public async Task RequestOverTheLimit_IsRejectedWithRetryAfter()
    {
        using var host = await StartHostAsync();

        for (var i = 0; i < PermitLimit; i++)
            await SendAsync(host, StrictPath, "203.0.113.11");

        var rejected = await SendAsync(host, StrictPath, "203.0.113.11");

        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.Response.StatusCode);

        // The header is the whole point: without it a client has no idea when to retry.
        var retryAfter = rejected.Response.Headers.RetryAfter.ToString();
        Assert.False(string.IsNullOrEmpty(retryAfter));
        Assert.True(int.Parse(retryAfter, CultureInfo.InvariantCulture) > 0);
    }

    [Fact]
    public async Task OneClientExhaustingItsBudget_DoesNotThrottleAnother()
    {
        using var host = await StartHostAsync();

        for (var i = 0; i <= PermitLimit; i++)
            await SendAsync(host, StrictPath, "203.0.113.12");

        var other = await SendAsync(host, StrictPath, "203.0.113.13");

        Assert.Equal(StatusCodes.Status200OK, other.Response.StatusCode);
    }

    [Fact]
    public async Task ForgedForwardedFor_DoesNotEarnAFreshBudget()
    {
        // No trusted networks: the header must be ignored entirely.
        using var host = await StartHostAsync();

        for (var i = 0; i < PermitLimit; i++)
            await SendAsync(host, StrictPath, "203.0.113.14", forwardedFor: $"198.51.100.{i}");

        var rejected = await SendAsync(
            host,
            StrictPath,
            "203.0.113.14",
            forwardedFor: "198.51.100.200"
        );

        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.Response.StatusCode);
    }

    [Fact]
    public async Task BehindATrustedProxy_CallersArePartitionedByForwardedAddress()
    {
        using var host = await StartHostAsync("10.89.0.0/16");

        // Every request arrives from the same proxy; only the forwarded address differs.
        for (var i = 0; i <= PermitLimit; i++)
            await SendAsync(host, StrictPath, "10.89.0.2", forwardedFor: "198.51.100.1");

        var otherCaller = await SendAsync(
            host,
            StrictPath,
            "10.89.0.2",
            forwardedFor: "198.51.100.2"
        );

        Assert.Equal(StatusCodes.Status200OK, otherCaller.Response.StatusCode);
    }

    [Fact]
    public async Task ExhaustingTheLoginBudget_LeavesTheSessionBudgetIntact()
    {
        using var host = await StartHostAsync();

        for (var i = 0; i <= PermitLimit; i++)
            await SendAsync(host, StrictPath, "203.0.113.15");

        var refresh = await SendAsync(host, SessionPath, "203.0.113.15");

        Assert.Equal(StatusCodes.Status200OK, refresh.Response.StatusCode);
    }
}
