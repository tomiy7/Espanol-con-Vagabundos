using CoursesService.API.Data;
using CoursesService.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoursesService.API.Repositories;

// Samo citanje (podatke menja osoblje kroz Directus), pa svuda AsNoTracking.
public class CourseRepository : ICourseRepository
{
    private readonly AppDbContext _context;

    public CourseRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<Course>> GetPublishedCoursesAsync(CancellationToken cancellationToken) =>
        _context.Courses.AsNoTracking().Where(c => c.IsPublished).ToListAsync(cancellationToken);

    public Task<Course?> GetPublishedBySlugWithLessonsAsync(string slug, CancellationToken cancellationToken) =>
        _context.Courses.AsNoTracking()
            .Include(c => c.Lessons.Where(l => l.IsPublished && l.IsVisible))
            .FirstOrDefaultAsync(c => c.Slug == slug && c.IsPublished, cancellationToken);

    public Task<Course?> GetPublishedByIdWithLessonsAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Courses.AsNoTracking()
            .Include(c => c.Lessons.Where(l => l.IsPublished && l.IsVisible))
            .FirstOrDefaultAsync(c => c.Id == id && c.IsPublished, cancellationToken);

    public Task<Course?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Lesson?> GetLessonAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Lessons.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.Sections)
            .Include(l => l.Tasks)
            .FirstOrDefaultAsync(l => l.Id == id && l.IsPublished && l.IsVisible, cancellationToken);

    public async Task<List<(Guid Id, int Order)>> GetLessonOrderAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var rows = await _context.Lessons.AsNoTracking()
            .Where(l => l.CourseId == courseId && l.IsPublished && l.IsVisible)
            .Select(l => new { l.Id, l.Order })
            .ToListAsync(cancellationToken);
        return rows.Select(r => (r.Id, r.Order)).ToList();
    }
}
