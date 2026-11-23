using GroupsService.Domain.Entities;
using GroupsService.Domain.Interfaces;

namespace GroupsService.Application.Interfaces;

public interface IPartnerInvitationRepository : IAsyncRepository<PartnerInvitation>
{
    Task<PartnerInvitation?> GetByTokenAsync(string token);

    // Za racunanje AttemptNumber (max 2 pokusaja za istog partnera na istom kursu)
    Task<List<PartnerInvitation>> GetByInviterAndInviteeAsync(Guid inviterEnrollmentId, Guid inviteeUserId);
}