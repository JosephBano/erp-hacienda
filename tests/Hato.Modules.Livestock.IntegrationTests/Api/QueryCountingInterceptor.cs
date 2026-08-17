using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// docs/spec/feature-0001-admin-web-animal-groups/spec.md sec.8: mitigation for the risk
/// "La agregación de GetAnimalGroupsHandler introduce un N+1 si se rompe el batching. No
/// falla ninguna prueba funcional: sólo se pone lento." This EF Core
/// <see cref="DbCommandInterceptor"/> counts every SQL command that returns a reader
/// (i.e. every SELECT a DbContext issues), which is exactly what
/// ToListAsync/ToDictionaryAsync/SumAsync each emit as one round-trip apiece.
///
/// <see cref="Reset"/> lets a single interceptor instance be shared across a test's
/// arrange/act phases (e.g. when it is registered once on a long-lived factory) so only
/// the queries issued by the code under test are counted.
/// </summary>
public sealed class QueryCountingInterceptor : DbCommandInterceptor
{
    private int _count;

    public int Count => _count;

    public void Reset() => Interlocked.Exchange(ref _count, 0);

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
