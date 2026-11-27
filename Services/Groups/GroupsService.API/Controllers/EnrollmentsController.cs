using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GroupsService.API.Constants;
using GroupsService.API.DTOs;
using GroupsService.API.Errors;
using GroupsService.Application.Commands.CancelEnrollment;
using GroupsService.Application.Commands.ChangePaymentPlan;
using GroupsService.Application.Commands.CreateIndividualEnrollment;
using GroupsService.Application.Commands.FreezeEnrollment;
using GroupsService.Application.Commands.InvitePartner;
using GroupsService.Application.Commands.JoinGroup;
using GroupsService.Application.Interfaces;
using GroupsService.Application.Queries.CheckAccess;
using GroupsService.Application.Queries.GetEnrollmentById;
using GroupsService.Application.Queries.GetMyEnrollments;

namespace GroupsService.API.Controllers;

// Upisi ucenika. Kontroler samo prevodi HTTP <-> komande/upite; pravila su u
// Application/Domain, a odbijene operacije pretvara u odgovor GroupsExceptionHandler.
[Route("enrollments")]
public class EnrollmentsController : ApiControllerBase
{
    private readonly CreateIndividualEnrollmentHandler _createIndividual;
    private readonly JoinGroupHandler _joinGroup;
    private readonly InvitePartnerHandler _invitePartner;
    private readonly CancelEnrollmentHandler _cancel;
    private readonly FreezeEnrollmentHandler _freeze;
    private readonly UnfreezeEnrollmentHandler _unfreeze;
    private readonly GetEnrollmentByIdHandler _getById;
    private readonly GetMyEnrollmentsHandler _getMine;
    private readonly CheckAccessHandler _checkAccess;
    private readonly ChangePaymentPlanHandler _changePaymentPlan;
    private readonly IAuditLog _audit;

    public EnrollmentsController(
        CreateIndividualEnrollmentHandler createIndividual,
        JoinGroupHandler joinGroup,
        InvitePartnerHandler invitePartner,
        CancelEnrollmentHandler cancel,
        FreezeEnrollmentHandler freeze,
        UnfreezeEnrollmentHandler unfreeze,
        GetEnrollmentByIdHandler getById,
        GetMyEnrollmentsHandler getMine,
        CheckAccessHandler checkAccess,
        ChangePaymentPlanHandler changePaymentPlan,
        IAuditLog audit)
    {
        _createIndividual = createIndividual;
        _joinGroup = joinGroup;
        _invitePartner = invitePartner;
        _cancel = cancel;
        _freeze = freeze;
        _unfreeze = unfreeze;
        _getById = getById;
        _getMine = getMine;
        _checkAccess = checkAccess;
        _changePaymentPlan = changePaymentPlan;
        _audit = audit;
    }

    [HttpPost("individual")]
    [ErrorCode(ErrorCodes.InvalidEnrollment)]
    [ProducesResponseType<EnrollmentCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateIndividual(CreateIndividualEnrollmentRequest request)
    {
        var result = await _createIndividual.Handle(
            new CreateIndividualEnrollmentCommand(CurrentUserId, request.CourseId, request.ProfessorId, request.PaymentPlan));
        return CreatedEnrollment(new EnrollmentCreatedResponse(result.EnrollmentId, result.GroupId));
    }

    // Ucenik sam bira grupu (termin). Za sada ucenike uglavnom ubacuje osoblje (POST /groups/{id}/members).
    [HttpPost("group")]
    [ErrorCode(ErrorCodes.InvalidEnrollment)]
    [ProducesResponseType<EnrollmentCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> JoinGroup(JoinGroupRequest request)
    {
        var result = await _joinGroup.Handle(new JoinGroupCommand(CurrentUserId, request.GroupId, PaymentPlan: request.PaymentPlan));
        return CreatedEnrollment(new EnrollmentCreatedResponse(result.EnrollmentId, result.GroupId));
    }

    // Upis "u paru" - inicira se ovde, a par postaje stvaran tek kad pozvani prihvati
    // (POST /invitations/{token}/accept).
    [HttpPost("invite-partner")]
    [ErrorCode(ErrorCodes.InvalidInvitation)]
    [ProducesResponseType<InvitationCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InvitePartner(InvitePartnerRequest request)
    {
        var result = await _invitePartner.Handle(
            new InvitePartnerCommand(CurrentUserId, request.InviteeUserId, request.CourseId, request.PaymentPlan));
        return CreatedAtAction(nameof(GetById), new { enrollmentId = result.EnrollmentId },
            new InvitationCreatedResponse(result.EnrollmentId, result.InvitationId));
    }

    // Svi upisi trenutnog korisnika (ukljucujuci zavrsene i otkazane, za prikaz po sekcijama).
    [HttpGet("mine")]
    [ProducesResponseType<List<MyEnrollmentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine() =>
        Ok(await _getMine.Handle(new GetMyEnrollmentsQuery(CurrentUserId)));

    // Ucenik vidi samo svoj upis, osoblje bilo koji. Tudji upis vraca isto 404 kao
    // nepostojeci, da se ne otkriva koji Id-jevi postoje.
    [HttpGet("{enrollmentId:guid}")]
    [ProducesResponseType<EnrollmentDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid enrollmentId)
    {
        var enrollment = await _getById.Handle(new GetEnrollmentByIdQuery(enrollmentId));
        if (enrollment == null || (enrollment.UserId != CurrentUserId && !IsStaff))
            return ProblemWithCode(StatusCodes.Status404NotFound, ErrorCodes.EnrollmentNotFound, "Enrollment does not exist.");

        return Ok(enrollment);
    }

    // Poziva ga Courses (i front) pre otvaranja lekcije.
    [HttpGet("access")]
    [ProducesResponseType<CheckAccessResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckAccess([FromQuery] Guid courseId)
    {
        if (courseId == Guid.Empty)
            return ProblemWithCode(StatusCodes.Status400BadRequest, ErrorCodes.InvalidCourseId, "courseId is required.");

        return Ok(await _checkAccess.Handle(new CheckAccessQuery(CurrentUserId, courseId)));
    }

    // Ucenik otkazuje svoj upis; osoblje moze i tudji (izbacivanje iz grupe).
    [HttpPost("{enrollmentId:guid}/cancel")]
    [ErrorCode(ErrorCodes.CannotCancel)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid enrollmentId)
    {
        await _cancel.Handle(new CancelEnrollmentCommand(enrollmentId, CurrentUserId, IsStaff));
        await _audit.RecordAsync(CurrentUserId, "CancelEnrollment", "Enrollment", enrollmentId, $"byStaff={IsStaff}");
        return NoContent();
    }

    // Izbor placanja (odjednom / rate) dok grupa nije potvrdjena; posle toga se menja u Payments-u.
    [HttpPut("{enrollmentId:guid}/payment-plan")]
    [ErrorCode(ErrorCodes.CannotChangePaymentPlan)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePaymentPlan(
        Guid enrollmentId, ChangePaymentPlanRequest request, CancellationToken cancellationToken)
    {
        await _changePaymentPlan.Handle(
            new ChangePaymentPlanCommand(enrollmentId, CurrentUserId, IsStaff, request.PaymentPlan), cancellationToken);
        await _audit.RecordAsync(CurrentUserId, "ChangePaymentPlan", "Enrollment", enrollmentId,
            $"plan={request.PaymentPlan}, byStaff={IsStaff}");
        return NoContent();
    }

    // Zamrzavanje odobrava osoblje (sama rola je odobrenje, nema posebnog zapisa).
    [HttpPost("{enrollmentId:guid}/freeze")]
    [Authorize(Roles = Roles.Staff)]
    [ErrorCode(ErrorCodes.CannotFreeze)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Freeze(Guid enrollmentId)
    {
        await _freeze.Handle(new FreezeEnrollmentCommand(enrollmentId, CurrentUserId));
        await _audit.RecordAsync(CurrentUserId, "FreezeEnrollment", "Enrollment", enrollmentId);
        return NoContent();
    }

    [HttpPost("{enrollmentId:guid}/unfreeze")]
    [Authorize(Roles = Roles.Staff)]
    [ErrorCode(ErrorCodes.CannotUnfreeze)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unfreeze(Guid enrollmentId)
    {
        await _unfreeze.Handle(new UnfreezeEnrollmentCommand(enrollmentId));
        await _audit.RecordAsync(CurrentUserId, "UnfreezeEnrollment", "Enrollment", enrollmentId);
        return NoContent();
    }

    private CreatedAtActionResult CreatedEnrollment(EnrollmentCreatedResponse response) =>
        CreatedAtAction(nameof(GetById), new { enrollmentId = response.EnrollmentId }, response);
}
