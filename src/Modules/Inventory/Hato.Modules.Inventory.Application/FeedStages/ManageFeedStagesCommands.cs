using FluentValidation;
using Hato.Modules.Inventory.Application.Abstractions;
using Hato.Modules.Inventory.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Inventory.Application.FeedStages;

public record CreateFeedStageCommand(string Key, string LabelEs) : IRequest<Guid>;
public class CreateFeedStageValidator : AbstractValidator<CreateFeedStageCommand>
{
    public CreateFeedStageValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LabelEs).NotEmpty().MaximumLength(100);
    }
}
public class CreateFeedStageHandler(IInventoryDbContext dbContext) : IRequestHandler<CreateFeedStageCommand, Guid>
{
    public async Task<Guid> Handle(CreateFeedStageCommand request, CancellationToken cancellationToken)
    {
        var stage = FeedStage.Create(request.Key, request.LabelEs);
        dbContext.FeedStages.Add(stage);
        await dbContext.SaveChangesAsync(cancellationToken);
        return stage.Id;
    }
}

public record DeactivateFeedStageCommand(Guid FeedStageId) : IRequest;
public class DeactivateFeedStageHandler(IInventoryDbContext dbContext) : IRequestHandler<DeactivateFeedStageCommand>
{
    public async Task Handle(DeactivateFeedStageCommand request, CancellationToken cancellationToken)
    {
        var stage = await dbContext.FeedStages.FirstOrDefaultAsync(s => s.Id == request.FeedStageId, cancellationToken)
            ?? throw new KeyNotFoundException($"La etapa de alimento con ID '{request.FeedStageId}' no existe.");
        stage.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record ActivateFeedStageCommand(Guid FeedStageId) : IRequest;
public class ActivateFeedStageHandler(IInventoryDbContext dbContext) : IRequestHandler<ActivateFeedStageCommand>
{
    public async Task Handle(ActivateFeedStageCommand request, CancellationToken cancellationToken)
    {
        var stage = await dbContext.FeedStages.FirstOrDefaultAsync(s => s.Id == request.FeedStageId, cancellationToken)
            ?? throw new KeyNotFoundException($"La etapa de alimento con ID '{request.FeedStageId}' no existe.");
        stage.Activate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
