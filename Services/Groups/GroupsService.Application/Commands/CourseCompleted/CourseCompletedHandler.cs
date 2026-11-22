using GroupsService.Application.Interfaces;
using GroupsService.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GroupsService.Application.Commands.CourseCompleted;

// Poziva ga potrosac poruka kad stigne CourseCompleted (iz Courses). Napredak po
// lekcijama vodi Courses; Groups samo upisuje cinjenicu da je upis zavrsen.
// Event koji ne moze da se primeni (nepoznat/tudj/nije-Active upis, ili vec zavrsen)
// se LOGUJE I IGNORISE, ne baca izuzetak - bacanje bi vratilo poruku u red i vrtelo je
// u krug za stvar koja se nikad nece promeniti. Zbog toga je handler i idempotentan:
// ista poruka dvaput ne menja nista. Jedini izuzetak je ConcurrencyConflictException
// (prolazna trka sa drugim clanom grupe) - tu ponavljanje poruke resava stvar.
public class CourseCompletedHandler
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly ILogger<CourseCompletedHandler> _logger;

    public CourseCompletedHandler(
        IEnrollmentRepository enrollmentRepository,
        IGroupRepository groupRepository,
        ILogger<CourseCompletedHandler> logger)
    {
        _enrollmentRepository = enrollmentRepository;
        _groupRepository = groupRepository;
        _logger = logger;
    }

    public async Task Handle(CourseCompletedCommand command)
    {
        var enrollment = await _enrollmentRepository.GetByIdAsync(command.EnrollmentId);
        if (enrollment is null)
        {
            _logger.LogWarning("CourseCompleted for non existing enrollment {EnrollmentId} - ignoring.", command.EnrollmentId);
            return;
        }

        // Odbrana od pogresno adresiranog event-a: upis mora da pripada bas tom
        // korisniku i kursu, inace Courses i Groups ne govore o istoj stvari.
        if (enrollment.UserId != command.UserId || enrollment.CourseId != command.CourseId)
        {
            _logger.LogWarning(
                "CourseCompleted not relating to enrollment {EnrollmentId} (user/course don't match) - ignoring.",
                command.EnrollmentId);
            return;
        }

        // Vec zavrsen upis: duplikat poruke (isporuka je "bar jednom"), nema sta da se radi.
        if (enrollment.Status == EnrollmentStatus.Completed)
            return;

        // Zamrznut/otkazan/jos neaktivan upis se ne zavrsava. Courses bi trebalo da
        // proveri pristup (CheckAccess) pre nego sto posluzi lekciju, ovo je druga
        // linija odbrane.
        if (enrollment.Status != EnrollmentStatus.Active)
        {
            _logger.LogWarning(
                "CourseCompleted for enrollment {EnrollmentId} in status {Status} - ignoring.",
                enrollment.Id, enrollment.Status);
            return;
        }

        enrollment.Complete();

        var group = enrollment.GroupId.HasValue
            ? await _groupRepository.GetByIdAsync(enrollment.GroupId.Value)
            : null;

        if (group is { Status: GroupStatus.Active } && group.HasMember(enrollment.Id))
        {
            // Povecava Version grupe, pa se istovremeni zavrseci clanova iste grupe
            // serijalizuju (vidi Group.RecordMemberCompleted).
            group.RecordMemberCompleted(enrollment.Id);

            if (await AllRemainingMembersCompletedAsync(group.Id))
                group.Complete();
        }

        // Upis i grupa se snimaju zajedno (isti DbContext, jedna transakcija). Ako je drugi
        // clan iste grupe zavrsio u istom trenutku, ovde leti ConcurrencyConflictException
        // i nista se ne snima: potrosac poruka treba da ponovi poruku (retry/requeue),
        // a ponovljeni pokusaj tada vidi novo stanje i po potrebi zatvara grupu.
        await _enrollmentRepository.SaveChangesAsync();
    }

    // Grupa se automatski zavrsava kad SVI njeni clanovi koji nisu otkazali i nisu
    // zamrznuti zavrse kurs. Zamrznuti clanovi se ignorisu (ne cekaju se). Ako su
    // SVI preostali zamrznuti, grupa se ne zatvara automatski. Upisi su tracked, pa
    // upis koji se upravo zavrsava ovde vec ima status Completed.
    private async Task<bool> AllRemainingMembersCompletedAsync(Guid groupId)
    {
        var members = await _enrollmentRepository.GetByGroupIdAsync(groupId);
        var relevant = members
            .Where(m => m.Status is not (EnrollmentStatus.Cancelled or EnrollmentStatus.Frozen))
            .ToList();

        return relevant.Count > 0 && relevant.All(m => m.Status == EnrollmentStatus.Completed);
    }
}
