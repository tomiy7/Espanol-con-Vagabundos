using GroupsService.Application.Interfaces;
using GroupsService.Domain.Entities;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Application.Commands.CreateIndividualEnrollment;

public class CreateIndividualEnrollmentHandler
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IGroupRepository _groupRepository;

    public CreateIndividualEnrollmentHandler(
        IEnrollmentRepository enrollmentRepository,
        IGroupRepository groupRepository)
    {
        _enrollmentRepository = enrollmentRepository;
        _groupRepository = groupRepository;
    }

    public async Task<CreateIndividualEnrollmentResult> Handle(CreateIndividualEnrollmentCommand command)
    {
        // Sprecava duplo upisivanje na isti kurs (bilo kojim tipom) - npr. duplo-klik
        // na "upisi se", ili pokusaj Individual upisa dok vec postoji Group/Pair upis
        // (Searching/Ready/Active) za isti kurs. Zavrseni/otkazani upisi NE blokiraju -
        // ponovno pohadjanje kursa je legitiman slucaj.
        var existing = await _enrollmentRepository.GetNonCancelledAsync(command.UserId, command.CourseId);
        
        if (existing is not null)
            throw EnrollmentGuard.Blocked(existing);

        // NAPOMENA: ne proveravamo da ProfessorId ima stvarno Professor rolu u Auth-u,
        // niti da CourseId postoji u Courses. API sloj je iza JWT-a; ovo ostaje tech debt.
        var enrollment = Enrollment.CreateIndividual(command.UserId, command.CourseId, command.ProfessorId);
        
        // Individual dobija SVOJU privatnu Group (nema deljenja sa drugima, za razliku
        // od CreateGroupEnrollmentHandler koji prvo trazi otvorenu grupu) - MinSize=
        // MaxSize=1, 1 slot. AddMember odmah prelazi u Ready (count=1>=MinSize=1), ali
        // Enrollment ostaje Searching->Ready, NE Active - profesor jos mora da potvrdi
        // termin preko AssignProfessorToSlot (isti endpoint kao za Pair/Group), i tek
        // StartGroupHandler (group.Start()) aktivira pristup kursu.
        var group = Group.CreateIndividual(command.CourseId);
        group.AddMember(enrollment.Id, enrollment.UserId);
        enrollment.AssignToGroup(group.Id);
        enrollment.MarkReady();
        
        await _enrollmentRepository.AddAsync(enrollment);
        await _groupRepository.AddAsync(group);

        await _enrollmentRepository.SaveChangesAsync();
        await _groupRepository.SaveChangesAsync();
        

        return new CreateIndividualEnrollmentResult(enrollment.Id, group.Id);
    }
}