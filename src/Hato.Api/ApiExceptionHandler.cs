using FluentValidation;
using Hato.Modules.Livestock.Application.HealthPlans;
using Hato.Modules.Livestock.Application.PlausibilityRanges;
using Hato.Modules.Livestock.Application.TreatmentCourses;
using Hato.Modules.Livestock.Domain.Exceptions;
using Hato.SharedKernel;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hato.Api;

/// <summary>Maps domain/application exceptions to RFC 7807 Problem Details responses.</summary>
public class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
            DomainException => (StatusCodes.Status400BadRequest, "Regla de negocio violada"),
            DuplicateHealthPlanException => (StatusCodes.Status409Conflict, "Plan duplicado"),
            DuplicatePlausibilityRangeException => (StatusCodes.Status409Conflict, "Combinación duplicada"),
            MissingWeighingForDoseException => (StatusCodes.Status400BadRequest, "Falta un pesaje para calcular la dosis"),
            AnimalGroupStateException => (StatusCodes.Status409Conflict, "Estado del grupo no permite la operación"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "No autorizado"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "No encontrado"),
            _ => (0, string.Empty),
        };

        if (statusCode == 0)
        {
            statusCode = StatusCodes.Status500InternalServerError;
            title = "Error interno";
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
        };

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        if (exception is MissingWeighingForDoseException)
        {
            // Typed error code (task 6/test 4): the field-app branches on this
            // instead of parsing the Spanish sentence to offer "switch to Absolute,
            // or weigh first" (sub-plan risk table).
            problemDetails.Extensions["errorCode"] = MissingWeighingForDoseException.ErrorCode;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
