using Hato.Modules.People.Domain;
using Xunit;

namespace Hato.Modules.People.UnitTests;

public class RefreshTokenTests
{
    [Fact]
    public void Create_ValidUserId_GeneratesRawTokenAndHash()
    {
        var userId = Guid.NewGuid();
        var (tokenEntity, rawToken) = RefreshToken.Create(userId, 30);

        Assert.Equal(userId, tokenEntity.UserId);
        Assert.False(string.IsNullOrWhiteSpace(rawToken));
        Assert.False(string.IsNullOrWhiteSpace(tokenEntity.TokenHash));
        Assert.True(tokenEntity.IsActive);
        Assert.False(tokenEntity.IsExpired);
        Assert.False(tokenEntity.IsRevoked);
    }

    [Fact]
    public void Revoke_ActiveToken_MarksTokenAsRevoked()
    {
        var (tokenEntity, _) = RefreshToken.Create(Guid.NewGuid());
        tokenEntity.Revoke("new_hash_123");

        Assert.True(tokenEntity.IsRevoked);
        Assert.False(tokenEntity.IsActive);
        Assert.Equal("new_hash_123", tokenEntity.ReplacedByTokenHash);
        Assert.NotNull(tokenEntity.RevokedAt);
    }

    [Fact]
    public void Create_EmptyUserId_ThrowsDomainException()
    {
        Assert.Throws<SharedKernel.DomainException>(() => RefreshToken.Create(Guid.Empty));
    }
}
