using FluentValidation;
using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.People.Application.Auth;

public record LoginCommand(string Email, string Password) : IRequest<LoginResultDto>;

public record LoginResultDto(
    string Token,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string FullName,
    List<string> Roles,
    List<string> Permissions);

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginCommandHandler(IPeopleDbContext dbContext, IJwtTokenGenerator tokenGenerator)
    : IRequestHandler<LoginCommand, LoginResultDto>
{
    public async Task<LoginResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive || !user.VerifyPassword(request.Password))
            throw new UnauthorizedAccessException("Correo o contraseña incorrectos.");

        var (token, expiresAt) = tokenGenerator.GenerateToken(user);
        var (refreshTokenEntity, rawRefreshToken) = Domain.RefreshToken.Create(user.Id);

        dbContext.RefreshTokens.Add(refreshTokenEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

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

        return new LoginResultDto(token, rawRefreshToken, expiresAt, user.Id, user.FullName, roles, permissions);
    }
}
