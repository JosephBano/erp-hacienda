using Hato.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hato.SharedKernel.Persistence;

/// <summary>
/// Stamps <see cref="AuditableEntity"/> timestamps and authorship on every module's
/// DbContext.
///
/// This is not cosmetic bookkeeping: the offline pull protocol (ADR-0008) positions every
/// row against a cursor using <c>updated_at ?? created_at</c>. A row saved without those
/// stamps sits at <c>0001-01-01</c> forever, which means a mobile client either receives
/// it on every single pull or — once its cursor has advanced past that date — never
/// receives it again. Timestamping therefore belongs to the shared kernel, not to any one
/// module.
/// </summary>
public class AuditTimestampInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Applies the stamps. Exposed so a module that already owns a richer interceptor can
    /// reuse the exact same rules instead of drifting from them.
    /// </summary>
    public static void Stamp(DbContext? context, ICurrentUser? currentUser = null)
    {
        if (context is null) return;

        var now = DateTimeOffset.UtcNow;
        var userId = currentUser?.UserId;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CreatedAt == default)
                    {
                        entry.Entity.CreatedAt = now;
                    }

                    entry.Entity.CreatedBy ??= userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    if (userId is not null)
                    {
                        entry.Entity.UpdatedBy = userId;
                    }

                    break;
            }
        }
    }

    private void Stamp(DbContext? context) => Stamp(context, currentUser);
}
