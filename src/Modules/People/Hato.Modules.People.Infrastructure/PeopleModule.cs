using System.Text;
using FluentValidation;
using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

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

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && o.SigningKey.Length >= 32,
                "Falta 'Jwt:SigningKey' (mínimo 32 caracteres). En desarrollo: " +
                "dotnet user-secrets set \"Jwt:SigningKey\" \"...\" --project src/Hato.Api")
            .ValidateOnStart();

        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });

        services.AddAuthorization();

        return services;
    }
}
