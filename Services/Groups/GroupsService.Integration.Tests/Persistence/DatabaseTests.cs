using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GroupsService.Integration.Tests.Persistence;

// Provere nad pravom bazom: migracija se poklapa sa modelom, a ogranicenja (indeksi,
// concurrency token) stvarno rade - to InMemory provider ne bi uhvatio.
public class DatabaseTests : ApiTestBase
{
    public DatabaseTests(ApiFactory factory) : base(factory) { }

    [IntegrationFact]
    public void Migration_matches_the_current_model()
    {
        var pending = Factory.WithDb(db => db.Database.HasPendingModelChanges());
        Assert.False(pending, "The model changed after the migration was generated - add a new migration.");
    }

    [IntegrationFact]
    public void Expected_tables_exist_and_lesson_progress_table_does_not()
    {
        var tables = Factory.WithDb(db =>
            db.Database.SqlQueryRaw<string>(
                "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'").ToList());

        foreach (var expected in new[] { "enrollments", "groups", "group_members", "group_slots", "partner_invitations", "audit_entries" })
            Assert.Contains(expected, tables);

        Assert.DoesNotContain("enrollment_completed_lessons", tables);
    }

    [IntegrationFact]
    public void Unique_index_blocks_a_second_non_cancelled_enrollment_for_the_same_course()
    {
        var userId = Guid.NewGuid();

        Factory.WithDb(db =>
        {
            db.Enrollments.Add(Enrollment.CreateSearching(userId, CourseId, EnrollmentType.Group));
            db.SaveChanges();
            return 0;
        });

        var ex = Assert.Throws<DbUpdateException>(() => Factory.WithDb(db =>
        {
            db.Enrollments.Add(Enrollment.CreateSearching(userId, CourseId, EnrollmentType.Group));
            db.SaveChanges();
            return 0;
        }));

        Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal("23505", ((PostgresException)ex.InnerException!).SqlState);
    }

    [IntegrationFact]
    public void Unique_index_allows_a_new_enrollment_after_the_old_one_was_cancelled()
    {
        var userId = Guid.NewGuid();

        Factory.WithDb(db =>
        {
            var first = Enrollment.CreateSearching(userId, CourseId, EnrollmentType.Group);
            db.Enrollments.Add(first);
            db.SaveChanges();
            first.Cancel();
            db.SaveChanges();

            db.Enrollments.Add(Enrollment.CreateSearching(userId, CourseId, EnrollmentType.Group));
            db.SaveChanges();
            return 0;
        });

        var count = Factory.WithDb(db => db.Enrollments.Count(e => e.UserId == userId));
        Assert.Equal(2, count);
    }

    [IntegrationFact]
    public void Stale_group_write_is_rejected_by_the_version_token()
    {
        var groupId = Factory.WithDb(db =>
        {
            var group = Group.CreateGroup(CourseId, 7);
            db.Groups.Add(group);
            db.SaveChanges();
            return group.Id;
        });

        // dva "korisnika" ucitaju istu grupu, pa oba pokusaju da dodaju clana
        using var scopeA = Factory.Services.CreateScope();
        using var scopeB = Factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<GroupsService.Infrastructure.Data.GroupDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<GroupsService.Infrastructure.Data.GroupDbContext>();

        var groupA = dbA.Groups.Include(g => g.Members).Include(g => g.Slots).Single(g => g.Id == groupId);
        var groupB = dbB.Groups.Include(g => g.Members).Include(g => g.Slots).Single(g => g.Id == groupId);

        groupA.AddMember(Guid.NewGuid());
        dbA.SaveChanges();

        groupB.AddMember(Guid.NewGuid());
        Assert.Throws<DbUpdateConcurrencyException>(() => dbB.SaveChanges());
    }

    [IntegrationFact]
    public void Enrollment_row_version_rejects_a_stale_update()
    {
        var enrollmentId = Factory.WithDb(db =>
        {
            var enrollment = Enrollment.CreateSearching(Guid.NewGuid(), CourseId, EnrollmentType.Group);
            db.Enrollments.Add(enrollment);
            db.SaveChanges();
            return enrollment.Id;
        });

        // dva "korisnika" ucitaju isti upis; prvi ga otkaze, drugi (sa zastarelim
        // podacima) pokusa da ga otkaze ponovo - xmin to mora da odbije
        using var scopeA = Factory.Services.CreateScope();
        using var scopeB = Factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<GroupsService.Infrastructure.Data.GroupDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<GroupsService.Infrastructure.Data.GroupDbContext>();

        var enrollmentA = dbA.Enrollments.Single(e => e.Id == enrollmentId);
        var enrollmentB = dbB.Enrollments.Single(e => e.Id == enrollmentId);

        enrollmentA.Cancel();
        dbA.SaveChanges();

        enrollmentB.Cancel();
        Assert.Throws<DbUpdateConcurrencyException>(() => dbB.SaveChanges());
    }

    [IntegrationFact]
    public void Row_version_is_the_postgres_system_column_not_a_new_column()
    {
        var columns = Factory.WithDb(db =>
            db.Database.SqlQueryRaw<string>(
                "SELECT column_name AS \"Value\" FROM information_schema.columns " +
                "WHERE table_name IN ('enrollments', 'partner_invitations') AND column_name = 'xmin'").ToList());

        Assert.Empty(columns);
    }
}