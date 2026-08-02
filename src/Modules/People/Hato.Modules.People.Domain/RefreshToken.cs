using System.Security.Cryptography;
using Hato.SharedKernel;

namespace Hato.Modules.People.Domain;

/// <summary>
/// Refresh token entity for mobile offline and persistent web sessions (Art. 4 + Art. 10).
/// Supports automatic token rotation and revocation.
/// </summary>
public class RefreshToken : AuditableEntity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string? CreatedByIp { get; private set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken()
    {
        TokenHash = null!;
    }

    private RefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAt, string? createdByIp)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
    }

    public static (RefreshToken Entity, string RawToken) Create(Guid userId, int expiryDays = 30, string? createdByIp = null)
    {
        if (userId == Guid.Empty)
            throw new DomainException("El refresh token debe estar asociado a un usuario válido.");

        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(randomBytes);
        var tokenHash = HashToken(rawToken);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(expiryDays);

        var entity = new RefreshToken(userId, tokenHash, expiresAt, createdByIp);
        return (entity, rawToken);
    }

    public static string HashToken(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            throw new DomainException("El token no puede estar vacío.");

        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }

    public void Revoke(string? replacedByTokenHash = null)
    {
        if (IsRevoked) return;

        RevokedAt = DateTimeOffset.UtcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
