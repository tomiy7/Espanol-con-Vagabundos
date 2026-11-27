namespace CoursesService.API.Errors;

// Stabilni kodovi gresaka koje front mapira na svoje poruke (isti oblik kao u Groups).
// Tekst ("detail") je za developera i moze da se menja; kod se ne menja bez dogovora sa frontom.
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Internal = "INTERNAL_ERROR";

    public const string CourseNotFound = "COURSE_NOT_FOUND";
    public const string LessonNotFound = "LESSON_NOT_FOUND";
    public const string NoCourseAccess = "NO_COURSE_ACCESS";
    public const string LessonLocked = "LESSON_LOCKED";
    public const string AccessCheckUnavailable = "ACCESS_CHECK_UNAVAILABLE";

    // Kod za odgovore koje pravi sam ASP.NET (401/403 iz autorizacije, 400 iz validacije...).
    public static string ForStatus(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => ValidationFailed,
        StatusCodes.Status401Unauthorized => Unauthorized,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        _ => Internal
    };
}
