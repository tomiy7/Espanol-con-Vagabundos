namespace GroupsService.API.Errors;

// Stabilni kodovi gresaka koje front mapira na svoje poruke. Tekst ("detail") je samo
// pomoc za developera i moze da se menja; kod se ne menja bez dogovora sa frontom.
public static class ErrorCodes
{
    // opsti (dodeljuju se po HTTP statusu kad akcija nema svoj kod)
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string BusinessRule = "BUSINESS_RULE_VIOLATION";
    public const string Conflict = "CONFLICT";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Internal = "INTERNAL_ERROR";

    // upisi
    public const string InvalidEnrollment = "INVALID_ENROLLMENT";
    public const string InvalidInvitation = "INVALID_INVITATION";
    public const string InvalidCourseId = "INVALID_COURSE_ID";
    public const string CannotCancel = "CANNOT_CANCEL";
    public const string CannotFreeze = "CANNOT_FREEZE";
    public const string CannotUnfreeze = "CANNOT_UNFREEZE";
    public const string CannotChangePaymentPlan = "CANNOT_CHANGE_PAYMENT_PLAN";

    // grupe
    public const string CannotCreateGroup = "CANNOT_CREATE_GROUP";
    public const string CannotAddMember = "CANNOT_ADD_MEMBER";
    public const string CannotAssign = "CANNOT_ASSIGN";
    public const string CannotStart = "CANNOT_START";
    public const string CannotComplete = "CANNOT_COMPLETE";
    public const string CannotSchedule = "CANNOT_SCHEDULE";

    // pozivnice
    public const string CannotAcceptInvitation = "CANNOT_ACCEPT_INVITATION";
    public const string CannotDeclineInvitation = "CANNOT_DECLINE_INVITATION";

    // nepostojeci resursi (isti oblik kao EntityNotFoundException.Code)
    public const string EnrollmentNotFound = "ENROLLMENT_NOT_FOUND";
    public const string GroupNotFound = "GROUP_NOT_FOUND";
    public const string InvitationNotFound = "INVITATION_NOT_FOUND";

    public static string ForStatus(int? status) => status switch
    {
        400 => ValidationFailed,
        401 => Unauthorized,
        403 => Forbidden,
        404 => NotFound,
        409 => Conflict,
        _ => Internal
    };
}
