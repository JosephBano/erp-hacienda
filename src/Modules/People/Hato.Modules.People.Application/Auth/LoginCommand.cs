using FluentValidation;
using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.People.Application.Auth;

public record LoginCommand(string Email, string Password) : IRequest<LoginResultDto>;

public record LoginResultDto(string Token, DateTimeOffset ExpiresAt, Guid UserId, string FullName, UserRole Role);

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
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive || !user.VerifyPassword(request.Password))
            throw new UnauthorizedAccessException("Correo o contraseña incorrectos.");

        var (token, expiresAt) = tokenGenerator.GenerateToken(user);
        return new LoginResultDto(token, expiresAt, user.Id, user.FullName, user.Role);
    }
}
