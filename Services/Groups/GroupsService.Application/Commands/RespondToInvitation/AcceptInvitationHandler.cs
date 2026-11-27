using GroupsService.Application.Interfaces;
using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Application.Commands.RespondToInvitation;

public class AcceptInvitationHandler
{
    private readonly IPartnerInvitationRepository _invitationRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IGroupRepository _groupRepository;
    
    public AcceptInvitationHandler(
        IPartnerInvitationRepository invitationRepository,
        IEnrollmentRepository enrollmentRepository,
        IGroupRepository groupRepository)
    {
        _invitationRepository = invitationRepository;
        _enrollmentRepository = enrollmentRepository;
        _groupRepository = groupRepository;
    }

    public async Task<AcceptInvitationResult> Handle(AcceptInvitationCommand command)
    {
        var invitation = await _invitationRepository.GetByTokenAsync(command.Token)
            ?? throw EntityNotFoundException.Invitation();

        var inviterEnrollment = await _enrollmentRepository.GetByIdAsync(invitation.InviterEnrollmentId)
            ?? throw new GroupsDomainException("Inviter enrollment no longer exists.");
        
        // Inicijator mora i dalje da trazi partnera: moze imati vise poziva na cekanju
        // (npr. poslao je dvojici), pa je drugi accept zakasnio, ili je inicijator
        // u medjuvremenu otkazao.
        if (inviterEnrollment.Status != EnrollmentStatus.Searching || inviterEnrollment.GroupId.HasValue)
            throw new GroupsDomainException("The person who invited you is no longer looking for a partner.");
        
        // Druga linija odbrane (prva je u InvitePartnerHandler, u trenutku slanja
        // poziva) - stanje se moglo promeniti izmedju invite-a i ovog klika (npr.
        // pozvani se u medjuvremenu upisao individualno na isti kurs).
        var inviteeExisting = await _enrollmentRepository.GetNonCancelledAsync(
            invitation.InviteeUserId, inviterEnrollment.CourseId);
        if (inviteeExisting is not null)
            throw EnrollmentGuard.Blocked(inviteeExisting);

        var inviteeEnrollment = Enrollment.CreateSearching(
            invitation.InviteeUserId, inviterEnrollment.CourseId, EnrollmentType.Pair,
            command.PaymentPlan ?? PaymentPlan.Full);

        var group = Group.CreatePair(inviterEnrollment.CourseId);

        group.AddMember(inviterEnrollment.Id, inviterEnrollment.UserId, inviterEnrollment.PaymentPlan);
        group.AddMember(inviteeEnrollment.Id, inviteeEnrollment.UserId, inviteeEnrollment.PaymentPlan);

        inviterEnrollment.AssignToGroup(group.Id);
        inviteeEnrollment.AssignToGroup(group.Id);

        // Pair ima MinSize=MaxSize=2, pa je group.Status vec Ready nakon drugog AddMember.
        inviterEnrollment.MarkReady();
        inviteeEnrollment.MarkReady();

        invitation.Accept(group.Id);
        
        await _enrollmentRepository.AddAsync(inviteeEnrollment);
        await _groupRepository.AddAsync(group);

        await _enrollmentRepository.SaveChangesAsync();
        await _groupRepository.SaveChangesAsync();
        await _invitationRepository.SaveChangesAsync();
        
        
        return new AcceptInvitationResult(group.Id, inviteeEnrollment.Id);
    }
}