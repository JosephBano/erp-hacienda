using FluentValidation;
using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.People.Application.Roles;

public record PermissionDto(Guid Id, string Code, string Name, string Module, string Description);

public record RoleDto(Guid Id, string Code, string Name, string Description, bool IsSystem, List<PermissionDto> Permissions);

public record GetPermissionsQuery() : IRequest<List<PermissionDto>>;

public class GetPermissionsHandler(IPeopleDbContext dbContext)
    : IRequestHandler<GetPermissionsQuery, List<PermissionDto>>
{
    public async Task<List<PermissionDto>> Handle(GetPermissionsQuery request, CancellationToken cancellationToken)
    {
        var permissions = await dbContext.Permissions.AsNoTracking().ToListAsync(cancellationToken);
        return permissions.Select(p => new PermissionDto(p.Id, p.Code, p.Name, p.Module, p.Description)).ToList();
    }
}

public record GetRolesQuery() : IRequest<List<RoleDto>>;

public class GetRolesHandler(IPeopleDbContext dbContext)
    : IRequestHandler<GetRolesQuery, List<RoleDto>>
{
    public async Task<List<RoleDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .ToListAsync(cancellationToken);

        return roles.Select(r => new RoleDto(
            r.Id,
            r.Code,
            r.Name,
            r.Description,
            r.IsSystem,
            r.RolePermissions
                .Where(rp => rp.Permission != null)
                .Select(rp => new PermissionDto(rp.Permission.Id, rp.Permission.Code, rp.Permission.Name, rp.Permission.Module, rp.Permission.Description))
                .ToList())).ToList();
    }
}

public record CreateRoleCommand(string Code, string Name, string Description, List<Guid>? PermissionIds = null) : IRequest<Guid>;

public class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class CreateRoleHandler(IPeopleDbContext dbContext) : IRequestHandler<CreateRoleCommand, Guid>
{
    public async Task<Guid> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Code.Trim().ToLowerInvariant();
        var exists = await dbContext.Roles.AnyAsync(r => r.Code == normalizedCode, cancellationToken);
        if (exists)
            throw new DomainException($"El rol con código '{request.Code}' ya existe.");

        var role = Role.Create(normalizedCode, request.Name, request.Description, isSystem: false);

        if (request.PermissionIds != null && request.PermissionIds.Count > 0)
        {
            var permissions = await dbContext.Permissions
                .Where(p => request.PermissionIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            foreach (var permission in permissions)
            {
                role.AddPermission(permission);
            }
        }

        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        return role.Id;
    }
}

public record UpdateRoleCommand(Guid RoleId, string Name, string Description, List<Guid>? PermissionIds = null) : IRequest;

public class UpdateRoleHandler(IPeopleDbContext dbContext) : IRequestHandler<UpdateRoleCommand>
{
    public async Task Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new DomainException($"El rol con ID '{request.RoleId}' no existe.");

        role.UpdateDetails(request.Name, request.Description);

        if (request.PermissionIds != null)
        {
            var existingPermissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToList();
            var toRemove = existingPermissionIds.Except(request.PermissionIds).ToList();
            foreach (var permId in toRemove)
            {
                role.RemovePermission(permId);
            }

            var toAdd = request.PermissionIds.Except(existingPermissionIds).ToList();
            if (toAdd.Count > 0)
            {
                var permissions = await dbContext.Permissions
                    .Where(p => toAdd.Contains(p.Id))
                    .ToListAsync(cancellationToken);

                foreach (var perm in permissions)
                {
                    role.AddPermission(perm);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record AssignUserRoleCommand(Guid UserId, Guid RoleId) : IRequest;

public class AssignUserRoleHandler(IPeopleDbContext dbContext) : IRequestHandler<AssignUserRoleCommand>
{
    public async Task Handle(AssignUserRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new DomainException($"El usuario con ID '{request.UserId}' no existe.");

        var role = await dbContext.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new DomainException($"El rol con ID '{request.RoleId}' no existe.");

        user.AddRole(role);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
