using BackendCodeChallengeApi.Contracts;
using BackendCodeChallengeApi.Contracts.Ships;
using BackendCodeChallengeApi.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace BackendCodeChallengeApi.Controllers;

[ApiController]
[Route("api/ships")]
public sealed class ShipsController(IShipRepository ships, ILogger<ShipsController> logger) : ControllerBase
{
    /// <summary>Creates a ship. The code is upper-cased before it is stored.</summary>
    /// <response code="201">Ship created.</response>
    /// <response code="400">Validation failed or the fiscal year code is unknown.</response>
    /// <response code="401">Missing or invalid API key.</response>
    /// <response code="409">A ship with this code already exists.</response>
    [HttpPost]
    [ProducesResponseType<ShipResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShipResponse>> CreateShip(CreateShipRequest request, CancellationToken ct)
    {
        var ship = await ships.CreateAsync(
            request.Code!.ToUpperInvariant(), request.Name!.Trim(), request.FiscalYearCode!, request.IsActive ?? true, ct);
        logger.LogInformation("Ship created {ShipCode}", ship.ShipCode);
        return CreatedAtAction(nameof(GetShip), new { code = ship.ShipCode }, ShipResponse.From(ship));
    }

    /// <summary>Lists ships, one page at a time. Status is active, inactive or all (default).</summary>
    /// <response code="200">A page of ships.</response>
    /// <response code="400">Invalid status or paging parameters.</response>
    /// <response code="401">Missing or invalid API key.</response>
    [HttpGet]
    [ProducesResponseType<PagedResponse<ShipResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResponse<ShipResponse>>> ListShips(
        [FromQuery] ShipStatusQuery filter, [FromQuery] PageQuery paging, CancellationToken ct)
    {
        var result = await ships.ListAsync(filter.ToSpStatus(), paging.Page, paging.PageSize, ct);
        return PagedResponse<ShipResponse>.Create(
            result.Items.Select(ShipResponse.From).ToList(), paging.Page, paging.PageSize, result.TotalCount);
    }

    /// <summary>Gets one ship by code (case-insensitive).</summary>
    /// <response code="200">The ship.</response>
    /// <response code="401">Missing or invalid API key.</response>
    /// <response code="404">Ship not found.</response>
    [HttpGet("{code:regex(^[[A-Za-z0-9]]{{2,10}}$)}")]
    [ProducesResponseType<ShipResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShipResponse>> GetShip(string code, CancellationToken ct) =>
        ShipResponse.From(await ships.GetAsync(code.ToUpperInvariant(), ct));
}