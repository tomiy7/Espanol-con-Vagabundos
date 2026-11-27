namespace CoursesService.API.Access;

// Pozicija lekcije u kursu (1, 2, 3...) za poredjenje sa brojem otkljucanih lekcija iz Groups-a.
// Directus "order" moze da pocne od 0, da ima rupe ili duplikate, pa se ne poredi direktno:
// lekcije se sortiraju po order, pa po id (stabilno), i broji se mesto.
public static class LessonPosition
{
    public static int Of(IEnumerable<(Guid Id, int Order)> lessons, Guid lessonId)
    {
        var position = 0;
        foreach (var lesson in lessons.OrderBy(l => l.Order).ThenBy(l => l.Id))
        {
            position++;
            if (lesson.Id == lessonId)
                return position;
        }

        throw new ArgumentException("Lesson is not part of the given lessons.", nameof(lessonId));
    }
}
