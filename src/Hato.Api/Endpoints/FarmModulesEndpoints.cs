using Hato.Modules.People.Application.FarmModules;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hato.Api.Endpoints;

public static class FarmModulesEndpoints
{
    public static void MapFarmModulesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/farm-modules").WithTags("FarmModules").RequireAuthorization();

        group.MapGet("/", async (ISender sender) =>
        {
            var modules = await sender.Send(new GetFarmModulesQuery());
            return Results.Ok(modules);
        })
        .RequireAuthorization("SettingsFarmModulesRead");

        group.MapPatch("/{key}", async (string key, [FromBody] SetFarmModuleEnabledRequest body, ISender sender) =>
        {
            var module = await sender.Send(new SetFarmModuleEnabledCommand(key, body.Enabled, body.DisabledReason));
            return Results.Ok(module);
        })
        .RequireAuthorization("SettingsFarmModulesManage");
    }
}

public record SetFarmModuleEnabledRequest(bool Enabled, string? DisabledReason);
