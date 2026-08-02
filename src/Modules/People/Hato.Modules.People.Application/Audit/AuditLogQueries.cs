using Hato.Modules.People.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.People.Application.Audit;

public record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string? UserEmail,
    string? UserFullName,
    string Action,
    string Module,
    string EntityName,
    string EntityId,
    string? DetailsJson,
    DateTimeOffset Timestamp);

public record PagedAuditLogsDto(
    List<AuditLogDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record GetAuditLogsQuery(
    Guid? UserId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 50) : IRequest<PagedAuditLogsDto>;

public class GetAuditLogsHandler(IPeopleDbContext dbContext)
    : IRequestHandler<GetAuditLogsQuery, PagedAuditLogsDto>
{
    public async Task<PagedAuditLogsDto> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 50 : request.PageSize;

        var query = dbContext.AuditLogs.AsNoTracking();

        if (request.UserId.HasValue)
        {
            query = query.Where(a => a.UserId == request.UserId.Value);
        }

        if (request.From.HasValue)
        {
            query = query.Where(a => a.Timestamp >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(a => a.Timestamp <= request.To.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(
                a.Id,
                a.UserId,
                a.UserEmail,
                a.UserFullName,
                a.Action,
                a.Module,
                a.EntityName,
                a.EntityId,
                a.DetailsJson,
                a.Timestamp))
            .ToListAsync(cancellationToken);

        return new PagedAuditLogsDto(items, totalCount, page, pageSize);
    }
}
