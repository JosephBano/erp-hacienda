using FluentValidation;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hato.Modules.Livestock.Infrastructure;

/// <summary>Composition entry point of the Livestock module, called from Hato.Api.</summary>
public static class LivestockModule
{
    public static IServiceCollection AddLivestockModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HatoDb")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión 'HatoDb'. En desarrollo: " +
                "dotnet user-secrets set \"ConnectionStrings:HatoDb\" \"...\" --project src/Hato.Api");

        services.AddDbContext<LivestockDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", LivestockDbContext.Schema)));

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(Application.AssemblyMarker).Assembly));

        services.AddValidatorsFromAssembly(typeof(Application.AssemblyMarker).Assembly);

        return services;
    }
}
