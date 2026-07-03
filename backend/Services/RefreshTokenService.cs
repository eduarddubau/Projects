using System.Security.Cryptography;
using System.Text;
using Backend.Config;
using Backend.Data;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _options;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(AppDbContext db, IOptions<JwtOptions> options, ILogger<RefreshTokenService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> IssueAsync(Guid userId, CancellationToken ct = default)
    {
        var (raw, hash) = GenerateToken();
        _db.RefreshTokens.Add(NewToken(userId, hash));
        await _db.SaveChangesAsync(ct);
        return raw;
    }

    public async Task<RefreshRotationResult> RotateAsync(string rawToken, CancellationToken ct = default)
    {
        var hash = Hash(rawToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);

        if (token is null)
        {
            _logger.LogWarning("Refresh rejected: token not recognized.");
            return RefreshRotationResult.Failure;
        }

        // A revoked token reused signals theft: revoke the user's whole token family.
        if (token.RevokedAt is not null)
        {
            _logger.LogWarning(
                "Refresh rejected: reuse of a revoked token detected for user {UserId}. Revoking all active tokens.",
                token.UserId);
            await RevokeAllActiveForUserAsync(token.UserId, ct);
            return RefreshRotationResult.Failure;
        }

        if (DateTime.UtcNow >= token.ExpiresAt)
        {
            _logger.LogInformation("Refresh rejected: expired token for user {UserId}.", token.UserId);
            return RefreshRotationResult.Failure;
        }

        // Rotate: revoke the current token and issue a replacement in one save.
        var (raw, newHash) = GenerateToken();
        token.RevokedAt = DateTime.UtcNow;
        token.ReplacedByTokenHash = newHash;
        _db.RefreshTokens.Add(NewToken(token.UserId, newHash));
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Rotated refresh token for user {UserId}.", token.UserId);
        return new RefreshRotationResult(true, token.UserId, raw);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken ct = default)
    {
        var hash = Hash(rawToken);
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
        var active = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var t in active)
            t.RevokedAt = now;

        await _db.SaveChangesAsync(ct);
    }

    private RefreshToken NewToken(Guid userId, string hash) => new()
    {
        UserId = userId,
        TokenHash = hash,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDurationInDays)
    };

    private static (string raw, string hash) GenerateToken()
    {
        var raw = Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        return (raw, Hash(raw));
    }

    // High-entropy token, so a fast hash suffices; only the hash is ever stored.
    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
