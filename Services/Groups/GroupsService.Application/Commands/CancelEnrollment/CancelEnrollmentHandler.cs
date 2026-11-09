using GroupsService.Application.Interfaces;
using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Application.Commands.CancelEnrollment;

public class CancelEnrollmentHandler
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IGroupRepository _groupRepository;

    public CancelEnrollmentHandler(IEnrollmentRepository enrollmentRepository, IGroupRepository groupRepository)
    {
        _enrollmentRepository = enrollmentRepository;
        _groupRepository = groupRepository;
    }

    public async Task Handle(CancelEnrollmentCommand command)
    {
        var enrollment = await _enrollmentRepository.GetByIdAsync(command.EnrollmentId)
            ?? throw new GroupsDomainException(
                $"Enrollment with id {command.EnrollmentId} does not exist.");

        if (!command.IsStaff && enrollment.UserId != command.RequestingUserId)
            throw new GroupsDomainException("You can only cancel your own enrollment.");

        // NAPOMENA: penal se NE racuna ovde - Domain samo belezi otkazivanje i kada.
        // Payments servis (faza 2) racuna da li penal vazi na osnovu CancelledAt i
        // konfigurabilnih pravila (besplatno pre formiranja grupe, posle toga penal).
        // Enrollment.Cancel() brise GroupId, pa ga pamtimo unapred.
        var groupId = enrollment.GroupId;
        enrollment.Cancel();

        if (groupId.HasValue)
        {
            var group = await _groupRepository.GetByIdAsync(groupId.Value)
                        ?? throw new GroupsDomainException($"Group with id {groupId.Value} does not exist.");

            var wasReady = group.Status == GroupStatus.Ready;
            group.RemoveMember(enrollment.Id);
            await HandleGroupAfterRemovalAsync(group, wasReady);

            await _groupRepository.SaveChangesAsync();
        }

        await _enrollmentRepository.SaveChangesAsync();
    }
    
    // Posledice odlaska clana po grupu koja jos nije pocela:
    // - Pair i Individual: grupa se rasformira, preostali clan (partner) se vraca u
    //   Searching bez grupe i moze da pozove novog partnera.
    // - Group: ako je grupa pala iz Ready u Forming, ostali clanovi se vracaju u
    //   Searching (invarijanta: upis je Ready samo dok je grupa Ready). Profesor/admin
    //   i dalje moze da forsira Start ispod minimuma.
    private async Task HandleGroupAfterRemovalAsync(Group group, bool wasReady)
    {
        if (group.Status == GroupStatus.Active)
        {
            // Aktivna grupa ostaje aktivna dok ima clanova (npr. Pair: preostali nastavlja
            // sam). Ako je ostala prazna (npr. Individual ciji je jedini clan otkazao),
            // zatvara se da ne drzi termine profesora.
            if (group.MemberCount == 0)
                group.Complete();

            return;
        }

        if (group.Status is not (GroupStatus.Forming or GroupStatus.Ready))
            return; // Completed/Dissolved grupa ostaje kakva jeste

        var remaining = await _enrollmentRepository.GetByGroupIdAsync(group.Id);

        if (group.Type is GroupType.Pair or GroupType.Individual)
        {
            foreach (var member in remaining)
                member.LeaveGroup();

            group.Dissolve();
            return;
        }

        if (wasReady && group.Status == GroupStatus.Forming)
        {
            foreach (var member in remaining.Where(m => m.Status == EnrollmentStatus.Ready))
                member.RevertToSearching();
        }
    }
}