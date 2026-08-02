using FluentValidation;
using Hato.Modules.Tasks.Application.Abstractions;
using Hato.Modules.Tasks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hato.Modules.Tasks.Infrastructure;

public static class TasksModule
{
    public static IServiceCollection AddTasksModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TasksDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("HatoDb")
                ?? throw new InvalidOperationException(
                    "Falta la cadena de conexión 'HatoDb'.");

            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", TasksDbContext.Schema));
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
