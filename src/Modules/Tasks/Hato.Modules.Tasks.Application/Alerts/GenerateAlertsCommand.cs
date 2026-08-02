using Hato.Modules.Breeding.Contracts;
using Hato.Modules.Inventory.Contracts;
using Hato.Modules.Livestock.Contracts;
using Hato.Modules.Tasks.Application.Abstractions;
using Hato.Modules.Tasks.Domain;
using Hato.Modules.Tasks.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Tasks.Application.Alerts;

public record GenerateAlertsCommand() : IRequest<int>;

/// <summary>
/// Runs every alert generator against the other modules' public read contracts
/// (Art. 6) — no cross-schema SQL lives here, so a column rename in Livestock or
/// Breeding fails this module's build instead of failing silently at runtime.
/// </summary>
public class GenerateAlertsCommandHandler(
    ITasksDbContext tasksDb,
    IActivePregnanciesReader activePregnancies,
    IPendingPregnancyChecksReader pendingChecks,
    IWithdrawalPeriodsReader withdrawals,
    IExpiringBatchesReader expiringBatches)
    : IRequestHandler<GenerateAlertsCommand, int>
{
    private const int UpcomingBirthWindowDays = 15;
    private const int PregnancyCheckDueAfterDays = 30;
    private const int InventoryExpiryWindowDays = 30;

    public async Task<int> Handle(GenerateAlertsCommand request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var generatedCount = 0;

        generatedCount += await GenerateUpcomingBirthAlertsAsync(today, cancellationToken);
        generatedCount += await GenerateWithdrawalAlertsAsync(today, cancellationToken);
        generatedCount += await GeneratePendingPregnancyCheckAlertsAsync(today, cancellationToken);
        generatedCount += await GenerateInventoryExpiryAlertsAsync(today, cancellationToken);

        if (generatedCount > 0)
            await tasksDb.SaveChangesAsync(cancellationToken);

        return generatedCount;
    }

    private async Task<int> GenerateUpcomingBirthAlertsAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var upcomingBirths = await activePregnancies.GetUpcomingBirthsAsync(today.AddDays(UpcomingBirthWindowDays), cancellationToken);
        var count = 0;

        foreach (var birth in upcomingBirths)
        {
            if (await AlertAlreadyActiveAsync("UPCOMING_BIRTH", birth.PregnancyId, cancellationToken))
                continue;

            tasksDb.Alerts.Add(Alert.Create(
                "UPCOMING_BIRTH",
                "Parto Próximo",
                $"La gestación {birth.PregnancyId} de la madre {birth.DamId} tiene fecha probable de parto el {birth.ExpectedBirthDate}.",
                AlertSeverity.Warning,
                birth.PregnancyId));
            count++;
        }

        return count;
    }

    private async Task<int> GenerateWithdrawalAlertsAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var activeWithdrawals = await withdrawals.GetActiveAsOfAsync(today, cancellationToken);
        var count = 0;

        foreach (var w in activeWithdrawals)
        {
            if (await AlertAlreadyActiveAsync("WITHDRAWAL_PERIOD_ACTIVE", w.Id, cancellationToken))
                continue;

            tasksDb.Alerts.Add(Alert.Create(
                "WITHDRAWAL_PERIOD_ACTIVE",
                "Período de Retiro Activo",
                $"El animal {w.AnimalId} tiene un período de retiro activo ({w.Target}) hasta {w.EndsAt:yyyy-MM-dd}.",
                AlertSeverity.Critical,
                w.Id));
            count++;
        }

        return count;
    }

    private async Task<int> GeneratePendingPregnancyCheckAlertsAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var pending = await pendingChecks.GetServicesPendingCheckAsync(today.AddDays(-PregnancyCheckDueAfterDays), cancellationToken);
        var count = 0;

        foreach (var service in pending)
        {
            if (await AlertAlreadyActiveAsync("PENDING_PREGNANCY_CHECK", service.ServiceId, cancellationToken))
                continue;

            tasksDb.Alerts.Add(Alert.Create(
                "PENDING_PREGNANCY_CHECK",
                "Palpación Pendiente",
                $"El servicio {service.ServiceId} de la madre {service.DamId} del {service.ServiceDate} aún no tiene diagnóstico de preñez.",
                AlertSeverity.Warning,
                service.ServiceId));
            count++;
        }

        return count;
    }

    private async Task<int> GenerateInventoryExpiryAlertsAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var expiring = await expiringBatches.GetExpiringWithinAsync(today.AddDays(InventoryExpiryWindowDays), cancellationToken);
        var count = 0;

        foreach (var batch in expiring)
        {
            if (await AlertAlreadyActiveAsync("INVENTORY_BATCH_EXPIRING", batch.BatchId, cancellationToken))
                continue;

            var severity = batch.ExpirationDate <= today ? AlertSeverity.Critical : AlertSeverity.Warning;
            tasksDb.Alerts.Add(Alert.Create(
                "INVENTORY_BATCH_EXPIRING",
                "Lote de Inventario por Vencer",
                $"El lote '{batch.BatchNumber}' de '{batch.ItemName}' ({batch.Quantity} unidades) vence el {batch.ExpirationDate:yyyy-MM-dd}.",
                severity,
                batch.BatchId));
            count++;
        }

        return count;
    }

    private Task<bool> AlertAlreadyActiveAsync(string code, Guid targetEntityId, CancellationToken cancellationToken) =>
        tasksDb.Alerts.AnyAsync(a => a.Code == code && a.TargetEntityId == targetEntityId && !a.IsDismissed, cancellationToken);
}
