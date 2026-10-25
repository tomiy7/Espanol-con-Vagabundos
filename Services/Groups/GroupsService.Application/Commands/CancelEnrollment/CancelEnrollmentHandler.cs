using GroupsService.Application.Interfaces;
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

        if (enrollment.UserId != command.RequestingUserId)
            throw new GroupsDomainException("You can only cancel your own enrollment.");

        // NAPOMENA: penal se NE racuna ovde - Domain samo belezi otkazivanje i kada.
        // Payments servis (faza 2) racuna da li penal vazi na osnovu CancelledAt i
        // konfigurabilnih pravila (besplatno pre formiranja grupe, posle toga penal).
        enrollment.Cancel();

        // Nalaz iz reviewa: bez ovoga Group._members ostaje "lazno pun" (Start() bi
        // prosao proveru MinSize sa clanom koji je upravo otkazao). Group.RemoveMember
        // sama vraca Ready->Forming ako padne ispod MinSize - admin/profesor i dalje
        // moze da forsira Start(force: true) ako odluci da ide dalje sa manje clanova.
        if (enrollment.GroupId.HasValue)
        {
            var group = await _groupRepository.GetByIdAsync(enrollment.GroupId.Value)
                        ?? throw new GroupsDomainException($"Group with id {enrollment.GroupId.Value} does not exist.");

            group.RemoveMember(enrollment.Id);
            await _groupRepository.SaveChangesAsync();
        }

        await _enrollmentRepository.SaveChangesAsync();
    }
}