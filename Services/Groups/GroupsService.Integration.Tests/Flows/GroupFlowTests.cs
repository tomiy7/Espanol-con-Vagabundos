using System.Net;
using GroupsService.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GroupsService.Integration.Tests.Flows;

// Group tip: profesor otvara grupu, ucenici se ubacuju ili sami ulaze, pa start, otkazivanje,
// zamrzavanje, dodela termina, zatvaranje i audit.
public class GroupFlowTests : ApiTestBase
{
    public GroupFlowTests(ApiFactory factory) : base(factory) { }

    // ---------- kreiranje ----------

    [IntegrationFact]
    public async Task Professor_creates_a_group_with_three_assigned_slots()
    {
        var professor = TestUser.Professor();

        var groupId = await CreateGroup(professor);
        var group = await GetGroup(professor, groupId);

        Assert.Equal("Group", group.GetProperty("type").GetString());
        Assert.Equal("Forming", group.GetProperty("status").GetString());
        Assert.Equal(3, group.GetProperty("minSize").GetInt32());
        Assert.Equal(6, group.GetProperty("maxSize").GetInt32());
        var slots = group.GetProperty("slots").EnumerateArray().ToList();
        Assert.Equal(3, slots.Count);
        Assert.All(slots, s => Assert.Equal(professor.Id, s.GetProperty("professorId").GetGuid()));
    }

    [IntegrationFact]
    public async Task Creating_a_group_validates_the_slots()
    {
        var professor = TestUser.Professor();
        var admin = TestUser.Admin();
        var other = TestUser.Professor();

        var twoSlots = await Post(professor, "/groups", new
        {
            courseId = CourseId,
            slots = Slots(professor.Id).Take(2).ToArray()
        });
        await AssertStatus(HttpStatusCode.BadRequest, twoSlots);
        Assert.Equal("CANNOT_CREATE_GROUP", await ErrorCode(twoSlots));

        var duplicate = await Post(professor, "/groups", new
        {
            courseId = CourseId,
            slots = new object[]
            {
                new { dayOfWeek = "Monday", time = "10:00:00", professorId = professor.Id },
                new { dayOfWeek = "Monday", time = "10:00:00", professorId = professor.Id },
                new { dayOfWeek = "Friday", time = "10:00:00", professorId = professor.Id }
            }
        });
        await AssertStatus(HttpStatusCode.BadRequest, duplicate);

        // profesor ne moze da upise drugog profesora, admin moze
        var foreignProfessor = await Post(professor, "/groups", new { courseId = CourseId, slots = Slots(other.Id, 11) });
        await AssertStatus(HttpStatusCode.BadRequest, foreignProfessor);
        await AssertStatus(HttpStatusCode.Created, await Post(admin, "/groups", new { courseId = CourseId, slots = Slots(other.Id, 11) }));
    }

    [IntegrationFact]
    public async Task The_same_professor_cannot_hold_the_same_day_and_time_in_two_groups()
    {
        var professor = TestUser.Professor();
        await CreateGroup(professor, hour: 18);

        var conflict = await Post(professor, "/groups", new { courseId = CourseId, slots = Slots(professor.Id, 18) });

        await AssertStatus(HttpStatusCode.BadRequest, conflict);
        Assert.Equal("CANNOT_CREATE_GROUP", await ErrorCode(conflict));
        // drugo vreme je ok
        await CreateGroup(professor, hour: 19);
    }

    // ---------- clanovi i status Ready ----------

    [IntegrationFact]
    public async Task Group_becomes_Ready_at_three_members_and_enrollments_follow()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);
        var s1 = TestUser.Student();
        var s2 = TestUser.Student();
        var s3 = TestUser.Student();

        var e1 = await AddMember(professor, groupId, s1);
        var e2 = await AddMember(professor, groupId, s2);
        Assert.Equal("Forming", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
        Assert.Equal("Searching", await EnrollmentStatus(s1, e1));

        var e3 = await AddMember(professor, groupId, s3);

        Assert.Equal("Ready", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
        Assert.Equal("Ready", await EnrollmentStatus(s1, e1));
        Assert.Equal("Ready", await EnrollmentStatus(s2, e2));
        Assert.Equal("Ready", await EnrollmentStatus(s3, e3));
    }

    [IntegrationFact]
    public async Task Member_names_come_from_the_user_directory_when_available()
    {
        var professor = TestUser.Professor();
        var student = TestUser.Student();
        Factory.Users.Add(student.Id, "Ana Test");
        var groupId = await CreateGroup(professor);
        await AddMember(professor, groupId, student);
        await AddMember(professor, groupId, TestUser.Student()); // nepoznat u Auth-u

        var members = (await GetGroup(professor, groupId)).GetProperty("members").EnumerateArray().ToList();

        Assert.Contains(members, m => m.GetProperty("fullName").GetString() == "Ana Test");
        Assert.Contains(members, m => m.GetProperty("fullName").ValueKind == System.Text.Json.JsonValueKind.Null);
    }

    [IntegrationFact]
    public async Task Same_student_cannot_be_added_twice_and_group_has_a_maximum()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);
        var student = TestUser.Student();
        await AddMember(professor, groupId, student);

        var duplicate = await Post(professor, $"/groups/{groupId}/members", new { userId = student.Id });
        await AssertStatus(HttpStatusCode.BadRequest, duplicate);
        Assert.Equal("CANNOT_ADD_MEMBER", await ErrorCode(duplicate));

        for (var i = 0; i < 5; i++) await AddMember(professor, groupId, TestUser.Student());

        var full = await Post(professor, $"/groups/{groupId}/members", new { userId = Guid.NewGuid() });
        await AssertStatus(HttpStatusCode.BadRequest, full);
        Assert.Contains("full", await ErrorDetail(full), StringComparison.OrdinalIgnoreCase);
    }

    [IntegrationFact]
    public async Task Unknown_group_returns_404_for_reads_and_writes()
    {
        var professor = TestUser.Professor();

        var get = await Get(professor, $"/groups/{Guid.NewGuid()}");
        await AssertStatus(HttpStatusCode.NotFound, get);
        Assert.Equal("GROUP_NOT_FOUND", await ErrorCode(get));

        await AssertProblem(HttpStatusCode.NotFound, "GROUP_NOT_FOUND",
            await Post(professor, $"/groups/{Guid.NewGuid()}/members", new { userId = Guid.NewGuid() }));
    }

    // ---------- samoprijava i dostupne grupe ----------

    [IntegrationFact]
    public async Task Student_can_self_join_an_open_group_and_it_is_listed_as_available()
    {
        var professor = TestUser.Professor();
        var student = TestUser.Student();
        var groupId = await CreateGroup(professor);

        var available = await Json(await Get(student, $"/groups/available?courseId={CourseId}"));
        Assert.Contains(available.EnumerateArray(), g => g.GetProperty("id").GetGuid() == groupId);

        var join = await Post(student, "/enrollments/group", new { groupId });
        await AssertStatus(HttpStatusCode.Created, join);

        var mine = await MyEnrollments(student);
        Assert.Equal(groupId, mine[0].GetProperty("groupId").GetGuid());
    }

    [IntegrationFact]
    public async Task Full_started_and_other_course_groups_are_not_available()
    {
        var professor = TestUser.Professor();
        var student = TestUser.Student();
        var full = await CreateGroup(professor, hour: 9);
        for (var i = 0; i < 6; i++) await AddMember(professor, full, TestUser.Student());
        var started = await CreateGroup(professor, hour: 10);
        for (var i = 0; i < 3; i++) await AddMember(professor, started, TestUser.Student());
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{started}/start"));
        var open = await CreateGroup(professor, hour: 11);

        var available = (await Json(await Get(student, $"/groups/available?courseId={CourseId}")))
            .EnumerateArray().Select(g => g.GetProperty("id").GetGuid()).ToList();
        var otherCourse = await Json(await Get(student, $"/groups/available?courseId={Guid.NewGuid()}"));

        Assert.Equal(new[] { open }, available);
        Assert.Empty(otherCourse.EnumerateArray());
    }

    [IntegrationFact]
    public async Task Self_join_is_closed_after_the_formation_window_but_staff_can_still_add()
    {
        var professor = TestUser.Professor();
        var student = TestUser.Student();
        var groupId = await CreateGroup(professor);
        Factory.WithDb(db => db.Database.ExecuteSqlRaw(
            "UPDATE groups SET formation_window_ends_at = now() - interval '1 day' WHERE id = {0}", groupId));

        var self = await Post(student, "/enrollments/group", new { groupId });
        await AssertStatus(HttpStatusCode.BadRequest, self);
        Assert.Equal("INVALID_ENROLLMENT", await ErrorCode(self));

        await AddMember(professor, groupId, student);
    }

    [IntegrationFact]
    public async Task Student_cannot_enroll_twice_in_the_same_course()
    {
        var professor = TestUser.Professor();
        var student = TestUser.Student();
        var g1 = await CreateGroup(professor, hour: 9);
        var g2 = await CreateGroup(professor, hour: 10);
        await AssertStatus(HttpStatusCode.Created, await Post(student, "/enrollments/group", new { groupId = g1 }));

        var again = await Post(student, "/enrollments/group", new { groupId = g2 });

        await AssertStatus(HttpStatusCode.BadRequest, again);
        Assert.Contains("already", await ErrorDetail(again));
    }

    // ---------- start i pristup ----------

    [IntegrationFact]
    public async Task Start_needs_the_minimum_unless_forced_and_never_an_empty_group()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);

        var empty = await Post(professor, $"/groups/{groupId}/start", new { force = true });
        await AssertStatus(HttpStatusCode.BadRequest, empty);
        Assert.Equal("CANNOT_START", await ErrorCode(empty));

        var student = TestUser.Student();
        var enrollmentId = await AddMember(professor, groupId, student);

        var belowMinimum = await Post(professor, $"/groups/{groupId}/start");
        await AssertStatus(HttpStatusCode.BadRequest, belowMinimum);
        Assert.Equal("Searching", await EnrollmentStatus(student, enrollmentId));

        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start", new { force = true }));
        Assert.Equal("Active", await EnrollmentStatus(student, enrollmentId));
        Assert.Equal("Active", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
    }

    [IntegrationFact]
    public async Task Start_activates_the_members_gives_access_and_cannot_run_twice()
    {
        var (groupId, professor, students, _) = await ReadyGroup();
        Assert.False((await Access(students[0])).GetProperty("hasAccess").GetBoolean());

        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));

        foreach (var student in students)
        {
            var access = await Access(student);
            Assert.True(access.GetProperty("hasAccess").GetBoolean());
            Assert.Equal(3, access.GetProperty("unlockedLessons").GetInt32()); // prva nedelja
        }

        var again = await Post(professor, $"/groups/{groupId}/start");
        await AssertStatus(HttpStatusCode.BadRequest, again);
        Assert.Contains("already active", await ErrorDetail(again));
    }

    [IntegrationFact]
    public async Task Unlocked_lessons_grow_three_per_week_from_the_unlock_date()
    {
        var (groupId, professor, students, _) = await ReadyGroup();

        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start",
            new { unlockStartsAt = DateTime.UtcNow.AddDays(-15) }));

        Assert.Equal(9, (await Access(students[0])).GetProperty("unlockedLessons").GetInt32());

        // otkljucavanje u buducnosti: pristup postoji, ali jos nista nije otkljucano
        var (futureGroup, p2, s2, _) = await ReadyGroup(hour: 20);
        await AssertStatus(HttpStatusCode.NoContent, await Post(p2, $"/groups/{futureGroup}/start",
            new { unlockStartsAt = DateTime.UtcNow.AddDays(5) }));
        var access = await Access(s2[0]);
        Assert.True(access.GetProperty("hasAccess").GetBoolean());
        Assert.Equal(0, access.GetProperty("unlockedLessons").GetInt32());
    }

    // ---------- otkazivanje ----------

    [IntegrationFact]
    public async Task Cancelling_from_a_Ready_group_returns_the_others_to_Searching_and_the_group_to_Forming()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();

        await AssertStatus(HttpStatusCode.NoContent, await Post(students[2], $"/enrollments/{ids[2]}/cancel"));

        Assert.Equal("Forming", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
        Assert.Equal("Searching", await EnrollmentStatus(students[0], ids[0]));
        Assert.Equal("Searching", await EnrollmentStatus(students[1], ids[1]));
        Assert.Equal("Cancelled", await EnrollmentStatus(students[2], ids[2]));

        // novi clan vraca grupu u Ready i ostali opet postaju Ready
        var newcomer = TestUser.Student();
        var newId = await AddMember(professor, groupId, newcomer);
        Assert.Equal("Ready", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
        Assert.Equal("Ready", await EnrollmentStatus(students[0], ids[0]));
        Assert.Equal("Ready", await EnrollmentStatus(newcomer, newId));
    }

    [IntegrationFact]
    public async Task Students_cannot_cancel_other_peoples_enrollments_but_staff_can()
    {
        var (_, professor, students, ids) = await ReadyGroup();

        var foreign = await Post(students[1], $"/enrollments/{ids[0]}/cancel");
        await AssertStatus(HttpStatusCode.BadRequest, foreign);
        Assert.Equal("CANNOT_CANCEL", await ErrorCode(foreign));

        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/enrollments/{ids[0]}/cancel"));
        Assert.Equal("Cancelled", await EnrollmentStatus(students[0], ids[0]));

        // vec otkazan upis se ne moze ponovo otkazati
        await AssertStatus(HttpStatusCode.BadRequest, await Post(professor, $"/enrollments/{ids[0]}/cancel"));
        await AssertProblem(HttpStatusCode.NotFound, "ENROLLMENT_NOT_FOUND", await Post(professor, $"/enrollments/{Guid.NewGuid()}/cancel"));
    }

    [IntegrationFact]
    public async Task Cancelled_student_can_enroll_again_as_a_new_enrollment()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();
        await AssertStatus(HttpStatusCode.NoContent, await Post(students[0], $"/enrollments/{ids[0]}/cancel"));

        var again = await Post(students[0], "/enrollments/group", new { groupId });

        await AssertStatus(HttpStatusCode.Created, again);
        var mine = await MyEnrollments(students[0]);
        Assert.Equal(2, mine.GetArrayLength());
        Assert.Equal(1, mine.EnumerateArray().Count(e => e.GetProperty("status").GetString() != "Cancelled"));
    }

    [IntegrationFact]
    public async Task Cancelling_an_active_member_keeps_the_group_running()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();
        await Post(professor, $"/groups/{groupId}/start");

        await AssertStatus(HttpStatusCode.NoContent, await Post(students[0], $"/enrollments/{ids[0]}/cancel"));

        Assert.Equal("Active", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
        Assert.False((await Access(students[0])).GetProperty("hasAccess").GetBoolean());
        Assert.True((await Access(students[1])).GetProperty("hasAccess").GetBoolean());
    }

    // ---------- zamrzavanje ----------

    [IntegrationFact]
    public async Task Staff_can_freeze_and_unfreeze_and_access_follows()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();
        await Post(professor, $"/groups/{groupId}/start");

        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/enrollments/{ids[0]}/freeze"));
        Assert.Equal("Frozen", await EnrollmentStatus(students[0], ids[0]));
        Assert.False((await Access(students[0])).GetProperty("hasAccess").GetBoolean());

        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/enrollments/{ids[0]}/unfreeze"));
        Assert.Equal("Active", await EnrollmentStatus(students[0], ids[0]));
        Assert.True((await Access(students[0])).GetProperty("hasAccess").GetBoolean());
    }

    [IntegrationFact]
    public async Task Freeze_and_unfreeze_reject_wrong_states()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();

        // nije Active: ne moze da se zamrzne
        var early = await Post(professor, $"/enrollments/{ids[0]}/freeze");
        await AssertStatus(HttpStatusCode.BadRequest, early);
        Assert.Equal("CANNOT_FREEZE", await ErrorCode(early));

        await Post(professor, $"/groups/{groupId}/start");

        // nije Frozen: ne moze da se odmrzne
        var notFrozen = await Post(professor, $"/enrollments/{ids[0]}/unfreeze");
        await AssertStatus(HttpStatusCode.BadRequest, notFrozen);
        Assert.Equal("CANNOT_UNFREEZE", await ErrorCode(notFrozen));

        // dvaput zamrznut
        await Post(professor, $"/enrollments/{ids[0]}/freeze");
        await AssertStatus(HttpStatusCode.BadRequest, await Post(professor, $"/enrollments/{ids[0]}/freeze"));
        await AssertProblem(HttpStatusCode.NotFound, "ENROLLMENT_NOT_FOUND", await Post(professor, $"/enrollments/{Guid.NewGuid()}/freeze"));
    }

    [IntegrationFact]
    public async Task A_frozen_member_can_be_cancelled()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();
        await Post(professor, $"/groups/{groupId}/start");
        await Post(professor, $"/enrollments/{ids[0]}/freeze");

        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/enrollments/{ids[0]}/cancel"));

        Assert.Equal("Cancelled", await EnrollmentStatus(students[0], ids[0]));
    }

    // ---------- termini ----------

    [IntegrationFact]
    public async Task Assigned_slot_can_be_changed_by_professors_and_admins_and_is_audited()
    {
        var professor = TestUser.Professor();
        var otherProfessor = TestUser.Professor();
        var admin = TestUser.Admin();
        var groupId = await CreateGroup(professor);
        var byProfessor = new { slotIndex = 0, dayOfWeek = "Tuesday", time = "17:00:00", professorId = otherProfessor.Id };
        var byAdmin = new { slotIndex = 1, dayOfWeek = "Thursday", time = "10:00:00", professorId = otherProfessor.Id };

        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/assign-professor-slot", byProfessor));
        await AssertStatus(HttpStatusCode.NoContent, await Post(admin, $"/groups/{groupId}/assign-professor-slot", byAdmin));

        var slots = (await GetGroup(admin, groupId)).GetProperty("slots").EnumerateArray().ToList();
        var slot0 = slots.First(s => s.GetProperty("slotIndex").GetInt32() == 0);
        Assert.Equal(otherProfessor.Id, slot0.GetProperty("professorId").GetGuid());
        Assert.Equal("Tuesday", slot0.GetProperty("dayOfWeek").GetString());
        Assert.Equal("Thursday", slots.First(s => s.GetProperty("slotIndex").GetInt32() == 1).GetProperty("dayOfWeek").GetString());

        var audit = await Json(await Get(admin, $"/audit?entityId={groupId}&actorUserId={professor.Id}"));
        Assert.Contains(audit.EnumerateArray(), a => a.GetProperty("action").GetString() == "AssignSlot");
    }

    [IntegrationFact]
    public async Task Re_assigning_the_same_values_is_allowed_for_professors()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor, hour: 18);

        var same = await Post(professor, $"/groups/{groupId}/assign-professor-slot",
            new { slotIndex = 0, dayOfWeek = "Monday", time = "18:00:00", professorId = professor.Id });

        await AssertStatus(HttpStatusCode.NoContent, same);
    }

    [IntegrationFact]
    public async Task Assigning_a_slot_validates_the_slot_index_and_professor_conflicts()
    {
        var professor = TestUser.Professor();
        var admin = TestUser.Admin();
        var g1 = await CreateGroup(professor, hour: 18);
        var g2 = await CreateGroup(professor, hour: 19);

        var missingSlot = await Post(professor, $"/groups/{g1}/assign-professor-slot",
            new { slotIndex = 7, dayOfWeek = "Monday", time = "18:00:00", professorId = professor.Id });
        await AssertStatus(HttpStatusCode.BadRequest, missingSlot);

        // admin pomera termin grupe 2 na vreme koje profesor vec drzi u grupi 1
        var conflict = await Post(admin, $"/groups/{g2}/assign-professor-slot",
            new { slotIndex = 0, dayOfWeek = "Monday", time = "18:00:00", professorId = professor.Id });
        await AssertStatus(HttpStatusCode.BadRequest, conflict);
        Assert.Equal("CANNOT_ASSIGN", await ErrorCode(conflict));
    }

    // ---------- zatvaranje ----------

    [IntegrationFact]
    public async Task Admin_completes_an_active_group_and_it_cannot_be_completed_twice()
    {
        var (groupId, professor, _, _) = await ReadyGroup();
        var admin = TestUser.Admin();
        await Post(professor, $"/groups/{groupId}/start");

        await AssertStatus(HttpStatusCode.NoContent, await Post(admin, $"/groups/{groupId}/complete"));

        Assert.Equal("Completed", (await GetGroup(admin, groupId)).GetProperty("status").GetString());
        await AssertStatus(HttpStatusCode.BadRequest, await Post(admin, $"/groups/{groupId}/complete"));
        // zatvorena grupa ne prima nove clanove niti termine
        await AssertStatus(HttpStatusCode.BadRequest,
            await Post(admin, $"/groups/{groupId}/members", new { userId = Guid.NewGuid() }));
    }

    [IntegrationFact]
    public async Task Listing_filters_groups_by_status_type_and_course()
    {
        var professor = TestUser.Professor();
        var admin = TestUser.Admin();
        var forming = await CreateGroup(professor, hour: 9);
        var (ready, _, _, _) = await ReadyGroup(hour: 10);

        var all = await Json(await Get(admin, "/groups"));
        var onlyReady = await Json(await Get(admin, "/groups?status=Ready"));
        var onlyForming = await Json(await Get(admin, "/groups?status=Forming"));
        var wrongCourse = await Json(await Get(admin, $"/groups?courseId={Guid.NewGuid()}"));
        var individuals = await Json(await Get(admin, "/groups?type=Individual"));

        Assert.Equal(2, all.GetArrayLength());
        Assert.Equal(new[] { ready }, onlyReady.EnumerateArray().Select(g => g.GetProperty("id").GetGuid()));
        Assert.Equal(new[] { forming }, onlyForming.EnumerateArray().Select(g => g.GetProperty("id").GetGuid()));
        Assert.Empty(wrongCourse.EnumerateArray());
        Assert.Empty(individuals.EnumerateArray());
    }

    // ---------- audit ----------

    [IntegrationFact]
    public async Task Sensitive_actions_are_written_to_the_audit_log()
    {
        var professor = TestUser.Professor();
        var admin = TestUser.Admin();
        var groupId = await CreateGroup(professor);
        var student = TestUser.Student();
        var enrollmentId = await AddMember(professor, groupId, student);
        await AddMember(professor, groupId, TestUser.Student()); // grupa ostaje Active i kad prvi otkaze
        await Post(professor, $"/groups/{groupId}/start", new { force = true });
        await Post(professor, $"/enrollments/{enrollmentId}/freeze");
        await Post(professor, $"/enrollments/{enrollmentId}/unfreeze");
        await Post(professor, $"/enrollments/{enrollmentId}/cancel");
        await Post(admin, $"/groups/{groupId}/complete");

        var forGroup = await Json(await Get(admin, $"/audit?entityId={groupId}"));
        var forEnrollment = await Json(await Get(admin, $"/audit?entityId={enrollmentId}"));
        var byProfessor = await Json(await Get(admin, $"/audit?actorUserId={professor.Id}"));

        var groupActions = forGroup.EnumerateArray().Select(a => a.GetProperty("action").GetString()).ToList();
        Assert.Contains("CreateGroup", groupActions);
        Assert.Contains("AddMember", groupActions);
        Assert.Contains("StartGroup", groupActions);
        Assert.Contains("CompleteGroup", groupActions);

        var enrollmentActions = forEnrollment.EnumerateArray().Select(a => a.GetProperty("action").GetString()).ToList();
        Assert.Contains("FreezeEnrollment", enrollmentActions);
        Assert.Contains("UnfreezeEnrollment", enrollmentActions);
        Assert.Contains("CancelEnrollment", enrollmentActions);

        Assert.All(byProfessor.EnumerateArray(), a => Assert.Equal(professor.Id, a.GetProperty("actorUserId").GetGuid()));
        Assert.DoesNotContain(byProfessor.EnumerateArray(), a => a.GetProperty("action").GetString() == "CompleteGroup");

        var limited = await Json(await Get(admin, "/audit?take=2"));
        Assert.Equal(2, limited.GetArrayLength());
    }
}