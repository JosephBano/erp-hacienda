using FluentValidation;
using Hato.Modules.Tasks.Application.Abstractions;
using Hato.Modules.Tasks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Hato.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hato.Modules.Tasks.Infrastructure;

public static class TasksModule
{
    public static IServiceCollection AddTasksModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Shared audit stamping: without it, rows carry no created_at/updated_at
        // and the offline pull cursor (ADR-0008) cannot position them.
        services.TryAddScoped<AuditTimestampInterceptor>();

        services.AddDbContext<TasksDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("HatoDb")
                ?? throw new InvalidOperationException(
                    "Falta la cadena de conexión 'HatoDb'.");

            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", TasksDbContext.Schema));

            options.AddInterceptors(sp.GetRequiredService<AuditTimestampInterceptor>());
        });

        services.AddScoped<ITasksDbContext>(sp => sp.GetRequiredService<TasksDbContext>());

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Application.AssemblyMarker).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(Application.AssemblyMarker).Assembly);

        return services;
    }
}
