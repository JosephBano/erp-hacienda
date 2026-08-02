using FluentValidation;
using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Application.CrossModule;
using Hato.Modules.Breeding.Contracts;
using Hato.Modules.Breeding.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hato.Modules.Breeding.Infrastructure;

public static class BreedingModule
{
    public static IServiceCollection AddBreedingModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BreedingDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("HatoDb")
                ?? throw new InvalidOperationException(
                    "Falta la cadena de conexión 'HatoDb'.");

            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", BreedingDbContext.Schema));
        });

        services.AddScoped<IBreedingDbContext>(sp => sp.GetRequiredService<BreedingDbContext>());

        services.AddScoped<IActivePregnanciesReader, ActivePregnanciesReader>();
        services.AddScoped<IPendingPregnancyChecksReader, PendingPregnancyChecksReader>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Application.AssemblyMarker).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(Application.AssemblyMarker).Assembly);

        return services;
    }
}
