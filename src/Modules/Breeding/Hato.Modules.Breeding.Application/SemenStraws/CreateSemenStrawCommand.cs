using FluentValidation;
using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Contracts;
using Hato.Modules.Breeding.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.SemenStraws;

public record CreateSemenStrawCommand(
    string Code,
    string BullName,
    Guid BreedId,
    int Quantity,
    string? BullCode = null,
    string? SupplierName = null,
    string? Notes = null
) : IRequest<SemenStrawDto>;

public class CreateSemenStrawCommandValidator : AbstractValidator<CreateSemenStrawCommand>
{
    public CreateSemenStrawCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.BullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BreedId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

public class CreateSemenStrawCommandHandler(IBreedingDbContext dbContext)
    : IRequestHandler<CreateSemenStrawCommand, SemenStrawDto>
{
    public async Task<SemenStrawDto> Handle(CreateSemenStrawCommand request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.SemenStraws
            .AnyAsync(s => s.Code == request.Code, cancellationToken);

        if (existing)
            throw new InvalidOperationException($"A semen straw with code '{request.Code}' already exists.");

        var straw = SemenStraw.Create(
            request.Code,
            request.BullName,
            request.BreedId,
            request.Quantity,
            request.BullCode,
            request.SupplierName,
            request.Notes
        );

        dbContext.SemenStraws.Add(straw);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SemenStrawDto(
            straw.Id,
            straw.Code,
            straw.BullName,
            straw.BullCode,
            straw.BreedId,
            straw.SupplierName,
            straw.InitialQuantity,
            straw.CurrentQuantity,
            straw.Notes,
            straw.CreatedAt
        );
    }
}
