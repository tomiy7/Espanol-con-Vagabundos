using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GroupsService.API.Constants;
using GroupsService.API.DTOs;
using GroupsService.API.Errors;
using GroupsService.Application.Commands.AssignProfessorToSlot;
using GroupsService.Application.Commands.CompleteGroup;
using GroupsService.Application.Commands.CreateGroup;
using GroupsService.Application.Commands.JoinGroup;
using GroupsService.Application.Commands.StartGroup;
using GroupsService.Application.Interfaces;
using GroupsService.Application.Queries.GetAvailableGroups;
using GroupsService.Application.Queries.GetGroupById;
using GroupsService.Application.Queries.GetGroups;
using GroupsService.Domain.Enums;

namespace GroupsService.API.Controllers;

// Grupe i termini. Sve osim liste dostupnih grupa je za osoblje (profesor ili admin);
// zatvaranje grupe i izmena vec dodeljenog termina su samo za admina.
[Route("groups")]
public class GroupsController : ApiControllerBase
{
    private readonly GetAvailableGroupsHandler _getAvailable;
    private readonly GetGroupsHandler _getGroups;
    private readonly GetGroupByIdHandler _getGroupById;
    private readonly CreateGroupHandler _createGroup;
    private readonly JoinGroupHandler _joinGroup;
    private readonly AssignProfessorToSlotHandler _assignProfessor;
    private readonly StartGroupHandler _startGroup;
    private readonly CompleteGroupHandler _completeGroup;
    private readonly IAuditLog _audit;

    public GroupsController(
        GetAvailableGroupsHandler getAvailable,
        GetGroupsHandler getGroups,
        GetGroupByIdHandler getGroupById,
        CreateGroupHandler createGroup,
        JoinGroupHandler joinGroup,
        AssignProfessorToSlotHandler assignProfessor,
        StartGroupHandler startGroup,
        CompleteGroupHandler completeGroup,
        IAuditLog audit)
    {
        _getAvailable = getAvailable;
        _getGroups = getGroups;
        _getGroupById = getGroupById;
        _createGroup = createGroup;
        _joinGroup = joinGroup;
        _assignProfessor = assignProfessor;
        _startGroup = startGroup;
        _completeGroup = completeGroup;
        _audit = audit;
    }

    // Grupe u koje ucenik moze da udje (otvorene, nisu pune, rok nije istekao).
    // Jedina ruta ovde dostupna i ucenicima.
    [HttpGet("available")]
    [ProducesResponseType<List<GroupSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAvailable([FromQuery] Guid courseId)
    {
        if (courseId == Guid.Empty)
            return ProblemWithCode(StatusCodes.Status400BadRequest, ErrorCodes.InvalidCourseId, "courseId is required.");

        return Ok(await _getAvailable.Handle(new GetAvailableGroupsQuery(courseId)));
    }

    // Lista grupa za osoblje; svi filteri su opcioni. professorId vraca grupe u kojima
    // profesor drzi termin, a type=Individual&status=Ready su zahtevi koji cekaju potvrdu termina.
    [HttpGet]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<List<GroupSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroups(
        [FromQuery] Guid? courseId, [FromQuery] GroupStatus? status,
        [FromQuery] GroupType? type, [FromQuery] Guid? professorId) =>
        Ok(await _getGroups.Handle(new GetGroupsQuery(courseId, status, type, professorId)));

    [HttpGet("{groupId:guid}")]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<GroupDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGroup(Guid groupId)
    {
        var group = await _getGroupById.Handle(new GetGroupByIdQuery(groupId));
        return group == null
            ? ProblemWithCode(StatusCodes.Status404NotFound, ErrorCodes.GroupNotFound, "Group does not exist.")
            : Ok(group);
    }

    // Osoblje otvara grupu sa gotovim rasporedom (tacno 3 termina, npr. pon/sre/pet).
    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
    [ErrorCode(ErrorCodes.CannotCreateGroup)]
    [ProducesResponseType<GroupCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateGroup(CreateGroupRequest request)
    {
        var slots = request.Slots.Select(s => new SlotInput(s.DayOfWeek, s.Time, s.ProfessorId)).ToList();
        var result = await _createGroup.Handle(new CreateGroupCommand(
            CurrentUserId, IsAdmin, request.CourseId, slots, request.FormationWindowDays));

        await _audit.RecordAsync(CurrentUserId, "CreateGroup", "Group", result.GroupId, $"course={request.CourseId}");
        return CreatedAtAction(nameof(GetGroup), new { groupId = result.GroupId }, new GroupCreatedResponse(result.GroupId));
    }

    // Osoblje ubacuje ucenika u grupu (npr. po odgovorima iz Google forme). Izbacivanje ide
    // kroz POST /enrollments/{id}/cancel, a premestanje je izbacivanje + dodavanje.
    [HttpPost("{groupId:guid}/members")]
    [Authorize(Roles = Roles.Staff)]
    [ErrorCode(ErrorCodes.CannotAddMember)]
    [ProducesResponseType<EnrollmentCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMember(Guid groupId, AddMemberRequest request)
    {
        var result = await _joinGroup.Handle(new JoinGroupCommand(request.UserId, groupId, AddedByStaff: true));

        await _audit.RecordAsync(CurrentUserId, "AddMember", "Group", groupId,
            $"user={request.UserId}, enrollment={result.EnrollmentId}");
        return CreatedAtAction(nameof(EnrollmentsController.GetById), "Enrollments",
            new { enrollmentId = result.EnrollmentId },
            new EnrollmentCreatedResponse(result.EnrollmentId, result.GroupId));
    }

    // Dodela ili izmena profesora i termina (za Pair/Individual profesor potvrdjuje
    // termin tek posle upisa). Vec dodeljen termin menja samo admin.
    [HttpPost("{groupId:guid}/assign-professor-slot")]
    [Authorize(Roles = Roles.Staff)]
    [ErrorCode(ErrorCodes.CannotAssign)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignProfessorToSlot(Guid groupId, AssignProfessorToSlotRequest request)
    {
        await _assignProfessor.Handle(new AssignProfessorToSlotCommand(
            groupId, request.SlotIndex, request.DayOfWeek, request.Time, request.ProfessorId, IsAdmin));

        await _audit.RecordAsync(CurrentUserId, "AssignSlot", "Group", groupId,
            $"slot={request.SlotIndex}, {request.DayOfWeek} {request.Time}, professor={request.ProfessorId}");
        return NoContent();
    }

    // Telo je opciono: { "force": true } pokrece grupu ispod minimuma, a unlockStartsAt
    // pomera pocetak otvaranja lekcija.
    [HttpPost("{groupId:guid}/start")]
    [Authorize(Roles = Roles.Staff)]
    [ErrorCode(ErrorCodes.CannotStart)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Start(Guid groupId, [FromBody] StartGroupRequest? request)
    {
        var force = request?.Force ?? false;
        await _startGroup.Handle(new StartGroupCommand(groupId, force, request?.UnlockStartsAt));

        await _audit.RecordAsync(CurrentUserId, "StartGroup", "Group", groupId,
            $"force={force}, unlockStartsAt={request?.UnlockStartsAt:O}");
        return NoContent();
    }

    // Rucno zatvaranje (samo admin, tesko se vraca). Automatski se grupa zatvara kad svi
    // clanovi koji nisu otkazani ili zamrznuti zavrse kurs (vidi CourseCompletedHandler).
    [HttpPost("{groupId:guid}/complete")]
    [Authorize(Roles = Roles.Admin)]
    [ErrorCode(ErrorCodes.CannotComplete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(Guid groupId)
    {
        await _completeGroup.Handle(new CompleteGroupCommand(groupId));
        await _audit.RecordAsync(CurrentUserId, "CompleteGroup", "Group", groupId);
        return NoContent();
    }
}
