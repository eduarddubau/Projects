namespace Backend.Services.Interfaces;

/// <summary>Outcome of a rotation: the owning user and the new raw token on success.</summary>
public record RefreshRotationResult(bool Succeeded, Guid UserId = default, string? NewRawToken = null)
{
    public static readonly RefreshRotationResult Failure = new(false);
}

public interface IRefreshTokenService
{
    /// <summary>Issues a new refresh token and returns its raw value.</summary>
    Task<string> IssueAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Validates and rotates a refresh token. Reusing a revoked token revokes
    /// the user's whole token family and fails.</summary>
    Task<RefreshRotationResult> RotateAsync(string rawToken, CancellationToken ct = default);

    /// <summary>Revokes a refresh token; no-op if unknown or already revoked.</summary>
    Task RevokeAsync(string rawToken, CancellationToken ct = default);
}
