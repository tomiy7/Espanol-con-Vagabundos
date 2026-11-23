using GroupsService.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace GroupsService.API.Errors;

// Jedno mesto koje izuzetke pretvara u HTTP odgovor (ProblemDetails + "code").
// Domen ne zna za HTTP; ovde se odlucuje o statusu:
//   EntityNotFoundException      -> 404 (kod iz izuzetka, npr. GROUP_NOT_FOUND)
//   ConcurrencyConflictException -> 409 (neko je u medjuvremenu promenio podatke)
//   GroupsDomainException        -> 400 (poslovno pravilo; kod iz [ErrorCode] akcije)
//   UnauthorizedAccessException  -> 401 (token bez ispravnog korisnika)
//   sve ostalo                   -> 500 bez detalja (detalji samo u logu)
public sealed class GroupsExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<GroupsExceptionHandler> _logger;

    public GroupsExceptionHandler(IProblemDetailsService problemDetails, ILogger<GroupsExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var actionCode = httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint?
            .Metadata.GetMetadata<ErrorCodeAttribute>()?.Code;

        var (status, code, detail) = exception switch
        {
            EntityNotFoundException e => (StatusCodes.Status404NotFound, e.Code, e.Message),
            ConcurrencyConflictException e => (StatusCodes.Status409Conflict, actionCode ?? ErrorCodes.Conflict, e.Message),
            GroupsDomainException e => (StatusCodes.Status400BadRequest, actionCode ?? ErrorCodes.BusinessRule, e.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized, "Authentication is required."),
            _ => (StatusCodes.Status500InternalServerError, ErrorCodes.Internal, "An unexpected error occurred.")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        else
            _logger.LogWarning("Request {Method} {Path} rejected with {Status} {Code}: {Message}",
                httpContext.Request.Method, httpContext.Request.Path, status, code, exception.Message);

        httpContext.Response.StatusCode = status;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = ReasonPhrases.GetReasonPhrase(status),
                Detail = detail,
                Extensions = { ["code"] = code }
            }
        });
    }
}
