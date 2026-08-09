using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.AnimalGroups;

public record UpdateAnimalGroupCommand(
    Guid Id,
    string Name,
    string? Description,
    Guid? SpeciesId) : IRequest<AnimalGroupDto>;

public class UpdateAnimalGroupValidator : AbstractValidator<UpdateAnimalGroupCommand>
{
    public UpdateAnimalGroupValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}

public class UpdateAnimalGroupHandler(ILivestockDbContext dbContext)
    : IRequestHandler<UpdateAnimalGroupCommand, AnimalGroupDto>
{
    public async Task<AnimalGroupDto> Handle(UpdateAnimalGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await dbContext.AnimalGroups
            .Include(g => g.Memberships)
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"El grupo con ID '{request.Id}' no existe.");

        group.Update(request.Name, request.Description, request.SpeciesId);

        // Re-project with the now-current SpeciesId (could have changed).
        var speciesName = request.SpeciesId.HasValue
            ? await dbContext.Species
                .Where(s => s.Id == request.SpeciesId.Value)
                .Select(s => (string?)s.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var liveHeadCount = await GetAnimalGroupByIdHandler.ComputeLiveHeadCountAsync(
            dbContext, group.Id, group.Memberships.Count(m => m.IsActive), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AnimalGroupDto(
            group.Id,
            group.Name,
            group.Description,
            group.SpeciesId,
            speciesName,
            group.IsActive,
            group.TrackingMode,
            liveHeadCount,
            group.Memberships.Select(m => new GroupMembershipDto(m.Id, m.AnimalId, m.JoinedAt, m.LeftAt, m.IsActive)).ToList());
    }
}
