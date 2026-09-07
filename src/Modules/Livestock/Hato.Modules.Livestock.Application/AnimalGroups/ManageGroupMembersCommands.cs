using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.AnimalGroups;

public record AddGroupMemberCommand(Guid GroupId, Guid AnimalId, DateOnly JoinedAt) : IRequest;

public class AddGroupMemberValidator : AbstractValidator<AddGroupMemberCommand>
{
    public AddGroupMemberValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.JoinedAt).NotEmpty();
    }
}

public class AddGroupMemberHandler(ILivestockDbContext dbContext) : IRequestHandler<AddGroupMemberCommand>
{
    public async Task Handle(AddGroupMemberCommand request, CancellationToken cancellationToken)
    {
        var group = await dbContext.AnimalGroups
            .Include(g => g.Memberships)
            .FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);

        if (group is null)
            throw new DomainException($"El grupo con ID '{request.GroupId}' no existe.");

        if (!group.IsActive)
            throw new DomainException("No se pueden agregar miembros a un grupo inactivo.");

        var animal = await dbContext.Animals
            .FirstOrDefaultAsync(a => a.Id == request.AnimalId && a.DeletedAt == null, cancellationToken);
        if (animal is null)
            throw new DomainException($"El animal con ID '{request.AnimalId}' no existe.");

        if (animal.DisposedAt is { } disposedAt)
        {
            var disposedDate = DateOnly.FromDateTime(disposedAt.UtcDateTime);
            if (disposedDate <= request.JoinedAt)
            {
                throw new DomainException("El animal fue dado de baja y no puede ser agregado a un grupo.");
            }
        }

        var membership = group.AddMember(request.AnimalId, request.JoinedAt);
        dbContext.GroupMemberships.Add(membership);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record RemoveGroupMemberCommand(Guid GroupId, Guid AnimalId, DateOnly LeftAt) : IRequest;

public class RemoveGroupMemberValidator : AbstractValidator<RemoveGroupMemberCommand>
{
    public RemoveGroupMemberValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.LeftAt).NotEmpty();
    }
}

public class RemoveGroupMemberHandler(ILivestockDbContext dbContext) : IRequestHandler<RemoveGroupMemberCommand>
{
    public async Task Handle(RemoveGroupMemberCommand request, CancellationToken cancellationToken)
    {
        var group = await dbContext.AnimalGroups
            .Include(g => g.Memberships)
            .FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);

        if (group is null)
            throw new DomainException($"El grupo con ID '{request.GroupId}' no existe.");

        group.RemoveMember(request.AnimalId, request.LeftAt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
