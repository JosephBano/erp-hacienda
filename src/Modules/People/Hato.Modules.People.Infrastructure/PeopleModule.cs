using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Contracts;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Authorization;
using Hato.Modules.People.Infrastructure.CrossModule;
using Hato.Modules.People.Infrastructure.Persistence;
using Hato.SharedKernel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<IUserPermissionsReader, UserPermissionsReader>();

        services.AddDbContext<PeopleDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("HatoDb")
                ?? throw new InvalidOperationException("Falta la cadena de conexión 'HatoDb'.");

            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", PeopleDbContext.Schema));

            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddScoped<IPeopleDbContext>(sp => sp.GetRequiredService<PeopleDbContext>());

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(Application.Abstractions.IPeopleDbContext).Assembly));

        services.AddValidatorsFromAssembly(typeof(Application.Abstractions.IPeopleDbContext).Assembly);

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) && !environment.IsProduction())
        {
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
                options.MapInboundClaims = false;

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

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userIdClaim = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                        if (!Guid.TryParse(userIdClaim, out var userId))
                        {
                            context.Fail("Token inválido.");
                            return;
                        }

                        var dbContext = context.HttpContext.RequestServices.GetRequiredService<IPeopleDbContext>();
                        var isActive = await dbContext.Users
                            .AsNoTracking()
                            .Where(u => u.Id == userId)
                            .Select(u => (bool?)u.IsActive)
                            .FirstOrDefaultAsync();

                        if (isActive != true)
                            context.Fail("La cuenta fue desactivada.");
                    }
                };
            });

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy("PeopleUsersManage", p => p.RequirePermission(SystemPermissions.PeopleUsersManage));
            options.AddPolicy("PeopleRolesManage", p => p.RequirePermission(SystemPermissions.PeopleRolesManage));
            options.AddPolicy("LivestockCategoriesManage", p => p.RequirePermission(SystemPermissions.LivestockCategoriesManage));
            options.AddPolicy("LivestockBreedsManage", p => p.RequirePermission(SystemPermissions.LivestockBreedsManage));
            options.AddPolicy("LivestockSpeciesManage", p => p.RequirePermission(SystemPermissions.LivestockSpeciesManage));
            options.AddPolicy("LivestockTreatmentsConfigure", p => p.RequirePermission(SystemPermissions.LivestockTreatmentsConfigure));
            options.AddPolicy("SettingsFarmModulesRead", p => p.RequirePermission(SystemPermissions.SettingsFarmModulesRead));
            options.AddPolicy("SettingsFarmModulesManage", p => p.RequirePermission(SystemPermissions.SettingsFarmModulesManage));
            options.AddPolicy("InventoryFeedStagesManage", p => p.RequirePermission(SystemPermissions.InventoryFeedStagesManage));
            options.AddPolicy("InventoryReceptionsManage", p => p.RequirePermission(SystemPermissions.InventoryReceptionsManage));
        });

        return services;
    }
}
