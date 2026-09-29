using Hato.Modules.Delivery.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Hato.Modules.Delivery.Application.Abstractions;

public interface IDeliveryDbContext
{
    DbSet<MobileBuildRequest> MobileBuildRequests { get; }
    DbSet<MobileRelease> MobileReleases { get; }
    DbSet<MobileReleaseTransitionAudit> MobileReleaseTransitionAudits { get; }
    DbSet<MobileVersionCodeSequence> MobileVersionCodeSequences { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
