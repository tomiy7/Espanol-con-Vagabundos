using GroupsService.Domain.Exceptions;

namespace GroupsService.Domain.ValueObjects;

// Clanstvo u grupi/paru. Vlasnik upisa je Enrollment (EnrollmentId); UserId se cuva
// uz clana da bi dogadjaji grupe (Ready, Start, rasformiranje) mogli da kazu KOME se
// to desilo (mejlovi, uplatnice) bez dodatnog citanja upisa.
// Zovemo ga Entity (ne cist ValueObject) zbog EF Core owned-collection mapiranja -
// ista odluka kao u prethodnoj verziji ovog koda.
public class GroupMember : Entity
{
    public Guid EnrollmentId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private GroupMember() { }

    public GroupMember(Guid enrollmentId, Guid userId, DateTime joinedAt)
    {
        if (enrollmentId == Guid.Empty)
            throw new GroupsDomainException("Enrollment ID cannot be empty.");

        if (userId == Guid.Empty)
            throw new GroupsDomainException("User ID cannot be empty.");

        Id = Guid.NewGuid();
        EnrollmentId = enrollmentId;
        UserId = userId;
        JoinedAt = joinedAt;
    }
}