using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CoursesService.API.Entities;

namespace CoursesService.API.Repositories;

public interface ICourseRepository
{
    Task<List<Course>> GetCoursesAsync();
    Task<List<Course>> GetPublishedCoursesAsync();
    Task<Course?> GetBySlugAsync(string slug);
    Task<Course?> GetByIdWithLessonsAsync(Guid id);
    Task<Lesson?> GetLessonByIdAsync(Guid id);
}