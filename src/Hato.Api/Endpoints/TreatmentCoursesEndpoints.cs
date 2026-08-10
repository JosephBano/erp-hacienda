using Hato.Modules.Livestock.Application.TreatmentCourses;
using MediatR;

namespace Hato.Api.Endpoints;

public static class TreatmentCoursesEndpoints
{
    public static void MapTreatmentCoursesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/treatment-courses").WithTags("TreatmentCourses").RequireAuthorization();

        group.MapPost("/", async (CreateTreatmentCourseRequest request, ISender sender) =>
        {
            var command = new CreateTreatmentCourseCommand(
                request.AnimalId,
                request.GroupId,
                request.StartsAt,
                request.RouteId,
                request.Reason,
                request.ProductId,
                request.DoseKindId,
                request.DoseFactorAmount,
                request.DoseFactorUnit,
                request.Notes,
                request.AdministeredDoseAmount,
                request.AdministeredDoseUnit,
                request.ApplicationNotes,
                request.MilkWithdrawalDays,
                request.MeatWithdrawalDays,
                request.IsPlausibilityConfirmed);

            var id = await sender.Send(command);
            return Results.Created($"/api/v1/treatment-courses/{id}", new { id });
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var course = await sender.Send(new GetTreatmentCourseByIdQuery(id));
            return Results.Ok(course);
        });

        group.MapPost("/{id:guid}/applications", async (Guid id, AddTreatmentCourseApplicationRequest request, ISender sender) =>
        {
            var command = new AddTreatmentCourseApplicationCommand(
                id,
                request.AppliedAt,
                request.AdministeredDoseAmount,
                request.AdministeredDoseUnit,
                request.Notes,
                request.MilkWithdrawalDays,
                request.MeatWithdrawalDays);

            var applicationId = await sender.Send(command);
            return Results.Created($"/api/v1/treatment-courses/{id}/applications/{applicationId}", new { id = applicationId });
        });

        app.MapGet("/api/v1/animals/{animalId:guid}/treatment-courses", async (Guid animalId, ISender sender) =>
        {
            var courses = await sender.Send(new GetTreatmentCoursesForAnimalQuery(animalId));
            return Results.Ok(courses);
        }).WithTags("TreatmentCourses").RequireAuthorization();

        app.MapGet("/api/v1/animal-groups/{groupId:guid}/treatment-courses", async (Guid groupId, ISender sender) =>
        {
            var courses = await sender.Send(new GetTreatmentCoursesForGroupQuery(groupId));
            return Results.Ok(courses);
        }).WithTags("TreatmentCourses").RequireAuthorization();

        app.MapGet("/api/v1/dose-kinds", async (bool? includeInactive, ISender sender) =>
        {
            var kinds = await sender.Send(new GetDoseKindsQuery(includeInactive ?? false));
            return Results.Ok(kinds);
        }).WithTags("TreatmentCourses").RequireAuthorization();
    }
}

public record CreateTreatmentCourseRequest(
    Guid? AnimalId,
    Guid? GroupId,
    DateTimeOffset StartsAt,
    Guid RouteId,
    string? Reason,
    Guid? ProductId,
    Guid DoseKindId,
    decimal DoseFactorAmount,
    string DoseFactorUnit,
    string? Notes,
    decimal? AdministeredDoseAmount = null,
    string? AdministeredDoseUnit = null,
    string? ApplicationNotes = null,
    int? MilkWithdrawalDays = null,
    int? MeatWithdrawalDays = null,
    bool IsPlausibilityConfirmed = false);

public record AddTreatmentCourseApplicationRequest(
    DateTimeOffset AppliedAt,
    decimal? AdministeredDoseAmount = null,
    string? AdministeredDoseUnit = null,
    string? Notes = null,
    int? MilkWithdrawalDays = null,
    int? MeatWithdrawalDays = null);
