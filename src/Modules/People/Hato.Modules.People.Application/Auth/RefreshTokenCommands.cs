using FluentValidation;
using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.People.Application.Auth;

public record RefreshAuthTokenCommand(string RefreshToken) : IRequest<LoginResultDto>;

public class RefreshAuthTokenCommandValidator : AbstractValidator<RefreshAuthTokenCommand>
{
    public RefreshAuthTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class RefreshAuthTokenCommandHandler(IPeopleDbContext dbContext, IJwtTokenGenerator tokenGenerator)
    : IRequestHandler<RefreshAuthTokenCommand, LoginResultDto>
{
    public async Task<LoginResultDto> Handle(RefreshAuthTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = Domain.RefreshToken.HashToken(request.RefreshToken.Trim());

        var existingToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null || !existingToken.IsActive)
            throw new UnauthorizedAccessException("El token de refresco no es válido o ha sido revocado.");

        var user = await dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == existingToken.UserId, cancellationToken);

        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("El usuario no existe o está desactivado.");

        // Rotate token
        var (newTokenEntity, newRawRefreshToken) = Domain.RefreshToken.Create(user.Id);
        existingToken.Revoke(newTokenEntity.TokenHash);

        dbContext.RefreshTokens.Add(newTokenEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var (jwtToken, expiresAt) = tokenGenerator.GenerateToken(user);

        var roles = user.UserRoles
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role.Code)
            .Distinct()
            .ToList();

        var permissions = user.UserRoles
            .Where(ur => ur.Role != null)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Where(rp => rp.Permission != null)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        return new LoginResultDto(jwtToken, newRawRefreshToken, expiresAt, user.Id, user.FullName, roles, permissions);
    }
}

public record RevokeRefreshTokenCommand(string RefreshToken) : IRequest;

public class RevokeRefreshTokenCommandValidator : AbstractValidator<RevokeRefreshTokenCommand>
{
    public RevokeRefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class RevokeRefreshTokenCommandHandler(IPeopleDbContext dbContext)
    : IRequestHandler<RevokeRefreshTokenCommand>
{
    public async Task Handle(RevokeRefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = Domain.RefreshToken.HashToken(request.RefreshToken.Trim());

        var token = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token != null && token.IsActive)
        {
            token.Revoke();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
