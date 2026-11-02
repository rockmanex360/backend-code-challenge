using BackendCodeChallengeApi.Contracts;
using BackendCodeChallengeApi.Contracts.Crew;
using BackendCodeChallengeApi.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace BackendCodeChallengeApi.Controllers;

[ApiController]
[Route("api/ships/{code:regex(^[[A-Za-z0-9]]{{2,10}}$)}/crew")]
public sealed class CrewController(ICrewRepository crew) : ControllerBase
{
    /// <summary>Lists the crew currently onboard or due for relief on a ship, with paging, sorting and search.</summary>
    /// <param name="code">Ship code (case-insensitive).</param>
    /// <param name="paging">Page number and size.</param>
    /// <param name="query">Sort column and direction, and search text (max 100 chars; blank means no search).</param>
    /// <response code="200">A page of crew.</response>
    /// <response code="400">Invalid paging, sort or search parameters.</response>
    /// <response code="401">Missing or invalid API key.</response>
    /// <response code="404">Ship not found or inactive.</response>
    [HttpGet]
    [ProducesResponseType<PagedResponse<CrewListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<CrewListItemResponse>>> ListCrew(
        string code, [FromQuery] PageQuery paging, [FromQuery] CrewListQuery query, CancellationToken ct)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var result = await crew.ListAsync(
            code.ToUpperInvariant(), paging.Page, paging.PageSize, query.SortBy?.ToString(), query.SortDir, search, ct);
        return PagedResponse<CrewListItemResponse>.Create(
            result.Items.Select(CrewListItemResponse.From).ToList(), paging.Page, paging.PageSize, result.TotalCount);
    }
}