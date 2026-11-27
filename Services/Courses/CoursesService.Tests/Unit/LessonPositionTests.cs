using CoursesService.API.Access;

namespace CoursesService.Tests.Unit;

public class LessonPositionTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-00000000000c");

    [Fact]
    public void Position_is_one_based_even_when_order_starts_at_zero()
    {
        var lessons = new[] { (B, 1), (A, 0), (C, 2) };

        Assert.Equal(1, LessonPosition.Of(lessons, A));
        Assert.Equal(2, LessonPosition.Of(lessons, B));
        Assert.Equal(3, LessonPosition.Of(lessons, C));
    }

    [Fact]
    public void Gaps_in_order_do_not_skip_positions()
    {
        var lessons = new[] { (A, 10), (B, 50), (C, 51) };

        Assert.Equal(2, LessonPosition.Of(lessons, B));
        Assert.Equal(3, LessonPosition.Of(lessons, C));
    }

    [Fact]
    public void Duplicate_order_is_resolved_by_id_so_it_is_stable()
    {
        var lessons = new[] { (C, 1), (A, 1), (B, 1) };

        Assert.Equal(1, LessonPosition.Of(lessons, A));
        Assert.Equal(3, LessonPosition.Of(lessons, C));
    }

    [Fact]
    public void Unknown_lesson_throws()
    {
        Assert.Throws<ArgumentException>(() => LessonPosition.Of([(A, 1)], B));
    }
}
