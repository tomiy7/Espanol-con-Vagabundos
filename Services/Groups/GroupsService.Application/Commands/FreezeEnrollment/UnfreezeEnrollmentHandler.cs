using GroupsService.Application.Interfaces;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Application.Commands.FreezeEnrollment;

public class UnfreezeEnrollmentHandler
{
    private readonly IEnrollmentRepository _repository;

    public UnfreezeEnrollmentHandler(IEnrollmentRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UnfreezeEnrollmentCommand command)
    {
        var enrollment = await _repository.GetByIdAsync(command.EnrollmentId)
            ?? throw EntityNotFoundException.Enrollment(command.EnrollmentId);

        enrollment.Unfreeze();
        await _repository.SaveChangesAsync();
    }
}