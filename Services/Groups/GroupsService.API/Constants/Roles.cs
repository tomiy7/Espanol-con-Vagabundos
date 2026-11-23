namespace GroupsService.API.Constants;

// Imena rola moraju da se poklapaju sa onima koje Auth upisuje u JWT (ClaimTypes.Role).
public static class Roles
{
    public const string Student = "student";
    public const string Professor = "professor";
    public const string Admin = "admin";

    // Osoblje = profesor ili admin (za [Authorize(Roles = Roles.Staff)]).
    public const string Staff = Admin + "," + Professor;
}
