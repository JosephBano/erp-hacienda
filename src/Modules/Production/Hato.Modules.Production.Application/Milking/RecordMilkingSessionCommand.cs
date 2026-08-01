using FluentValidation;
using Hato.Modules.Production.Application.Abstractions;
using Hato.Modules.Production.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Production.Application.Milking;

public record IndividualYieldItem(Guid AnimalId, decimal Liters);

public record RecordMilkingSessionCommand(
    DateOnly Date,
    MilkingShift Shift,
    string RecordedBy,
    decimal TotalLiters,
    Guid? GroupId = null,
    string? Notes = null,
    List<IndividualYieldItem>? IndividualYields = null) : IRequest<Guid>;

public class RecordMilkingSessionValidator : AbstractValidator<RecordMilkingSessionCommand>
{
    public RecordMilkingSessionValidator()
    {
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.RecordedBy).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TotalLiters).GreaterThanOrEqualTo(0);
    }
}

public class RecordMilkingSessionHandler(IProductionDbContext dbContext)
    : IRequestHandler<RecordMilkingSessionCommand, Guid>
{
    public async Task<Guid> Handle(RecordMilkingSessionCommand request, CancellationToken cancellationToken)
    {
        var session = MilkingSession.Create(
            request.Date,
            request.Shift,
            request.RecordedBy,
            request.TotalLiters,
            request.GroupId,
            request.Notes);

        if (request.IndividualYields is not null && request.IndividualYields.Count > 0)
        {
            foreach (var item in request.IndividualYields)
            {
                var y = session.RecordAnimalYield(item.AnimalId, item.Liters);
                dbContext.MilkYields.Add(y);
            }
        }

        dbContext.MilkingSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return session.Id;
    }
}
