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
/// una cuenta admin con una contrasena conocida y publica en el codigo fuente
/// de un repositorio publico (ADR-0030). Si esto se ejecutara en produccion
/// seria un backdoor, no una comodidad de pruebas.
/// </summary>
public static class StagingAdminSeeder
{
    public const string DefaultEmail = "admin@hato-staging.local";

    // Solo para staging, documentado a proposito: no protege nada real, ADR-0031
    // dice explicitamente que staging no tiene datos que duela perder. Si algun
    // dia hiciera falta rotarla sin tocar codigo, HATO_STAGING_SEED_ADMIN_PASSWORD
    // la sobreescribe.
    private const string DefaultPassword = "Admin123!";

    public static async Task SeedIfStagingAsync(WebApplication app)
    {
        if (!app.Environment.IsStaging())
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IPeopleDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("StagingAdminSeeder");

        var alreadyExists = await db.Users.AnyAsync(u => u.Email == DefaultEmail);
        if (alreadyExists)
        {
            return;
        }

        var password = Environment.GetEnvironmentVariable("HATO_STAGING_SEED_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            password = DefaultPassword;
        }

        await sender.Send(new RegisterUserCommand("Administrador (staging)", DefaultEmail, password, Role: "admin"));

        logger.LogWarning(
            "Sembrado el admin de staging {Email} porque la tabla de usuarios no lo tenia. " +
            "Esto solo corre con ASPNETCORE_ENVIRONMENT=Staging.",
            DefaultEmail);
    }
}
