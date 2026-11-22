namespace GroupsService.Domain.Exceptions;

// Trazeni agregat ne postoji. Nosi stabilan kod (npr. GROUP_NOT_FOUND) koji API vraca
// klijentu; sam domen ne zna nista o HTTP-u (API ovo mapira u 404).
public class EntityNotFoundException : GroupsDomainException
{
    public string Code { get; }

    public EntityNotFoundException(string entityName, object id)
        : this(entityName, $"{entityName} with id {id} does not exist.") { }

    private EntityNotFoundException(string entityName, string message) : base(message)
    {
        Code = $"{entityName.ToUpperInvariant()}_NOT_FOUND";
    }

    public static EntityNotFoundException Group(Guid id) => new("Group", id);
    public static EntityNotFoundException Enrollment(Guid id) => new("Enrollment", id);
    // Pozivnica se trazi po tokenu, koji ne sme da se vraca u odgovoru.
    public static EntityNotFoundException Invitation() => new("Invitation", "Invitation does not exist.");
}
