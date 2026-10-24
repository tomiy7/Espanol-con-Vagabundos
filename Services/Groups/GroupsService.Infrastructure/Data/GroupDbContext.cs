using GroupsService.Domain.Entities;
using GroupsService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupsService.Infrastructure.Data;

public class GroupDbContext : DbContext
{
    public GroupDbContext(DbContextOptions<GroupDbContext> options) : base(options) { }
    
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<PartnerInvitation> PartnerInvitations => Set<PartnerInvitation>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new EnrollmentConfiguration());
        modelBuilder.ApplyConfiguration(new GroupConfiguration());
        modelBuilder.ApplyConfiguration(new PartnerInvitationConfiguration());
    }
}