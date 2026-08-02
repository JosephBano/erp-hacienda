using FluentValidation;
using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.People.Application.Users;

public record RegisterUserCommand(
    string FullName,
    string Email,
    string Password,
    List<string>? RoleCodes = null,
    string? Role = null) : IRequest<Guid>;

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
        var user = User.Create(request.FullName, request.Email, passwordHash);

        var requestedRoleCodes = new List<string>();
        if (request.RoleCodes != null && request.RoleCodes.Count > 0)
        {
            requestedRoleCodes.AddRange(request.RoleCodes);
        }
        else if (!string.IsNullOrWhiteSpace(request.Role))
        {
            requestedRoleCodes.Add(request.Role);
        }
        else
        {
            requestedRoleCodes.Add(SystemRoles.Registrar);
        }

        foreach (var roleCode in requestedRoleCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var normalized = roleCode.Trim().ToLowerInvariant();
            if (int.TryParse(normalized, out var enumVal))
            {
                normalized = enumVal switch
                {
                    0 => SystemRoles.Admin,
                    1 => SystemRoles.Registrar,
                    2 => SystemRoles.Veterinarian,
                    _ => normalized
                };
            }

            var role = await dbContext.Roles.FirstOrDefaultAsync(r => r.Code == normalized, cancellationToken);
            if (role is null)
            {
                var fallbackCode = normalized switch
                {
                    "admin" or "administrator" => SystemRoles.Admin,
                    "registrar" => SystemRoles.Registrar,
                    "veterinarian" or "vet" => SystemRoles.Veterinarian,
                    _ => normalized
                };

                role = await dbContext.Roles.FirstOrDefaultAsync(r => r.Code == fallbackCode, cancellationToken)
                    ?? throw new DomainException($"El rol '{roleCode}' no existe.");
            }

            user.AddRole(role);
        }

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}

public record UserDto(Guid Id, string FullName, string Email, List<string> Roles, bool IsActive);

public record GetUsersQuery() : IRequest<List<UserDto>>;

public class GetUsersHandler(IPeopleDbContext dbContext)
    : IRequestHandler<GetUsersQuery, List<UserDto>>
{
    public async Task<List<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .ToListAsync(cancellationToken);

        return users.Select(u => new UserDto(
            u.Id,
            u.FullName,
            u.Email,
            u.UserRoles.Where(ur => ur.Role != null).Select(ur => ur.Role.Code).Distinct().ToList(),
            u.IsActive)).ToList();
    }
}
