using FluentValidation;
using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Infrastructure.Persistence;
using Hato.Modules.Delivery.Infrastructure.Persistence.Services;
using Hato.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hato.Modules.Delivery.Infrastructure;

public static class DeliveryModule
{
    public static IServiceCollection AddDeliveryModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddScoped<AuditTimestampInterceptor>();

        services.AddDbContext<DeliveryDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("HatoDb")
                ?? throw new InvalidOperationException("Falta la cadena de conexión 'HatoDb'.");

            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", DeliveryDbContext.Schema));

            options.AddInterceptors(sp.GetRequiredService<AuditTimestampInterceptor>());
        });

        services.AddScoped<IDeliveryDbContext>(sp => sp.GetRequiredService<DeliveryDbContext>());
        services.AddScoped<IVersionCodeReservator, VersionCodeReservator>();
        services.AddSingleton<IArtifactStorage, LocalArtifactStorage>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Application.AssemblyMarker).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(Application.AssemblyMarker).Assembly);

        return services;
    }
}
