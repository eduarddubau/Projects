using System.Text.Json;
using Backend.Exceptions;
using Backend.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace Backend.Tests.Middleware;

public class GlobalExceptionHandlerTests
{
    private readonly GlobalExceptionHandler _handler;

    public GlobalExceptionHandlerTests()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns("Production");

        _handler = new GlobalExceptionHandler(Mock.Of<ILogger<GlobalExceptionHandler>>(), env.Object);
    }

    /// <summary>Runs the handler over a throwaway context and returns the serialised body,
    /// so assertions are against what a client actually receives.</summary>
    private async Task<JsonElement> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        Assert.True(await _handler.TryHandleAsync(context, exception, CancellationToken.None));

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task BusinessRuleException_WithParams_SerialisesCodeAndParams()
    {
        var body = await HandleAsync(new BusinessRuleException(
            "SoleOwnerOfWorkspaces",
            "This user is the only owner of: Acme Team.",
            new Dictionary<string, string> { ["workspaces"] = "Acme Team" }));

        Assert.Equal(StatusCodes.Status409Conflict, body.GetProperty("statusCode").GetInt32());
        Assert.Equal("SoleOwnerOfWorkspaces", body.GetProperty("code").GetString());
        Assert.Equal("Acme Team", body.GetProperty("params").GetProperty("workspaces").GetString());
    }

    [Fact]
    public async Task BusinessRuleException_WithoutParams_OmitsThemButKeepsCode()
    {
        var body = await HandleAsync(new BusinessRuleException("DuplicateProjectName", "Taken."));

        Assert.Equal("DuplicateProjectName", body.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("params").ValueKind);
    }

    [Fact]
    public async Task NotFoundException_CarriesNeitherCodeNorParams()
    {
        // A code here would leak the missing-vs-forbidden distinction the 404 exists to hide.
        var body = await HandleAsync(new NotFoundException("Workspace not found."));

        Assert.Equal(StatusCodes.Status404NotFound, body.GetProperty("statusCode").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("code").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("params").ValueKind);
    }

    [Fact]
    public async Task UnexpectedException_IsMappedTo500WithoutLeakingItsMessage()
    {
        var body = await HandleAsync(new InvalidOperationException("Connection string is bad."));

        Assert.Equal(StatusCodes.Status500InternalServerError, body.GetProperty("statusCode").GetInt32());
        Assert.DoesNotContain("Connection string", body.GetProperty("message").GetString());
        // Outside Development the stack trace must not travel to the client.
        Assert.Equal(JsonValueKind.Null, body.GetProperty("details").ValueKind);
    }

    [Fact]
    public async Task EveryResponse_CarriesATraceIdForCorrelation()
    {
        var body = await HandleAsync(new NotFoundException("Nope."));

        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
    }
}
