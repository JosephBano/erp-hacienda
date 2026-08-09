using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.TreatmentCourses;

public record GetTreatmentCourseByIdQuery(Guid Id) : IRequest<TreatmentCourseDto>;

public record GetTreatmentCoursesForAnimalQuery(Guid AnimalId) : IRequest<List<TreatmentCourseDto>>;

public record GetTreatmentCoursesForGroupQuery(Guid GroupId) : IRequest<List<TreatmentCourseDto>>;

public class GetTreatmentCourseByIdHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetTreatmentCourseByIdQuery, TreatmentCourseDto>
{
    public async Task<TreatmentCourseDto> Handle(GetTreatmentCourseByIdQuery request, CancellationToken cancellationToken)
    {
        var course = await dbContext.TreatmentCourses
            .AsNoTracking()
            .Include(c => c.Applications)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (course is null)
            throw new DomainException($"La serie de tratamiento con ID '{request.Id}' no existe.");

        return ToDto(course);
    }

    internal static TreatmentCourseDto ToDto(TreatmentCourse course) => new(
        course.Id,
        course.AnimalId,
        course.GroupId,
        course.StartsAt,
        course.EndsAt,
        course.RouteId,
        course.Reason,
        course.ProductId,
        course.DoseKindId,
        course.DoseFactorAmount,
        course.DoseFactorUnit,
        course.Notes,
        course.IsSynthetic,
        course.Applications
            .OrderBy(a => a.ApplicationNo)
            .Select(a => new TreatmentCourseApplicationDto(
                a.Id,
                a.ApplicationNo,
                a.AppliedAt,
                a.CalculatedDoseAmount,
                a.CalculatedDoseUnit,
                a.IsEstimated,
                a.AdministeredDoseAmount,
                a.AdministeredDoseUnit,
                a.Notes))
            .ToList());
}

public class GetTreatmentCoursesForAnimalHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetTreatmentCoursesForAnimalQuery, List<TreatmentCourseDto>>
{
    public async Task<List<TreatmentCourseDto>> Handle(
        GetTreatmentCoursesForAnimalQuery request, CancellationToken cancellationToken)
    {
        var courses = await dbContext.TreatmentCourses
            .AsNoTracking()
            .Include(c => c.Applications)
            .Where(c => c.AnimalId == request.AnimalId)
            .OrderByDescending(c => c.StartsAt)
            .ToListAsync(cancellationToken);

        return courses.Select(GetTreatmentCourseByIdHandler.ToDto).ToList();
    }
}

public class GetTreatmentCoursesForGroupHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetTreatmentCoursesForGroupQuery, List<TreatmentCourseDto>>
{
    public async Task<List<TreatmentCourseDto>> Handle(
        GetTreatmentCoursesForGroupQuery request, CancellationToken cancellationToken)
    {
        var courses = await dbContext.TreatmentCourses
            .AsNoTracking()
            .Include(c => c.Applications)
            .Where(c => c.GroupId == request.GroupId)
            .OrderByDescending(c => c.StartsAt)
            .ToListAsync(cancellationToken);

        return courses.Select(GetTreatmentCourseByIdHandler.ToDto).ToList();
    }
}

public record DoseKindDto(Guid Id, string Key, string LabelEs, bool IsActive);

public record GetDoseKindsQuery(bool IncludeInactive = false) : IRequest<List<DoseKindDto>>;

public class GetDoseKindsHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetDoseKindsQuery, List<DoseKindDto>>
{
    public async Task<List<DoseKindDto>> Handle(GetDoseKindsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.DoseKinds.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(k => k.IsActive);

        return await query
            .OrderBy(k => k.LabelEs)
            .Select(k => new DoseKindDto(k.Id, k.Key, k.LabelEs, k.IsActive))
            .ToListAsync(cancellationToken);
    }
}
