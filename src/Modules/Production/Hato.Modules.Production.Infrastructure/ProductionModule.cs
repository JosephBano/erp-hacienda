using FluentValidation;
using Hato.Modules.Production.Application.Abstractions;
using Hato.Modules.Production.Application.CrossModule;
using Hato.Modules.Production.Contracts;
using Hato.Modules.Production.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Hato.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hato.Modules.Production.Infrastructure;

public static class ProductionModule
{
    public static IServiceCollection AddProductionModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Shared audit stamping: without it, rows carry no created_at/updated_at
        // and the offline pull cursor (ADR-0008) cannot position them.
        services.TryAddScoped<AuditTimestampInterceptor>();

        services.AddDbContext<ProductionDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("HatoDb")
                ?? throw new InvalidOperationException("Falta la cadena de conexión 'HatoDb'.");

            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", ProductionDbContext.Schema));

            options.AddInterceptors(sp.GetRequiredService<AuditTimestampInterceptor>());
        });

        services.AddScoped<IProductionDbContext>(sp => sp.GetRequiredService<ProductionDbContext>());

        services.AddScoped<IMilkYieldsReader, MilkYieldsReader>();

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(Application.Abstractions.IProductionDbContext).Assembly));

        services.AddValidatorsFromAssembly(typeof(Application.Abstractions.IProductionDbContext).Assembly);

        return services;
    }
}
