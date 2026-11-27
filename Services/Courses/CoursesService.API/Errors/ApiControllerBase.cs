using Microsoft.AspNetCore.Mvc;

namespace CoursesService.API.Errors;

// Zajednicko za kontrolere: ProblemDetails sa kodom i podaci iz tokena.
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected bool IsStaff => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Professor);

    protected ObjectResult ProblemWithCode(int status, string code, string detail)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, detail: detail);
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}

// Imena rola moraju da se poklapaju sa onima koje Auth upisuje u JWT (ClaimTypes.Role).
public static class Roles
{
    public const string Student = "student";
    public const string Professor = "professor";
    public const string Admin = "admin";
}
