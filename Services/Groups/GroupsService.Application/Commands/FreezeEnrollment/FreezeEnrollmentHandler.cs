using GroupsService.Application.Interfaces;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Application.Commands.FreezeEnrollment;

public class FreezeEnrollmentHandler
{
    private readonly IEnrollmentRepository _repository;

    public FreezeEnrollmentHandler(IEnrollmentRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(FreezeEnrollmentCommand command)
    {
        var enrollment = await _repository.GetByIdAsync(command.EnrollmentId)
                         ?? throw EntityNotFoundException.Enrollment(command.EnrollmentId);

        // NAPOMENA: "odobreno od strane admina/profesora" se za sad svodi na to da
        // SAMO admin/professor rolu moze da pozove ovaj endpoint (proverava se u
        // kontroleru). Nema posebnog Approval zapisa - ApprovedByUserId je samo trag
        // ko je to odobrio.
        enrollment.Freeze(command.ApprovedByUserId);
        await _repository.SaveChangesAsync();

        // TODO notifikacija
    }
}