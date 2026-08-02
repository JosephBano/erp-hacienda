using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Hato.Modules.People.Infrastructure;

public static class PeopleModule
{
    public static IServiceCollection AddPeopleModule(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
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

        // Resolved once, here, so the same key signs (JwtTokenGenerator, via
        // IOptions<JwtOptions>) and validates (AddJwtBearer below) tokens.
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) && !environment.IsProduction())
        {
            // Dev/test convenience only — nothing is committed to the repo (Art. 2:
            // GitGuardian already caught one hardcoded dev secret in this project's
            // history). A fresh key per process start just invalidates old tokens on
            // restart. Production must set Jwt:SigningKey via user-secrets or an
            // environment variable, enforced by ValidateOnStart below.
            jwtOptions.SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }

        services.AddOptions<JwtOptions>()
            .Configure(o =>
            {
                o.Issuer = jwtOptions.Issuer;
                o.Audience = jwtOptions.Audience;
                o.SigningKey = jwtOptions.SigningKey;
                o.ExpiryMinutes = jwtOptions.ExpiryMinutes;
            })
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && o.SigningKey.Length >= 32,
                "Falta 'Jwt:SigningKey' (mínimo 32 caracteres). En producción: " +
                "dotnet user-secrets set \"Jwt:SigningKey\" \"...\" --project src/Hato.Api, " +
                "o la variable de entorno Jwt__SigningKey.")
            .ValidateOnStart();

        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey ?? string.Empty)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });

        services.AddAuthorization();

        return services;
    }
}
