using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Application.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Api;

/// <summary>
/// Crea el admin de staging si no existe, cada vez que la API arranca.
///
/// Por que existe: ADR-0031 declara que staging "se reconstruye desde cero por
/// definicion" y no tiene backups a proposito. Sin esto, cada vez que se pierde
/// el volumen de Postgres (disco, un `down -v`, lo que sea) hay que volver a
/// crear el primer usuario a mano contra `POST /api/v1/people/users` para poder
/// entrar. Con esto, el siguiente arranque de la API ya lo deja listo.
///
/// GUARDADO POR ENTORNO A PROPOSITO. Esta clase solo actua si
/// `ASPNETCORE_ENVIRONMENT=Staging` (compose.staging.yml lo fija de forma
/// explicita para el servicio `api`). NUNCA debe correr en produccion: crearia
/// una cuenta admin en cuanto lo hiciera (ADR-0030).
///
/// SIN CONTRASENA POR DEFECTO EN EL CODIGO. La regla de este proyecto es que
/// ningun secreto entra al repositorio en ninguna forma, ni de ejemplo — eso
/// incluye una contrasena "solo para staging" escrita como literal: es
/// exactamente el patron que un escaner de secretos existe para atrapar, y de
/// hecho lo atrapo (GitGuardian, incidente sobre un intento anterior de esta
/// misma clase). La contrasena viene de HATO_STAGING_SEED_ADMIN_PASSWORD,
/// cargada como secreto del entorno `staging` en GitHub —igual que
/// STAGING_POSTGRES_PASSWORD— y si no esta presente, el seed no corre: se
/// prefiere no sembrar nada a sembrar con una contrasena que cualquiera puede
/// leer en el historial de git.
/// </summary>
public static class StagingAdminSeeder
{
    public const string DefaultEmail = "admin@hato-staging.local";

    public static async Task SeedIfStagingAsync(WebApplication app)
    {
        if (!app.Environment.IsStaging())
        {
            return;
        }

        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("StagingAdminSeeder");

        var password = Environment.GetEnvironmentVariable("HATO_STAGING_SEED_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "HATO_STAGING_SEED_ADMIN_PASSWORD no esta definida: no se sembro ningun " +
                "admin de staging. Cargala como secreto del entorno 'staging' en GitHub " +
                "para que el proximo despliegue si lo haga.");
            return;
        }

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IPeopleDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var alreadyExists = await db.Users.AnyAsync(u => u.Email == DefaultEmail);
        if (alreadyExists)
        {
            return;
        }

        await sender.Send(new RegisterUserCommand("Administrador (staging)", DefaultEmail, password, Role: "admin"));

        logger.LogWarning(
            "Sembrado el admin de staging {Email} porque la tabla de usuarios no lo tenia. " +
            "Esto solo corre con ASPNETCORE_ENVIRONMENT=Staging.",
            DefaultEmail);
    }
}
