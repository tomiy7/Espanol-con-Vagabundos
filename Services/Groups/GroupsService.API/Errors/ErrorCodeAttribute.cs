namespace GroupsService.API.Errors;

// Kod koji akcija vraca kad domen odbije operaciju (npr. CANNOT_CANCEL). Cita ga
// GroupsExceptionHandler, pa kontroleri nemaju try/catch u svakoj akciji.
[AttributeUsage(AttributeTargets.Method)]
public sealed class ErrorCodeAttribute(string code) : Attribute
{
    public string Code { get; } = code;
}
