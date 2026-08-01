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

        var animalExists = await dbContext.Animals.AnyAsync(a => a.Id == request.AnimalId, cancellationToken);
        if (!animalExists)
            throw new DomainException($"El animal con ID '{request.AnimalId}' no existe.");

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
