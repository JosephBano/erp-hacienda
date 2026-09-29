using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Application.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Delivery.Application.BuildRequests.Queries;

public record GetBuildRequestByIdQuery(Guid Id) : IRequest<MobileBuildRequestDto?>;

public record ListBuildRequestsQuery(string? Channel = null, string? Status = null)
    : IRequest<IReadOnlyCollection<MobileBuildRequestDto>>;

public class BuildRequestQueriesHandler(IDeliveryDbContext dbContext)
    : IRequestHandler<GetBuildRequestByIdQuery, MobileBuildRequestDto?>,
      IRequestHandler<ListBuildRequestsQuery, IReadOnlyCollection<MobileBuildRequestDto>>
{
    public async Task<MobileBuildRequestDto?> Handle(
        GetBuildRequestByIdQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.MobileBuildRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        return entity is null ? null : MapToDto(entity);
    }

    public async Task<IReadOnlyCollection<MobileBuildRequestDto>> Handle(
        ListBuildRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MobileBuildRequests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Channel))
        {
            var channel = request.Channel.Trim().ToLowerInvariant();
            query = query.Where(r => r.Channel == channel);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(r => r.Status == status);
        }

        var list = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return list.Select(MapToDto).ToList();
    }

    private static MobileBuildRequestDto MapToDto(Domain.MobileBuildRequest r) =>
        new(
            r.Id,
            r.Channel,
            r.CommitSha,
            r.Version,
            r.VersionCode,
            r.PackageName,
            r.TargetApiUrl,
            r.IdempotencyKey,
            r.Status,
            r.WorkflowRunId,
            r.WorkflowRunAttempt,
            r.ErrorMessage,
            r.ReleaseId,
            r.GroupRequestId,
            r.ReleaseTag,
            r.CreatedAt,
            r.UpdatedAt);
}
