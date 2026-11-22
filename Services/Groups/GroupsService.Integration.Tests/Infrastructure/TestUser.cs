namespace GroupsService.Integration.Tests.Infrastructure;

// Korisnik u testu: Id i rola idu u JWT (isti claim-ovi koje izdaje Auth).
public record TestUser(Guid Id, string Role)
{
    public static TestUser Student() => new(Guid.NewGuid(), "student");
    public static TestUser Professor() => new(Guid.NewGuid(), "professor");
    public static TestUser Admin() => new(Guid.NewGuid(), "admin");
}
