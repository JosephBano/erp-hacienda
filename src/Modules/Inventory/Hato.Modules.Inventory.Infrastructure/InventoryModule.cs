using FluentValidation;
using Hato.Modules.Inventory.Application.Abstractions;
using Hato.Modules.Inventory.Application.CrossModule;
using Hato.Modules.Inventory.Contracts;
using Hato.Modules.Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Hato.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hato.Modules.Inventory.Infrastructure;

public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Shared audit stamping: without it, rows carry no created_at/updated_at
        // and the offline pull cursor (ADR-0008) cannot position them.
        services.TryAddScoped<AuditTimestampInterceptor>();

        services.AddDbContext<InventoryDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("HatoDb")
                ?? throw new InvalidOperationException("Falta la cadena de conexión 'HatoDb'.");

            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", InventoryDbContext.Schema));

            options.AddInterceptors(sp.GetRequiredService<AuditTimestampInterceptor>());
        });

        services.AddScoped<IInventoryDbContext>(sp => sp.GetRequiredService<InventoryDbContext>());

        services.AddScoped<IExpiringBatchesReader, ExpiringBatchesReader>();

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(Application.Abstractions.IInventoryDbContext).Assembly));

        services.AddValidatorsFromAssembly(typeof(Application.Abstractions.IInventoryDbContext).Assembly);

        return services;
    }
}
