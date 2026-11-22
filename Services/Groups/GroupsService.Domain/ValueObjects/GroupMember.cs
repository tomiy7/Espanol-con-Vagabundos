using GroupsService.Domain.Exceptions;

namespace GroupsService.Domain.ValueObjects;

// Clanstvo u grupi/paru. Referise Enrollment.Id (ne UserId direktno) jer Enrollment
// je sad vlasnik korisnikovog upisa - Group je samo koordinacioni mehanizam koji
// Pair i Group tip Enrollment-a koriste da se organizuju (termini, clanovi).
// Zovemo ga Entity (ne cist ValueObject) zbog EF Core owned-collection mapiranja -
// ista odluka kao u prethodnoj verziji ovog koda.
public class GroupMember : Entity
{
    public Guid EnrollmentId { get; private set; }
    public DateTime JoinedAt { get; private set; }
    
    private GroupMember() { }

    public GroupMember(Guid enrollmentId, DateTime joinedAt)
    {
        if (enrollmentId == Guid.Empty)
            throw new GroupsDomainException("Enrollment ID cannot be empty.");
        
        Id = Guid.NewGuid();
        EnrollmentId = enrollmentId;
        JoinedAt = joinedAt;
    }
}