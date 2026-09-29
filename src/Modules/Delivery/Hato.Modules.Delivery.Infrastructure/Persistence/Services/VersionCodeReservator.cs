using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Delivery.Infrastructure.Persistence.Services;

public class VersionCodeReservator(DeliveryDbContext dbContext) : IVersionCodeReservator
{
    public async Task<int> ReserveNextVersionCodeAsync(string packageName, CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            if (dbContext.Database.IsNpgsql())
            {
                var result = await dbContext.Database
                    .SqlQueryRaw<int>(@"
                        INSERT INTO delivery.mobile_version_code_sequences (package_name, last_version_code, updated_at)
                        VALUES ({0}, 1, NOW())
                        ON CONFLICT (package_name)
                        DO UPDATE SET last_version_code = delivery.mobile_version_code_sequences.last_version_code + 1, updated_at = NOW()
                        RETURNING last_version_code", packageName)
                    .ToListAsync(cancellationToken);

                return result.First();
            }

            var seq = await dbContext.MobileVersionCodeSequences
                .FirstOrDefaultAsync(s => s.PackageName == packageName, cancellationToken);

            int nextCode;
            if (seq is null)
            {
                seq = MobileVersionCodeSequence.Create(packageName, 1);
                dbContext.MobileVersionCodeSequences.Add(seq);
                nextCode = 1;
            }
            else
            {
                nextCode = seq.Next();
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return nextCode;
        });
    }
}
