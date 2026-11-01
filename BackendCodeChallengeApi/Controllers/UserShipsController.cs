using BackendCodeChallengeApi.Contracts.Ships;
using BackendCodeChallengeApi.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace BackendCodeChallengeApi.Controllers;

[ApiController]
[Route("api/users/{userId:int:min(1)}/ships")]
public sealed class UserShipsController(IShipRepository ships, ILogger<UserShipsController> logger) : ControllerBase
{
    /// <summary>Assigns a ship to a user. Idempotent.</summary>
    /// <response code="204">The ship is assigned.</response>
    /// <response code="401">Missing or invalid API key.</response>
    /// <response code="404">User or ship not found.</response>
    [HttpPut("{shipCode:regex(^[[A-Za-z0-9]]{{2,10}}$)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignShip(int userId, string shipCode, CancellationToken ct)
    {
        var code = shipCode.ToUpperInvariant();
        await ships.AssignAsync(userId, code, ct);
        logger.LogInformation("Ship assigned {UserId} {ShipCode}", userId, code);
        return NoContent();
    }

    /// <summary>Removes a ship from a user. Idempotent.</summary>
    /// <response code="204">The ship is not assigned.</response>
    /// <response code="401">Missing or invalid API key.</response>
    /// <response code="404">User or ship not found.</response>
    [HttpDelete("{shipCode:regex(^[[A-Za-z0-9]]{{2,10}}$)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnassignShip(int userId, string shipCode, CancellationToken ct)
    {
        var code = shipCode.ToUpperInvariant();
        await ships.UnassignAsync(userId, code, ct);
        logger.LogInformation("Ship unassigned {UserId} {ShipCode}", userId, code);
        return NoContent();
    }

    /// <summary>Lists the ships assigned to a user. Status is active, inactive or all (default).</summary>
    /// <response code="200">The user's ships.</response>
    /// <response code="400">Invalid status.</response>
    /// <response code="401">Missing or invalid API key.</response>
    /// <response code="404">User not found.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ShipResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ShipResponse>>> ListUserShips(
        int userId, [FromQuery] ShipStatusQuery filter, CancellationToken ct) =>
        (await ships.ListByUserAsync(userId, filter.ToSpStatus(), ct)).Select(ShipResponse.From).ToList();
}