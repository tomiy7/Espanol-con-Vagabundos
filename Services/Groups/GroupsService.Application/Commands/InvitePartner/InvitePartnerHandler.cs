using GroupsService.Application.Interfaces;
using GroupsService.Application.Options;
using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace GroupsService.Application.Commands.InvitePartner;

public class InvitePartnerHandler
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IPartnerInvitationRepository _invitationRepository;
    private readonly EnrollmentOptions _options;
    
    public InvitePartnerHandler(
        IEnrollmentRepository enrollmentRepository,
        IPartnerInvitationRepository invitationRepository,
        IOptions<EnrollmentOptions> options)
    {
        _enrollmentRepository = enrollmentRepository;
        _invitationRepository = invitationRepository;
        _options = options.Value;
    }

    public async Task<InvitePartnerResult> Handle(InvitePartnerCommand command)
    {
        if (command.InviterUserId == command.InviteeUserId)
            throw new GroupsDomainException("Cannot invite yourself as a partner.");

        // Provera i za pozvanog, ne samo inicijatora - nema smisla slati poziv (i mejl)
        // osobi koja vec ima aktivan/searching upis za ovaj kurs, kad ce svakako da
        // propadne na Accept-u (vidi istu proveru u AcceptInvitationHandler, koja je
        // tu kao odbrana druge linije za slucaj da se stanje promeni izmedju invite-a
        // i accept-a).
        var inviteeExisting = await _enrollmentRepository.GetActiveOrSearchingAsync(
            command.InviteeUserId, command.CourseId);
        if (inviteeExisting is not null)
            throw new GroupsDomainException("This person already has an active or pending enrollment for this course.");

        // Inicijatorov Enrollment: ako vec postoji Searching Pair enrollment za ovaj
        // kurs (ovo je retry nakon odbijenog/isteklog poziva), ponovo ga koristimo -
        // ne pravimo novi red svaki put kad neko pokusa ponovo.
        var existing = await _enrollmentRepository.GetActiveOrSearchingAsync(command.InviterUserId, command.CourseId);

        Enrollment inviterEnrollment;
        if (existing is { Type: EnrollmentType.Pair, GroupId: null })
        {
            inviterEnrollment = existing;
        }
        else if (existing is not null)
        {
            throw new GroupsDomainException("You already have an active or pending enrollment for this course.");
        }
        else
        {
            inviterEnrollment =
                Enrollment.CreateSearching(command.InviterUserId, command.CourseId, EnrollmentType.Pair);
            await _enrollmentRepository.AddAsync(inviterEnrollment);
        }
        
        var previousAttempts = await _invitationRepository.GetByInviterAndInviteeAsync(
            inviterEnrollment.Id, command.InviteeUserId);

        var attemptNumber = previousAttempts.Count + 1;
        if (attemptNumber > _options.PartnerInvitationMaxAttempts)
            throw new GroupsDomainException(
                $"Maximum number of invitation attempts ({_options.PartnerInvitationMaxAttempts}) reached for this partner.");

        var invitation = PartnerInvitation.Create(
            inviterEnrollment.Id, command.InviteeUserId, attemptNumber, _options.PairInvitationExpiryDays);

        await _invitationRepository.AddAsync(invitation);
        await _enrollmentRepository.SaveChangesAsync();
        await _invitationRepository.SaveChangesAsync();
        
        return new InvitePartnerResult(inviterEnrollment.Id, invitation.Id);
    }
}