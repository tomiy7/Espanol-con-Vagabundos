using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GroupsService.API.DTOs;
using GroupsService.API.Errors;
using GroupsService.Application.Commands.RespondToInvitation;
using GroupsService.Application.Queries.GetInvitationByToken;

namespace GroupsService.API.Controllers;

// Pozivnice za par. Link iz mejla vodi na stranicu fronta ("prihvati/odbij"), koja:
//   1) GET  /invitations/{token}         - prikaze ko poziva i na koji kurs,
//   2) POST /invitations/{token}/accept  - ili /decline, na klik korisnika.
// GET ne menja stanje (mejl klijenti i skeneri linkova automatski otvaraju linkove, pa
// bi GET koji prihvata poziv mogao da ga prihvati bez korisnika). Rute su bez JWT-a:
// token u URL-u je jednokratan, slucajan i vezan za tacno jedan poziv.
[Route("invitations")]
[AllowAnonymous]
public class InvitationsController : ApiControllerBase
{
    private readonly GetInvitationByTokenHandler _getByToken;
    private readonly AcceptInvitationHandler _accept;
    private readonly DeclineInvitationHandler _decline;

    public InvitationsController(
        GetInvitationByTokenHandler getByToken,
        AcceptInvitationHandler accept,
        DeclineInvitationHandler decline)
    {
        _getByToken = getByToken;
        _accept = accept;
        _decline = decline;
    }

    [HttpGet("{token}")]
    [ProducesResponseType<InvitationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByToken(string token)
    {
        var invitation = await _getByToken.Handle(new GetInvitationByTokenQuery(token));
        return invitation == null
            ? ProblemWithCode(StatusCodes.Status404NotFound, ErrorCodes.InvitationNotFound, "Invitation does not exist.")
            : Ok(invitation);
    }

    [HttpPost("{token}/accept")]
    [ErrorCode(ErrorCodes.CannotAcceptInvitation)]
    [ProducesResponseType<InvitationAcceptedResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Accept(string token, [FromBody] AcceptInvitationRequest? request)
    {
        var result = await _accept.Handle(new AcceptInvitationCommand(token, request?.PaymentPlan));
        return Ok(new InvitationAcceptedResponse(result.InviteeEnrollmentId, result.GroupId));
    }

    [HttpPost("{token}/decline")]
    [ErrorCode(ErrorCodes.CannotDeclineInvitation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Decline(string token)
    {
        await _decline.Handle(new DeclineInvitationCommand(token));
        return NoContent();
    }
}
