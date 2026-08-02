using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.Kpis;

public record DamKpisDto(
    Guid DamId,
    double? AverageCalvingIntervalDays,
    int? DaysOpen,
    double ServicesPerConception,
    int TotalBirthings,
    int TotalOffspringAlive,
    double WeanedPerYear
);

public record GetDamKpisQuery(Guid DamId) : IRequest<DamKpisDto>;

public class GetDamKpisQueryHandler(IBreedingDbContext dbContext)
    : IRequestHandler<GetDamKpisQuery, DamKpisDto>
{
    public async Task<DamKpisDto> Handle(GetDamKpisQuery request, CancellationToken cancellationToken)
    {
        var birthings = await dbContext.Birthings
            .AsNoTracking()
            .Where(b => b.DamId == request.DamId)
            .OrderBy(b => b.BirthDate)
            .ToListAsync(cancellationToken);

        double? avgCalvingInterval = null;
        if (birthings.Count > 1)
        {
            var intervals = new List<int>();
            for (int i = 1; i < birthings.Count; i++)
            {
                var diff = birthings[i].BirthDate.DayNumber - birthings[i - 1].BirthDate.DayNumber;
                intervals.Add(diff);
            }
            avgCalvingInterval = intervals.Average();
        }

        int? daysOpen = null;
        var lastBirthing = birthings.LastOrDefault();
        if (lastBirthing is not null)
        {
            var activePregnancy = await dbContext.Pregnancies
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.DamId == request.DamId && p.Status == PregnancyStatus.Active, cancellationToken);

            if (activePregnancy is not null)
            {
                daysOpen = activePregnancy.ConfirmedAt.DayNumber - lastBirthing.BirthDate.DayNumber;
            }
            else
            {
                daysOpen = DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - lastBirthing.BirthDate.DayNumber;
            }
        }

        var totalServices = await dbContext.BreedingServices
            .AsNoTracking()
            .CountAsync(s => s.DamId == request.DamId, cancellationToken);

        var totalPregnancies = await dbContext.Pregnancies
            .AsNoTracking()
            .CountAsync(p => p.DamId == request.DamId, cancellationToken);

        double servicesPerConception = totalPregnancies > 0 ? (double)totalServices / totalPregnancies : totalServices;

        int totalAlive = birthings.Sum(b => b.BornAlive);

        double weanedPerYear = 0;
        if (birthings.Count > 0)
        {
            var totalWeaned = birthings.Sum(b => b.WeanedCount ?? 0);
            var yearsSpan = Math.Max(
                1.0,
                (DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - birthings[0].BirthDate.DayNumber) / 365.25);
            weanedPerYear = Math.Round(totalWeaned / yearsSpan, 2);
        }

        return new DamKpisDto(
            request.DamId,
            avgCalvingInterval,
            daysOpen,
            Math.Round(servicesPerConception, 2),
            birthings.Count,
            totalAlive,
            weanedPerYear
        );
    }
}
