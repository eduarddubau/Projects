using Backend.Config;
using Backend.Data;
using Backend.Services;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Backend.Tests.Services;

public sealed class RefreshTokenServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly RefreshTokenService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public RefreshTokenServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options, Mock.Of<ICurrentUserService>());

        var jwt = Options.Create(new JwtOptions { RefreshTokenDurationInDays = 7 });
        _service = new RefreshTokenService(_context, jwt, Mock.Of<ILogger<RefreshTokenService>>());
    }

    [Fact]
    public async Task IssueAsync_PersistsHashedTokenAndReturnsRaw()
    {
        var raw = await _service.IssueAsync(_userId, TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(raw));
        var stored = await _context.RefreshTokens.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(_userId, stored.UserId);
        Assert.NotEqual(raw, stored.TokenHash);   // only the hash is stored, never the raw token
        Assert.Null(stored.RevokedAt);
        Assert.True(stored.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task RotateAsync_WithValidToken_RevokesOldAndIssuesNew()
    {
        var raw = await _service.IssueAsync(_userId, TestContext.Current.CancellationToken);

        var result = await _service.RotateAsync(raw, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(_userId, result.UserId);
        Assert.False(string.IsNullOrWhiteSpace(result.NewRawToken));
        Assert.NotEqual(raw, result.NewRawToken);

        var tokens = await _context.RefreshTokens.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens, t => t.RevokedAt != null);   // the presented token is revoked
        Assert.Single(tokens, t => t.RevokedAt == null);   // the replacement is active
    }

    [Fact]
    public async Task RotateAsync_WithUnknownToken_Fails()
    {
        var result = await _service.RotateAsync("not-a-real-token", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RotateAsync_WithExpiredToken_Fails()
    {
        var raw = await _service.IssueAsync(_userId, TestContext.Current.CancellationToken);
        var stored = await _context.RefreshTokens.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        stored.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.RotateAsync(raw, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RotateAsync_ReusingRevokedToken_RevokesEntireFamily()
    {
        var raw = await _service.IssueAsync(_userId, TestContext.Current.CancellationToken);
        var first = await _service.RotateAsync(raw, TestContext.Current.CancellationToken);   // raw is now revoked, a new token is active
        Assert.True(first.Succeeded);

        var reuse = await _service.RotateAsync(raw, TestContext.Current.CancellationToken);   // replaying the revoked token

        Assert.False(reuse.Succeeded);
        var active = await _context.RefreshTokens.CountAsync(t => t.UserId == _userId && t.RevokedAt == null, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, active);   // reuse detection revoked every active token
    }

    [Fact]
    public async Task RevokeAsync_RevokesTokenSoItCannotRotate()
    {
        var raw = await _service.IssueAsync(_userId, TestContext.Current.CancellationToken);

        await _service.RevokeAsync(raw, TestContext.Current.CancellationToken);

        var stored = await _context.RefreshTokens.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(stored.RevokedAt);

        var result = await _service.RotateAsync(raw, TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RevokeAsync_WithUnknownToken_IsNoOp()
    {
        await _service.RevokeAsync("nope", TestContext.Current.CancellationToken);

        Assert.Empty(await _context.RefreshTokens.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    public void Dispose() => _context.Dispose();
}
