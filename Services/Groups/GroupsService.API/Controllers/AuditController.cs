using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GroupsService.API.Constants;
using GroupsService.Application.Interfaces;

namespace GroupsService.API.Controllers;

// Pregled traga ko je sta uradio (samo admin). entityId = id grupe ili upisa.
[Route("audit")]
[Authorize(Roles = Roles.Admin)]
public class AuditController : ApiControllerBase
{
    private readonly IAuditLog _audit;

    public AuditController(IAuditLog audit)
    {
        _audit = audit;
    }

    // take je ogranicen na 1-200 (najnoviji prvi).
    [HttpGet]
    [ProducesResponseType<List<AuditEntryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid? entityId, [FromQuery] Guid? actorUserId, [FromQuery] int take = 50) =>
        Ok(await _audit.GetAsync(entityId, actorUserId, take));
}
