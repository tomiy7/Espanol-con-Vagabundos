using GroupsService.Application.Interfaces;
using GroupsService.Application.Options;
using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace GroupsService.Application.Commands.CreateGroupEnrollment;

public class CreateGroupEnrollmentHandler
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly EnrollmentOptions _options;
    
    public CreateGroupEnrollmentHandler(
        IEnrollmentRepository enrollmentRepository,
        IGroupRepository groupRepository,
        IOptions<EnrollmentOptions> options)
    {
        _enrollmentRepository = enrollmentRepository;
        _groupRepository = groupRepository;
        _options = options.Value;
    }

    public async Task<CreateGroupEnrollmentResult> Handle(CreateGroupEnrollmentCommand command)
    {
        // Ista provera kao u CreateIndividualEnrollmentHandler/InvitePartnerHandler
        var existingEnrollment = await _enrollmentRepository.GetActiveOrSearchingAsync(command.UserId, command.CourseId);
        if (existingEnrollment is not null)
            throw new GroupsDomainException("You already have an active or pending enrollment for this course.");

        var enrollment = Enrollment.CreateSearching(command.UserId, command.CourseId, EnrollmentType.Group);
        await _enrollmentRepository.AddAsync(enrollment);
        
        // Prvo trazimo otvorenu grupu (Forming/Ready, ispod MaxSize) za ovaj kurs -
        // profesori/admin rucno raspodeljuju kad prozor istekne sa "neravnim" brojem
        // prijavljenih (npr. 8 ljudi -> 4+4), tako da ne pokusavamo to da automatizujemo.
        var group = await _groupRepository.FindOpenGroupForCourseAsync(command.CourseId, GroupType.Group);

        bool isNewGroup = group is null;
        if (group is null)
        {
            group = Group.CreateGroup(command.CourseId, _options.GroupFormationWindowDays);
            await _groupRepository.AddAsync(group);
        }
        
        var wasReadyBefore = group.Status == GroupStatus.Ready;
        group.AddMember(enrollment.Id);
        enrollment.AssignToGroup(group.Id);
        
        var becameReadyNow = !wasReadyBefore && group.Status == GroupStatus.Ready;
        if (becameReadyNow)
        {
            // Svi postojeci clanovi (ukljucujuci upravo dodatog) prelaze u Ready
            var members = await _enrollmentRepository.GetByGroupIdAsync(group.Id);
            foreach (var member in members)
                member.MarkReady();
        }
        
        await _enrollmentRepository.SaveChangesAsync();
        await _groupRepository.SaveChangesAsync();

        // notifikacija TODO
        
        return new CreateGroupEnrollmentResult(enrollment.Id, group.Id);
    }
}