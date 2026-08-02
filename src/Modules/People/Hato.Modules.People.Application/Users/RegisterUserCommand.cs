using FluentValidation;
using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.People.Application.Users;

public record RegisterUserCommand(string FullName, string Email, string Password, UserRole Role) : IRequest<Guid>;

public class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}

public class RegisterUserHandler(IPeopleDbContext dbContext)
    : IRequestHandler<RegisterUserCommand, Guid>
{
    public async Task<Guid> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var existingEmail = await dbContext.Users
            .AnyAsync(u => u.Email == request.Email.Trim().ToLowerInvariant(), cancellationToken);

        if (existingEmail)
            throw new DomainException($"El correo '{request.Email}' ya se encuentra registrado.");

        var passwordHash = PasswordHasher.Hash(request.Password);

        var user = User.Create(request.FullName, request.Email, passwordHash, request.Role);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}

public record UserDto(Guid Id, string FullName, string Email, UserRole Role, bool IsActive);

public record GetUsersQuery() : IRequest<List<UserDto>>;

public class GetUsersHandler(IPeopleDbContext dbContext)
    : IRequestHandler<GetUsersQuery, List<UserDto>>
{
    public async Task<List<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await dbContext.Users.AsNoTracking().ToListAsync(cancellationToken);

        return users.Select(u => new UserDto(u.Id, u.FullName, u.Email, u.Role, u.IsActive)).ToList();
    }
}
