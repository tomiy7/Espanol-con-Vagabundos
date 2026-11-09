using GroupsService.Application.Interfaces;
using GroupsService.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GroupsService.Application.Commands.RecordLessonCompleted;

// Poziva ga RabbitMqEventConsumer kad stigne LessonCompleted (iz Courses).
// Event koji ne moze da se primeni (nepoznat/tudj/nije-Active upis) se LOGUJE I
// IGNORISE, ne baca izuzetak - bacanje bi vratilo poruku u red i vrtelo je u krug
// za stvar koja se nikad nece promeniti.
public class RecordLessonCompletedHandler
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly ILogger<RecordLessonCompletedHandler> _logger;

    public RecordLessonCompletedHandler(
        IEnrollmentRepository enrollmentRepository,
        IGroupRepository groupRepository,
        ILogger<RecordLessonCompletedHandler> logger)
    {
        _enrollmentRepository = enrollmentRepository;
        _groupRepository = groupRepository;
        _logger = logger;
    }

    public async Task Handle(RecordLessonCompletedCommand command)
    {
        var enrollment = await _enrollmentRepository.GetByIdAsync(command.EnrollmentId);
        if (enrollment is null)
        {
            _logger.LogWarning("LessonCompleted for non existing enrollment {EnrollmentId} - ignoring.", command.EnrollmentId);
            return;
        }

        // Odbrana od pogresno adresiranog event-a: upis mora da pripada baš tom
        // korisniku i kursu, inace Courses i Groups ne govore o istoj stvari.
        if (enrollment.UserId != command.UserId || enrollment.CourseId != command.CourseId)
        {
            _logger.LogWarning(
                "LessonCompleted not relating to enrollment {EnrollmentId} (user/course don't match) - ignoring.",
                command.EnrollmentId);
            return;
        }

        // Zamrznut/otkazan/jos neaktivan upis ne napreduje. Courses bi trebalo da
        // proveri pristup (CheckAccess) pre nego sto posluzi lekciju, ovo je druga
        // linija odbrane.
        if (enrollment.Status != EnrollmentStatus.Active)
        {
            _logger.LogWarning(
                "LessonCompleted for enrollment {EnrollmentId} in status {Status} - ignoring.",
                enrollment.Id, enrollment.Status);
            return;
        }

        var recorded = enrollment.RecordLessonCompleted(command.LessonId, command.TotalLessons);
        if (!recorded)
            return; // duplikat event-a, vec zabelezeno

        await _enrollmentRepository.SaveChangesAsync();

        if (enrollment.Status == EnrollmentStatus.Completed && enrollment.GroupId.HasValue)
            await TryCompleteGroupAsync(enrollment.GroupId.Value);
    }

    // Grupa se automatski zavrsava kad SVI njeni clanovi koji nisu otkazali i nisu
    // zamrznuti zavrse kurs. Zamrznuti clanovi se ignorisu (ne cekaju se). Ako su
    // SVI preostali zamrznuti, grupa se ne zatvara automatski.
    private async Task TryCompleteGroupAsync(Guid groupId)
    {
        var group = await _groupRepository.GetByIdAsync(groupId);
        if (group is null || group.Status != GroupStatus.Active)
            return;

        var members = await _enrollmentRepository.GetByGroupIdAsync(groupId);
        var relevant = members
            .Where(m => m.Status is not (EnrollmentStatus.Cancelled or EnrollmentStatus.Frozen))
            .ToList();

        if (relevant.Count == 0 || !relevant.All(m => m.Status == EnrollmentStatus.Completed))
            return;

        group.Complete();
        await _groupRepository.SaveChangesAsync();
    }
}
