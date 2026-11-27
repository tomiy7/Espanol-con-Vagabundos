using System.Net;
using System.Text.Json;
using CoursesService.Tests.Infrastructure;

namespace CoursesService.Tests.Api;

// GET /courses/lessons/{id}: sadrzaj samo za osoblje i upisane, po broju otkljucanih lekcija.
[Collection(CoursesCollection.Name)]
public class LessonAccessTests(CoursesFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Kurs sa 3 lekcije; order namerno pocinje od 0 i ima rupe (kao u Directus-u).
    private async Task<(Guid CourseId, List<Guid> Lessons)> CourseWithLessons(bool published = true)
    {
        var course = Seed.Course(published);
        var lessons = new[] { Seed.Lesson(course, 0), Seed.Lesson(course, 4), Seed.Lesson(course, 9) };
        await factory.SeedAsync([course, .. lessons]);
        return (course.Id, lessons.Select(l => l.Id).ToList());
    }

    private Task<HttpResponseMessage> GetLesson(TestUser user, Guid lessonId) =>
        user.Client(factory).GetAsync($"/courses/lessons/{lessonId}");

    private static async Task AssertProblem(HttpStatusCode status, string code, HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(status == response.StatusCode, $"Expected {status} but got {response.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(code, doc.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("video.test", body);
    }

    [IntegrationFact]
    public async Task Anonymous_users_get_401_with_a_code()
    {
        var (_, lessons) = await CourseWithLessons();

        var response = await factory.CreateClient().GetAsync($"/courses/lessons/{lessons[0]}");

        await AssertProblem(HttpStatusCode.Unauthorized, "UNAUTHORIZED", response);
    }

    [IntegrationFact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var (_, lessons) = await CourseWithLessons();
        var student = TestUser.Student();
        var forged = student.Token("some-other-secret-some-other-secret-some-other");

        var response = await student.Client(factory, forged).GetAsync($"/courses/lessons/{lessons[0]}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, factory.Groups.Calls);
    }

    [IntegrationFact]
    public async Task Student_without_enrollment_is_forbidden()
    {
        var (_, lessons) = await CourseWithLessons();

        await AssertProblem(HttpStatusCode.Forbidden, "NO_COURSE_ACCESS", await GetLesson(TestUser.Student(), lessons[0]));
    }

    [IntegrationFact]
    public async Task Student_reads_unlocked_lessons_and_not_the_next_one()
    {
        var (courseId, lessons) = await CourseWithLessons();
        factory.Groups.Grant(courseId, unlockedLessons: 2);
        var student = TestUser.Student();

        var second = await GetLesson(student, lessons[1]);
        var third = await GetLesson(student, lessons[2]);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        await AssertProblem(HttpStatusCode.Forbidden, "LESSON_LOCKED", third);
        Assert.Equal(3, factory.Groups.LastToken!.Split('.').Length); // pita se korisnikovim JWT-om
    }

    [IntegrationFact]
    public async Task Access_to_another_course_does_not_open_this_one()
    {
        var (_, lessons) = await CourseWithLessons();
        factory.Groups.Grant(Guid.NewGuid(), unlockedLessons: 99);

        await AssertProblem(HttpStatusCode.Forbidden, "NO_COURSE_ACCESS", await GetLesson(TestUser.Student(), lessons[0]));
    }

    [IntegrationFact]
    public async Task Professor_and_admin_read_any_lesson_without_asking_groups()
    {
        var (_, lessons) = await CourseWithLessons(published: false);

        var professor = await GetLesson(TestUser.Professor(), lessons[2]);
        var admin = await GetLesson(TestUser.Admin(), lessons[2]);

        Assert.Equal(HttpStatusCode.OK, professor.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        Assert.Equal(0, factory.Groups.Calls);
    }

    [IntegrationFact]
    public async Task Lesson_of_unpublished_course_is_not_found_for_students()
    {
        var (courseId, lessons) = await CourseWithLessons(published: false);
        factory.Groups.Grant(courseId, unlockedLessons: 3);

        await AssertProblem(HttpStatusCode.NotFound, "LESSON_NOT_FOUND", await GetLesson(TestUser.Student(), lessons[0]));
    }

    [IntegrationFact]
    public async Task Hidden_unpublished_or_unknown_lessons_are_not_found()
    {
        var course = Seed.Course();
        var hidden = Seed.Lesson(course, 1, visible: false);
        var draft = Seed.Lesson(course, 2, published: false);
        await factory.SeedAsync(course, hidden, draft);
        var admin = TestUser.Admin();

        await AssertProblem(HttpStatusCode.NotFound, "LESSON_NOT_FOUND", await GetLesson(admin, hidden.Id));
        await AssertProblem(HttpStatusCode.NotFound, "LESSON_NOT_FOUND", await GetLesson(admin, draft.Id));
        await AssertProblem(HttpStatusCode.NotFound, "LESSON_NOT_FOUND", await GetLesson(admin, Guid.NewGuid()));
    }

    [IntegrationFact]
    public async Task Hidden_lessons_do_not_count_for_unlocking()
    {
        var course = Seed.Course();
        var first = Seed.Lesson(course, 1);
        var hidden = Seed.Lesson(course, 2, visible: false);
        var third = Seed.Lesson(course, 3);
        await factory.SeedAsync(course, first, hidden, third);
        factory.Groups.Grant(course.Id, unlockedLessons: 2);

        var response = await GetLesson(TestUser.Student(), third.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [IntegrationFact]
    public async Task When_groups_is_down_no_content_is_returned()
    {
        var (_, lessons) = await CourseWithLessons();
        factory.Groups.Unavailable = true;

        await AssertProblem(HttpStatusCode.ServiceUnavailable, "ACCESS_CHECK_UNAVAILABLE",
            await GetLesson(TestUser.Student(), lessons[0]));
    }

    [IntegrationFact]
    public async Task When_groups_rejects_the_token_the_student_gets_401()
    {
        var (_, lessons) = await CourseWithLessons();
        factory.Groups.RejectsToken = true;

        await AssertProblem(HttpStatusCode.Unauthorized, "UNAUTHORIZED", await GetLesson(TestUser.Student(), lessons[0]));
    }

    [IntegrationFact]
    public async Task Lesson_has_course_id_sorted_sections_and_json_content()
    {
        var course = Seed.Course();
        var lesson = Seed.Lesson(course, 1);
        var later = Seed.Section(lesson, 2, "\"samo tekst\"");
        var first = Seed.Section(lesson, 1, """{"blocks":[{"type":"paragraph","data":{"text":"Hola"}}]}""");
        await factory.SeedAsync(course, lesson, later, first);

        var response = await GetLesson(TestUser.Admin(), lesson.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(course.Id, root.GetProperty("courseId").GetGuid());
        var sections = root.GetProperty("sections").EnumerateArray().ToList();
        Assert.Equal(new[] { first.Id, later.Id }, sections.Select(s => s.GetProperty("id").GetGuid()));
        Assert.Equal("Hola", sections[0].GetProperty("content").GetProperty("blocks")[0]
            .GetProperty("data").GetProperty("text").GetString());
        Assert.Equal("samo tekst", sections[1].GetProperty("content").GetString());
    }
}
