using FluentValidation;
using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hato.Modules.People.Infrastructure;

public static class PeopleModule
{
    public static IServiceCollection AddPeopleModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PeopleDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("HatoDb")
                ?? throw new InvalidOperationException("Falta la cadena de conexión 'HatoDb'.");

            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", PeopleDbContext.Schema));
        });

        services.AddScoped<IPeopleDbContext>(sp => sp.GetRequiredService<PeopleDbContext>());

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(Application.Abstractions.IPeopleDbContext).Assembly));

        services.AddValidatorsFromAssembly(typeof(Application.Abstractions.IPeopleDbContext).Assembly);

        return services;
    }
}
