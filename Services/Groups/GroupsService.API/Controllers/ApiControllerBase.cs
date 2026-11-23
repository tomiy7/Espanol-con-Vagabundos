using System.Security.Claims;
using GroupsService.API.Constants;
using GroupsService.API.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupsService.API.Controllers;

// Zajednicko za sve kontrolere: podrazumevano trazi JWT, cita korisnika iz tokena i
// pravi ProblemDetails sa kodom. Greske iz domena NE hvata - to radi GroupsExceptionHandler.
[ApiController]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public abstract class ApiControllerBase : ControllerBase
{
    // Id korisnika se uvek cita iz validiranog JWT-a, nikad iz tela zahteva.
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty
            ? id
            : throw new UnauthorizedAccessException("The token does not contain a valid user id.");

    protected bool IsAdmin => User.IsInRole(Roles.Admin);

    protected bool IsStaff => IsAdmin || User.IsInRole(Roles.Professor);

    // Greska koju kontroler sam proverava (npr. nedostaje parametar ili resurs ne postoji).
    protected ObjectResult ProblemWithCode(int status, string code, string detail)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, detail: detail);
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
