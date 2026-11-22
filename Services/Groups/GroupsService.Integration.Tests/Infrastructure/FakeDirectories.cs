using GroupsService.Application.Interfaces;

namespace GroupsService.Integration.Tests.Infrastructure;

// Auth i Courses nisu deo ovih testova - zamenjeni su lazima kojima test zadaje podatke.
public class FakeUserDirectory : IUserDirectory
{
    private readonly Dictionary<Guid, UserContact> _users = new();

    public void Add(Guid userId, string name) => _users[userId] = new UserContact(userId, $"{name}@test.local", name);
    public void Clear() => _users.Clear();

    public Task<UserContact?> GetUserContactAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_users.TryGetValue(userId, out var u) ? u : null);
}

public class FakeCourseDirectory : ICourseDirectory
{
    private readonly Dictionary<Guid, string> _courses = new();

    public void Add(Guid courseId, string name) => _courses[courseId] = name;
    public void Clear() => _courses.Clear();

    public Task<string?> GetCourseNameAsync(Guid courseId, CancellationToken ct = default) =>
        Task.FromResult(_courses.TryGetValue(courseId, out var n) ? n : null);
}
