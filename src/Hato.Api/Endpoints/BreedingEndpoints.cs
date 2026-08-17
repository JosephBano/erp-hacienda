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

        // The read-side of the partos/camadas feature (docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.4):
        // the field-app and the panel have always been able to register a birthing, but
        // there was no way to list the ones already on file. This endpoint powers the
        // "Partos" tab in the breeding dashboard — most-recent-first, with the dam's
        // farm tag and the offspring rows so the panel can show the per-calf birth
        // weight the operator typed at registration time.
        group.MapGet("/birthings", async (ISender sender) =>
        {
            var items = await sender.Send(new GetBirthingsQuery());
            return Results.Ok(items);
        });

        group.MapPost("/weanings", async (RecordWeaningCommand command, ISender sender) =>
        {
            var birthing = await sender.Send(command);
            return Results.Ok(birthing);
        });

        // ----- Nursing cohorts (docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.4) -----
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

        // 3.5a.4 task 4: classifies a weaned nursing cohort by weight into N
        // headcount engorde lots (ADR-0023). The clinic document is the source
        // for the (animal → group, weight) assignment payload: the operator
        // weighs each animal on the day and the panel sends the list in one
        // request. The handler emits N individual WeightSorted events and M
        // GroupWeightSorting events in the same transaction.
        group.MapPost("/cohorts/{cohortId:guid}/classify-by-weight", async (
            Guid cohortId,
            ClassifyCohortByWeightRequest request,
            ISender sender) =>
        {
            var assignments = request.Assignments
                .Select(a => new WeightSortingAssignment(
                    a.AnimalId, a.TargetGroupId, a.WeightKg))
                .ToList();

            var command = new ClassifyCohortByWeightCommand(
                cohortId, request.SortingDate, assignments, request.Notes);

            var result = await sender.Send(command);
            return Results.Ok(result);
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

public record ClassifyCohortByWeightRequest(
    DateOnly SortingDate,
    List<ClassifyCohortByWeightAssignmentDto> Assignments,
    string? Notes = null);

public record ClassifyCohortByWeightAssignmentDto(
    Guid AnimalId,
    Guid TargetGroupId,
    decimal WeightKg);
