using GroupsService.Domain.Exceptions;

namespace GroupsService.Domain.ValueObjects;

// Jedna zavrsena lekcija u okviru jednog Enrollment-a. Obicna klasa bez Id-a:
// identitet je par (enrollment, lekcija), pa je to i primarni kljuc u bazi i
// garancija da se lekcija u jednom pohadjanju ne moze upisati dvaput.
public class CompletedLesson
{
    public Guid LessonId { get; private set; }
    public DateTime CompletedAt { get; private set; }

    private CompletedLesson() { }

    public CompletedLesson(Guid lessonId, DateTime completedAt)
    {
        if (lessonId == Guid.Empty)
            throw new GroupsDomainException("Lesson ID cannot be empty.");
        
        LessonId = lessonId;
        CompletedAt = completedAt;
    }
}