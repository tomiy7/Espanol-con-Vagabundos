using GroupsService.Application.Interfaces;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Application.Commands.ChangePaymentPlan;

public record ChangePaymentPlanCommand(Guid EnrollmentId, Guid RequestingUserId, bool IsStaff, PaymentPlan PaymentPlan);

// Promena izbora placanja dok grupa nije potvrdjena (posle potvrde uplatnice vec postoje,
// pa izbor menja Payments, koji zna da li je nesto placeno). Ucenik menja svoj upis,
// osoblje bilo koji. Tudji upis se prijavljuje kao nepostojeci (404), kao i pri citanju.
public class ChangePaymentPlanHandler
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IGroupRepository _groupRepository;

    public ChangePaymentPlanHandler(IEnrollmentRepository enrollmentRepository, IGroupRepository groupRepository)
    {
        _enrollmentRepository = enrollmentRepository;
        _groupRepository = groupRepository;
    }

    public async Task Handle(ChangePaymentPlanCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var enrollment = await _enrollmentRepository.GetByIdAsync(command.EnrollmentId);
        if (enrollment is null || (!command.IsStaff && enrollment.UserId != command.RequestingUserId))
            throw EntityNotFoundException.Enrollment(command.EnrollmentId);

        enrollment.ChangePaymentPlan(command.PaymentPlan);

        if (enrollment.GroupId is { } groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId)
                ?? throw EntityNotFoundException.Group(groupId);
            group.ChangeMemberPaymentPlan(enrollment.Id, command.PaymentPlan);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _enrollmentRepository.SaveChangesAsync();
        await _groupRepository.SaveChangesAsync();
    }
}
