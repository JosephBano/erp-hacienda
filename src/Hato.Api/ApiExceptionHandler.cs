using FluentValidation;
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

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
