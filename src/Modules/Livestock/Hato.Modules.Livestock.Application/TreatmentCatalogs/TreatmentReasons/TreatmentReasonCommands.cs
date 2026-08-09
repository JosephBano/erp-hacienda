using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.TreatmentCatalogs.TreatmentReasons;

public record TreatmentReasonDto(Guid Id, string Key, string LabelEs, bool IsActive);

public record CreateTreatmentReasonCommand(string Key, string LabelEs) : IRequest<Guid>;

public class CreateTreatmentReasonValidator : AbstractValidator<CreateTreatmentReasonCommand>
{
    public CreateTreatmentReasonValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LabelEs).NotEmpty().MaximumLength(100);
    }
}

public class CreateTreatmentReasonHandler(ILivestockDbContext dbContext)
    : IRequestHandler<CreateTreatmentReasonCommand, Guid>
{
    public async Task<Guid> Handle(CreateTreatmentReasonCommand request, CancellationToken cancellationToken)
    {
        var reason = TreatmentReason.Create(request.Key, request.LabelEs);

        var keyTaken = await dbContext.TreatmentReasons
            .AsNoTracking()
            .AnyAsync(r => r.Key == reason.Key, cancellationToken);

        if (keyTaken)
            throw new DomainException($"Ya existe un motivo de tratamiento con la clave '{reason.Key}'.");

        dbContext.TreatmentReasons.Add(reason);
        await dbContext.SaveChangesAsync(cancellationToken);

        return reason.Id;
    }
}

public record DeactivateTreatmentReasonCommand(Guid Id) : IRequest;

public class DeactivateTreatmentReasonHandler(ILivestockDbContext dbContext)
    : IRequestHandler<DeactivateTreatmentReasonCommand>
{
    public async Task Handle(DeactivateTreatmentReasonCommand request, CancellationToken cancellationToken)
    {
        var reason = await dbContext.TreatmentReasons
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (reason is null)
            throw new DomainException($"El motivo de tratamiento con ID '{request.Id}' no existe.");

        reason.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record ActivateTreatmentReasonCommand(Guid Id) : IRequest;

public class ActivateTreatmentReasonHandler(ILivestockDbContext dbContext)
    : IRequestHandler<ActivateTreatmentReasonCommand>
{
    public async Task Handle(ActivateTreatmentReasonCommand request, CancellationToken cancellationToken)
    {
        var reason = await dbContext.TreatmentReasons
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (reason is null)
            throw new DomainException($"El motivo de tratamiento con ID '{request.Id}' no existe.");

        reason.Activate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record UpdateTreatmentReasonLabelCommand(Guid Id, string LabelEs) : IRequest;

public class UpdateTreatmentReasonLabelValidator : AbstractValidator<UpdateTreatmentReasonLabelCommand>
{
    public UpdateTreatmentReasonLabelValidator()
    {
        RuleFor(x => x.LabelEs).NotEmpty().MaximumLength(100);
    }
}

public class UpdateTreatmentReasonLabelHandler(ILivestockDbContext dbContext)
    : IRequestHandler<UpdateTreatmentReasonLabelCommand>
{
    public async Task Handle(UpdateTreatmentReasonLabelCommand request, CancellationToken cancellationToken)
    {
        var reason = await dbContext.TreatmentReasons
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (reason is null)
            throw new DomainException($"El motivo de tratamiento con ID '{request.Id}' no existe.");

        reason.UpdateLabel(request.LabelEs);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record GetTreatmentReasonsQuery(bool IncludeInactive = false)
    : IRequest<List<TreatmentReasonDto>>;

public class GetTreatmentReasonsHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetTreatmentReasonsQuery, List<TreatmentReasonDto>>
{
    public async Task<List<TreatmentReasonDto>> Handle(
        GetTreatmentReasonsQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.TreatmentReasons.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(r => r.IsActive);

        return await query
            .OrderBy(r => r.LabelEs)
            .Select(r => new TreatmentReasonDto(r.Id, r.Key, r.LabelEs, r.IsActive))
            .ToListAsync(cancellationToken);
    }
}