using GroupsService.Application.Interfaces;

namespace GroupsService.Application.Queries.GetInvitationByToken;

// Koristi ga API da pre Accept/Decline klika pokaze "pozvao te X na kurs Y" stranicu
// (korisno ako frontend pravi svoju landing stranicu umesto da API direktno radi
// Accept cim se link otvori - vidi SETUP.md za oba pristupa).
public class GetInvitationByTokenHandler
{
    private readonly IPartnerInvitationRepository _invitationRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IUserDirectory _userDirectory;
    private readonly ICourseDirectory _courseDirectory;

    public GetInvitationByTokenHandler(
        IPartnerInvitationRepository invitationRepository,
        IEnrollmentRepository enrollmentRepository,
        IUserDirectory userDirectory,
        ICourseDirectory courseDirectory)
    {
        _invitationRepository = invitationRepository;
        _enrollmentRepository = enrollmentRepository;
        _userDirectory = userDirectory;
        _courseDirectory = courseDirectory;
    }

    public async Task<InvitationDetailDto?> Handle(GetInvitationByTokenQuery query)
    {
        var invitation = await _invitationRepository.GetByTokenAsync(query.Token);
        if (invitation == null) return null;

        var inviterEnrollment = await _enrollmentRepository.GetByIdAsync(invitation.InviterEnrollmentId);
        if (inviterEnrollment == null) return null;

        var courseName = await _courseDirectory.GetCourseNameAsync(inviterEnrollment.CourseId);
        var inviter = await _userDirectory.GetUserContactAsync(inviterEnrollment.UserId);

        return new InvitationDetailDto(
            Id: invitation.Id,
            Status: invitation.Status,
            IsExpired: invitation.IsExpired,
            CourseId: inviterEnrollment.CourseId,
            CourseName: courseName,
            InviterName: inviter?.FullName);
    }
}