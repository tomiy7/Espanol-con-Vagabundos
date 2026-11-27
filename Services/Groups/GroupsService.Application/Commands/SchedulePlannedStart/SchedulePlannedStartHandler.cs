using GroupsService.Application.Interfaces;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Application.Commands.SchedulePlannedStart;

// Postavlja ili pomera datum prvog casa. Kad grupa ima i dovoljno clanova i datum,
// domen je potvrdjuje (GroupConfirmed), a Payments na to izdaje uplatnice.
public class SchedulePlannedStartHandler
{
    private readonly IGroupRepository _groupRepository;

    public SchedulePlannedStartHandler(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task Handle(SchedulePlannedStartCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var group = await _groupRepository.GetByIdAsync(command.GroupId)
            ?? throw EntityNotFoundException.Group(command.GroupId);

        group.SchedulePlannedStart(ToUtc(command.PlannedStartAt), DateTime.UtcNow);

        cancellationToken.ThrowIfCancellationRequested();
        await _groupRepository.SaveChangesAsync();
    }

    // Datum bez zone se tumaci kao UTC; sa zonom se prevodi u UTC (baza cuva UTC).
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
