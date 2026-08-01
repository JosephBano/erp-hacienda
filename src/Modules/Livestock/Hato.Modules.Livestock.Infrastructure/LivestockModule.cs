using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
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
        // Resolved lazily (at DbContext construction, not service registration) so that
        // test hosts like WebApplicationFactory — which inject their own connection
        // string after this method runs — still see it. Also means the app doesn't fail
        // at startup on endpoints that never touch the database.
        services.AddDbContext<LivestockDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("HatoDb")
                ?? throw new InvalidOperationException(
                    "Falta la cadena de conexión 'HatoDb'. En desarrollo: " +
                    "dotnet user-secrets set \"ConnectionStrings:HatoDb\" \"...\" --project src/Hato.Api");

            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", LivestockDbContext.Schema));
        });

        services.AddScoped<ILivestockDbContext>(sp => sp.GetRequiredService<LivestockDbContext>());

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Application.AssemblyMarker).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(Application.AssemblyMarker).Assembly);

        return services;
    }
}
