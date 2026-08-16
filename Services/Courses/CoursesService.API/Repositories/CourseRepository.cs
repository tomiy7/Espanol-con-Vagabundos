using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoursesService.API.Data;
using CoursesService.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoursesService.API.Repositories;

public class CourseRepository : ICourseRepository
{
    private readonly AppDbContext _context;
    
    public CourseRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Course>> GetCoursesAsync() =>
        await _context.Courses.ToListAsync();

    public async Task<List<Course>> GetPublishedCoursesAsync() =>
        await _context.Courses.Where(c => c.IsPublished).ToListAsync();

    public async Task<Course?> GetBySlugAsync(string slug) =>
        await _context.Courses.FirstOrDefaultAsync(c => c.Slug == slug && c.IsPublished);

    public async Task<Course?> GetByIdWithLessonsAsync(Guid id) =>
        await _context.Courses
            .Include(c => c.Lessons.Where(l => l.IsPublished && l.IsVisible))
            .FirstOrDefaultAsync(c => c.Id == id && c.IsPublished);

    public async Task<Lesson?> GetLessonByIdAsync(Guid id) =>
        await _context.Lessons
            .Include(l => l.Sections.OrderBy(s => s.Order))
            .Include(l => l.Tasks.OrderBy(t => t.Order))
            .FirstOrDefaultAsync(l => l.Id == id && l.IsPublished && l.IsVisible);
}