using Backend.Config;
using Backend.Data;
using Backend.Models;
using Backend.Security;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public partial class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _options;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(
        AppDbContext db,
        IOptions<JwtOptions> options,
        ILogger<RefreshTokenService> logger
    )
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> IssueAsync(Guid userId, CancellationToken ct = default)
    {
        var (raw, hash) = SecureToken.Generate();
        _db.RefreshTokens.Add(NewToken(userId, hash));
        await _db.SaveChangesAsync(ct);
        return raw;
    }

    public async Task<RefreshRotationResult> RotateAsync(
        string rawToken,
        CancellationToken ct = default
    )
    {
        var hash = SecureToken.Hash(rawToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);

        if (token is null)
        {
            LogTokenNotRecognized();
            return RefreshRotationResult.Failure;
        }

        // A revoked token reused signals theft: revoke the user's whole token family.
        if (token.RevokedAt is not null)
        {
            LogRevokedTokenReused(token.UserId);
            await RevokeAllActiveForUserAsync(token.UserId, ct);
            return RefreshRotationResult.Failure;
        }

        if (DateTime.UtcNow >= token.ExpiresAt)
        {
            LogTokenExpired(token.UserId);
            return RefreshRotationResult.Failure;
        }

        // Rotate: revoke the current token and issue a replacement in one save.
        var (raw, newHash) = SecureToken.Generate();
        token.RevokedAt = DateTime.UtcNow;
        token.ReplacedByTokenHash = newHash;
        _db.RefreshTokens.Add(NewToken(token.UserId, newHash));
        await _db.SaveChangesAsync(ct);

        LogTokenRotated(token.UserId);
        return new RefreshRotationResult(true, token.UserId, raw);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken ct = default)
    {
        var hash = SecureToken.Hash(rawToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);
        if (token is { RevokedAt: null })
        {
            token.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task RevokeAllActiveForUserAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var active = await _db
            .RefreshTokens.Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var t in active)
            t.RevokedAt = now;

        await _db.SaveChangesAsync(ct);
    }

    private RefreshToken NewToken(Guid userId, string hash) =>
        new()
        {
            UserId = userId,
            TokenHash = hash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDurationInDays),
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh rejected: token not recognized.")]
    private partial void LogTokenNotRecognized();

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Refresh rejected: reuse of a revoked token detected for user {userId}. Revoking all active tokens."
    )]
    private partial void LogRevokedTokenReused(Guid userId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Refresh rejected: expired token for user {userId}."
    )]
    private partial void LogTokenExpired(Guid userId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Rotated refresh token for user {userId}."
    )]
    private partial void LogTokenRotated(Guid userId);
}
