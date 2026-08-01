using Hato.Modules.Tasks.Application.Abstractions;
using Hato.Modules.Tasks.Domain;
using Hato.Modules.Tasks.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Tasks.Application.Alerts;

public record GenerateAlertsCommand() : IRequest<int>;

public class GenerateAlertsCommandHandler(ITasksDbContext tasksDb)
    : IRequestHandler<GenerateAlertsCommand, int>
{
    public async Task<int> Handle(GenerateAlertsCommand request, CancellationToken cancellationToken)
    {
        int generatedCount = 0;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dbContext = (DbContext)tasksDb;
        var connection = dbContext.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        // 1. Check upcoming birthings (expected birth date within 15 days)
        using var birthCommand = connection.CreateCommand();
        birthCommand.CommandText = """
            SELECT p.id, p.dam_id, p.expected_birth_date 
            FROM breeding.pregnancies p
            WHERE p.status = 'Active' 
              AND p.expected_birth_date <= @thresholdDate
              AND p.deleted_at IS NULL;
            """;
        var paramThreshold = birthCommand.CreateParameter();
        paramThreshold.ParameterName = "@thresholdDate";
        paramThreshold.Value = today.AddDays(15);
        birthCommand.Parameters.Add(paramThreshold);

        using var birthReader = await birthCommand.ExecuteReaderAsync(cancellationToken);
        var upcomingBirths = new List<(Guid PregnancyId, Guid DamId, DateOnly ExpectedDate)>();
        while (await birthReader.ReadAsync(cancellationToken))
        {
            upcomingBirths.Add((birthReader.GetGuid(0), birthReader.GetGuid(1), birthReader.GetFieldValue<DateOnly>(2)));
        }
        birthReader.Close();

        foreach (var birth in upcomingBirths)
        {
            bool exists = await tasksDb.Alerts.AnyAsync(
                a => a.Code == "UPCOMING_BIRTH" && a.TargetEntityId == birth.PregnancyId && !a.IsDismissed, cancellationToken);

            if (!exists)
            {
                var alert = Alert.Create(
                    "UPCOMING_BIRTH",
                    "Parto Próximo",
                    $"La gestación {birth.PregnancyId} de la madre {birth.DamId} tiene fecha probable de parto el {birth.ExpectedDate}.",
                    AlertSeverity.Warning,
                    birth.PregnancyId
                );
                tasksDb.Alerts.Add(alert);
                generatedCount++;
            }
        }

        // 2. Check active withdrawal periods
        using var withdrawalCommand = connection.CreateCommand();
        withdrawalCommand.CommandText = """
            SELECT w.id, w.animal_id, w.ends_at, w.type
            FROM livestock.withdrawal_periods w
            WHERE w.ends_at >= @today
              AND w.deleted_at IS NULL;
            """;
        var paramToday = withdrawalCommand.CreateParameter();
        paramToday.ParameterName = "@today";
        paramToday.Value = DateTimeOffset.UtcNow;
        withdrawalCommand.Parameters.Add(paramToday);

        using var withdrawalReader = await withdrawalCommand.ExecuteReaderAsync(cancellationToken);
        var activeWithdrawals = new List<(Guid Id, Guid AnimalId, DateTimeOffset EndsAt, string Type)>();
        while (await withdrawalReader.ReadAsync(cancellationToken))
        {
            activeWithdrawals.Add((withdrawalReader.GetGuid(0), withdrawalReader.GetGuid(1), withdrawalReader.GetFieldValue<DateTimeOffset>(2), withdrawalReader.GetString(3)));
        }
        withdrawalReader.Close();

        foreach (var w in activeWithdrawals)
        {
            bool exists = await tasksDb.Alerts.AnyAsync(
                a => a.Code == "WITHDRAWAL_PERIOD_ACTIVE" && a.TargetEntityId == w.Id && !a.IsDismissed, cancellationToken);

            if (!exists)
            {
                var alert = Alert.Create(
                    "WITHDRAWAL_PERIOD_ACTIVE",
                    "Período de Retiro Activo",
                    $"El animal {w.AnimalId} tiene un período de retiro activo ({w.Type}) hasta {w.EndsAt:yyyy-MM-dd HH:mm}.",
                    AlertSeverity.Critical,
                    w.Id
                );
                tasksDb.Alerts.Add(alert);
                generatedCount++;
            }
        }

        await tasksDb.SaveChangesAsync(cancellationToken);
        return generatedCount;
    }
}
