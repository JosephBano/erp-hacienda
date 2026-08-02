using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Domain;
using Hato.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hato.Modules.People.Infrastructure.Persistence;

public class AuditSaveChangesInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateAuditPropertiesAndLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditPropertiesAndLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditPropertiesAndLogs(DbContext? context)
    {
        if (context is null) return;

        var now = DateTimeOffset.UtcNow;
        var userId = currentUser.UserId;
        var userEmail = currentUser.UserEmail;
        var userFullName = currentUser.FullName;

        var entries = context.ChangeTracker.Entries<AuditableEntity>().ToList();
        var auditLogs = new List<AuditLog>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default)
                {
                    entry.Entity.CreatedAt = now;
                }
                entry.Entity.CreatedBy ??= userId;

                auditLogs.Add(AuditLog.Create(
                    userId,
                    userEmail,
                    userFullName,
                    "Create",
                    context.GetType().Name.Replace("DbContext", ""),
                    entry.Entity.GetType().Name,
                    entry.Entity.Id.ToString(),
                    timestamp: now));
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = userId;

                var action = entry.Entity.IsDeleted ? "Delete" : "Update";

                auditLogs.Add(AuditLog.Create(
                    userId,
                    userEmail,
                    userFullName,
                    action,
                    context.GetType().Name.Replace("DbContext", ""),
                    entry.Entity.GetType().Name,
                    entry.Entity.Id.ToString(),
                    timestamp: now));
            }
        }

        if (auditLogs.Count > 0 && context is PeopleDbContext peopleDb)
        {
            peopleDb.AuditLogs.AddRange(auditLogs);
        }
    }
}
