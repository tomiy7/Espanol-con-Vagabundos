using CoursesService.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoursesService.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) 
        : base(options) { }
    
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lesson>()
            .HasOne(l => l.Course)
            .WithMany(c => c.Lessons)
            .HasForeignKey(l => l.CourseId);

        modelBuilder.Entity<Section>()
            .HasOne(s => s.Lesson)
            .WithMany(l => l.Sections)
            .HasForeignKey(s => s.LessonId);

        modelBuilder.Entity<TaskItem>()
            .HasOne(t => t.Lesson)
            .WithMany(l => l.Tasks)
            .HasForeignKey(t => t.LessonId);
    }
}