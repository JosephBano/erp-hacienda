using Hato.Modules.Breeding.Application.Birthings;
using Hato.Modules.Breeding.Application.Cohorts;
using Hato.Modules.Breeding.Application.Pregnancies;
using Hato.Modules.Breeding.Application.PregnancyChecks;
using Hato.Modules.Breeding.Application.Services;
using Hato.Modules.Breeding.Application.SemenStraws;
using Hato.Modules.Breeding.Application.Weanings;
using MediatR;

namespace Hato.Api.Endpoints;

public static class BreedingEndpoints
{
    public static void MapBreedingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/breeding").WithTags("Breeding").RequireAuthorization();

        group.MapPost("/semen-straws", async (CreateSemenStrawCommand command, ISender sender) =>
        {
            var straw = await sender.Send(command);
            return Results.Created($"/api/v1/breeding/semen-straws/{straw.Id}", straw);
        });

        group.MapGet("/semen-straws", async (ISender sender) =>
        {
            var straws = await sender.Send(new GetSemenStrawsQuery());
            return Results.Ok(straws);
        });

        group.MapPost("/services", async (RegisterBreedingServiceCommand command, ISender sender) =>
        {
            var service = await sender.Send(command);
            return Results.Created($"/api/v1/breeding/services/{service.Id}", service);
        });

        group.MapPost("/pregnancy-checks", async (RecordPregnancyCheckCommand command, ISender sender) =>
        {
            var check = await sender.Send(command);
            return Results.Created($"/api/v1/breeding/pregnancy-checks/{check.Id}", check);
        });

        group.MapGet("/pregnancies/active", async (ISender sender) =>
        {
            var pregnancies = await sender.Send(new GetActivePregnanciesQuery());
            return Results.Ok(pregnancies);
        });

        group.MapPost("/birthings", async (RecordBirthingCommand command, ISender sender) =>
        {
            var birthing = await sender.Send(command);
            return Results.Created($"/api/v1/breeding/birthings/{birthing.Id}", birthing);
        });

        group.MapPost("/weanings", async (RecordWeaningCommand command, ISender sender) =>
        {
            var birthing = await sender.Send(command);
            return Results.Ok(birthing);
        });

        // ----- Nursing cohorts (PLAN-FASE-3-5-PORCINO.md sec.3.5a.4) -----
        // Weaning is recorded at the cohort level rather than litter by litter. The
        // command computes the date from the species' DaysOfLactation setting and
        // walks every birthings row to record the per-birthing weaning event
        // (Art. 1: those are the immutable history; the cohort is the calendar view
        // that binds them together).
        group.MapPost("/cohorts/{cohortId:guid}/wean", async (
            Guid cohortId,
            RecordCohortWeaningCommand command,
            ISender sender) =>
        {
            var commandWithCohort = command with { NursingCohortId = cohortId };
            var cohort = await sender.Send(commandWithCohort);
            return Results.Ok(cohort);
        });

        group.MapGet("/pedigree/{animalId:guid}", async (Guid animalId, ISender sender) =>
        {
            var pedigree = await sender.Send(new Hato.Modules.Breeding.Application.Pedigree.GetPedigreeQuery(animalId));
            return pedigree is not null ? Results.Ok(pedigree) : Results.NotFound();
        });

        group.MapGet("/kpis/dams/{damId:guid}", async (Guid damId, ISender sender) =>
        {
            var kpis = await sender.Send(new Hato.Modules.Breeding.Application.Kpis.GetDamKpisQuery(damId));
            return Results.Ok(kpis);
        });
    }
}
