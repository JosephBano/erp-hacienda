using FluentValidation;
using Hato.Modules.Inventory.Application.Abstractions;
using Hato.Modules.Inventory.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Inventory.Application.Items;

/// <summary>
/// docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.5 task 1: a per-item unit conversion lives in the
/// database so the field-app can record feed consumption in the operator's unit
/// (a 40-kg sack, a 25-kg bag) and the cost engine can still read the kg number
/// without an <c>if</c> per presentation (Art. 8). One row per (from_unit, to_unit) pair;
/// the inverse lives on its own row.
/// </summary>
public record RegisterUnitConversionCommand(
    Guid InventoryItemId,
    string FromUnit,
    string ToUnit,
    decimal Factor) : IRequest<Guid>;

public class RegisterUnitConversionValidator : AbstractValidator<RegisterUnitConversionCommand>
{
    public RegisterUnitConversionValidator()
    {
        RuleFor(x => x.InventoryItemId).NotEmpty();
        RuleFor(x => x.FromUnit).NotEmpty().MaximumLength(20);
        RuleFor(x => x.ToUnit).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Factor).GreaterThan(0);
    }
}

public class RegisterUnitConversionHandler(IInventoryDbContext dbContext)
    : IRequestHandler<RegisterUnitConversionCommand, Guid>
{
    public async Task<Guid> Handle(
        RegisterUnitConversionCommand request,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == request.InventoryItemId, cancellationToken)
            ?? throw new DomainException(
                $"El ítem de inventario con ID '{request.InventoryItemId}' no existe.");

        var conversion = UnitConversion.Register(
            request.InventoryItemId, request.FromUnit, request.ToUnit, request.Factor);

        dbContext.UnitConversions.Add(conversion);
        await dbContext.SaveChangesAsync(cancellationToken);

        return conversion.Id;
    }
}