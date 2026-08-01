using Hato.Modules.People.Domain;

namespace Hato.Modules.People.Application.Abstractions;

public interface IJwtTokenGenerator
{
    (string Token, DateTimeOffset ExpiresAt) GenerateToken(User user);
}
