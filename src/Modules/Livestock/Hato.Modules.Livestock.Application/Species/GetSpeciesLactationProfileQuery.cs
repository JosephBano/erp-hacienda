using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Species;

/// <summary>
/// Read-side of the lactation parameters so the admin-web panel can show the current
/// values before issuing the PATCH. Companion to
/// <see cref="UpdateSpeciesLactationCommand"/>. The query returns null when the species
/// does not exist so the endpoint can answer 404 without a try/catch on the caller side.
/// </summary>
public record GetSpeciesLactationProfileQuery(Guid SpeciesId) : IRequest<SpeciesLactationProfile?>;

public class GetSpeciesLactationProfileHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetSpeciesLactationProfileQuery, SpeciesLactationProfile?>
{
    public async Task<SpeciesLactationProfile?> Handle(
        GetSpeciesLactationProfileQuery request,
        CancellationToken cancellationToken)
    {
        return await dbContext.Species
            .AsNoTracking()
            .Where(s => s.Id == request.SpeciesId)
            .Select(s => new SpeciesLactationProfile(
                s.Id,
                s.DaysOfLactation,
                s.CohortWindowDays))
            .FirstOrDefaultAsync(cancellationToken);
    }
}