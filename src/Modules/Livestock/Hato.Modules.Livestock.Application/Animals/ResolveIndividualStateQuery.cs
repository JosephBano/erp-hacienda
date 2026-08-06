using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Animals;

/// <summary>
/// The one function ADR-0015 sec.6 asks for: every caller that needs to know whether an
/// animal is alive goes through here, instead of each screen inventing its own guess about
/// what a <see cref="TrackingMode.Headcount"/> membership means.
/// </summary>
public record ResolveIndividualStateQuery(Guid AnimalId, DateOnly AsOf) : IRequest<IndividualState>;

public class ResolveIndividualStateHandler(ILivestockDbContext dbContext)
    : IRequestHandler<ResolveIndividualStateQuery, IndividualState>
{
    public async Task<IndividualState> Handle(ResolveIndividualStateQuery request, CancellationToken cancellationToken)
    {
        var animal = await dbContext.Animals
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == request.AnimalId, cancellationToken);

        if (animal is null)
            throw new DomainException($"El animal con ID '{request.AnimalId}' no existe.");

        if (animal.DisposedAt is not null)
            return IndividualState.Disposed;

        // The membership active as of the requested date, if any. An animal outside any
        // group — or in a group that tracks individuals — is answered normally: Alive
        // unless it was closed above.
        var membershipGroupId = await dbContext.GroupMemberships
            .Where(m => m.AnimalId == request.AnimalId
                     && m.JoinedAt <= request.AsOf
                     && (m.LeftAt == null || m.LeftAt >= request.AsOf))
            .OrderByDescending(m => m.JoinedAt)
            .Select(m => (Guid?)m.GroupId)
            .FirstOrDefaultAsync(cancellationToken);

        if (membershipGroupId is not { } groupId)
            return IndividualState.Alive;

        var trackingMode = await dbContext.AnimalGroups
            .Where(g => g.Id == groupId)
            .Select(g => g.TrackingMode)
            .FirstOrDefaultAsync(cancellationToken);

        return trackingMode == TrackingMode.Headcount
            ? IndividualState.Indeterminate
            : IndividualState.Alive;
    }
}
