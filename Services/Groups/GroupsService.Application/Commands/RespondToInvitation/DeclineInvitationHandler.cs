using GroupsService.Application.Interfaces;
using GroupsService.Application.Options;
using GroupsService.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace GroupsService.Application.Commands.RespondToInvitation;

public class DeclineInvitationHandler
{
    private readonly IPartnerInvitationRepository _invitationRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly EnrollmentOptions _options;
    
    public DeclineInvitationHandler(
        IPartnerInvitationRepository invitationRepository,
        IEnrollmentRepository enrollmentRepository,
        IOptions<EnrollmentOptions> options)
    {
        _invitationRepository = invitationRepository;
        _enrollmentRepository = enrollmentRepository;
        _options = options.Value;
    }

    public async Task Handle(DeclineInvitationCommand command)
    {
        var invitation = await _invitationRepository.GetByTokenAsync(command.Token)
            ?? throw EntityNotFoundException.Invitation();

        invitation.Decline();
        await _invitationRepository.SaveChangesAsync();

        // TODO notifikacija
    }
}