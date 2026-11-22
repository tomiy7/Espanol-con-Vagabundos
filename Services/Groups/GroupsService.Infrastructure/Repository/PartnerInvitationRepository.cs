using GroupsService.Application.Interfaces;
using GroupsService.Domain.Entities;
using GroupsService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GroupsService.Infrastructure.Repository;

public class PartnerInvitationRepository : RepositoryBase<PartnerInvitation>, IPartnerInvitationRepository
{
    private readonly GroupDbContext _db;
    
    public PartnerInvitationRepository(GroupDbContext dbContext) : base(dbContext)
    {
        _db = dbContext;
    }

    public async Task<PartnerInvitation?> GetByTokenAsync(string token) =>
        await _db.PartnerInvitations.FirstOrDefaultAsync(i => i.Token == token);

    public async Task<List<PartnerInvitation>> GetByInviterAndInviteeAsync(Guid inviterEnrollmentId, Guid inviteeUserId) =>
        await _db.PartnerInvitations
            .Where(i => i.InviterEnrollmentId == inviterEnrollmentId && i.InviteeUserId == inviteeUserId)
            .ToListAsync();
}