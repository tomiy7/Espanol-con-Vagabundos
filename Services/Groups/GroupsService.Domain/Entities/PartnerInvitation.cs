using System.Security.Cryptography;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Events;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Domain.Entities;

// Poziv za upis "u paru" - jedna osoba (postojeci korisnik) pozove drugog postojeceg
// korisnika preko mejla (magic link: Token u URL-u je sam po sebi autentikacija za
// Accept/Decline - te rute namerno NEMAJU JWT zahtev, token je dovoljan i jednokratan).
public class PartnerInvitation : AggregateRoot
{
    public Guid InviterEnrollmentId { get; private set; }
    public Guid InviteeUserId { get; private set; }
    public InvitationStatus Status { get; private set; }

    // Koliko je ovo puta ista osoba pozvana istog partnera za isti kurs (max 2 - vidi config)
    public int AttemptNumber { get; private set; }

    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RespondedAt { get; private set; }

    private PartnerInvitation() { }

    public static PartnerInvitation Create(
        Guid inviterEnrollmentId, Guid inviteeUserId, int attemptNumber, int expiryDays)
    {
        if (inviterEnrollmentId == Guid.Empty)
            throw new GroupsDomainException("Inviter enrollment ID cannot be empty.");

        if (inviteeUserId == Guid.Empty)
            throw new GroupsDomainException("Invitee user ID cannot be empty.");

        if (attemptNumber < 1)
            throw new GroupsDomainException("Attempt number must be at least 1.");

        if (expiryDays <= 0)
            throw new GroupsDomainException("Expiry days must be positive.");

        var invitation = new PartnerInvitation
        {
            Id = Guid.NewGuid(),
            InviterEnrollmentId = inviterEnrollmentId,
            InviteeUserId = inviteeUserId,
            AttemptNumber = attemptNumber,
            Status = InvitationStatus.Pending,
            Token = GenerateToken(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays)
        };

        invitation.AddDomainEvent(new PartnerInvited(invitation.Id, inviterEnrollmentId, inviteeUserId));
        return invitation;
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public void Accept(Guid groupId)
    {
        EnsurePendingAndNotExpired();

        Status = InvitationStatus.Accepted;
        RespondedAt = DateTime.UtcNow;
        AddDomainEvent(new InvitationAccepted(Id, groupId));
    }

    public void Decline()
    {
        EnsurePendingAndNotExpired();

        Status = InvitationStatus.Declined;
        RespondedAt = DateTime.UtcNow;
        AddDomainEvent(new InvitationDeclined(Id));
    } 
    
    // Lenjo oznacavanje isteka - poziva se kad handler primeti da je IsExpired=true
    // pre nego sto dozvoli Accept/Decline, umesto pozadinskog posla koji bi to radio.
    public void MarkExpired()
    {
        if (Status != InvitationStatus.Pending)
            throw new GroupsDomainException($"Cannot expire invitation in status {Status}.");

        Status = InvitationStatus.Expired;
    }
    
    private void EnsurePendingAndNotExpired()
    {
        if (IsExpired && Status == InvitationStatus.Pending)
        {
            MarkExpired();
            throw new GroupsDomainException("This invitation has expired.");
        }

        if (Status != InvitationStatus.Pending)
            throw new GroupsDomainException($"Invitation is not pending (status: {Status}).");
    }
}