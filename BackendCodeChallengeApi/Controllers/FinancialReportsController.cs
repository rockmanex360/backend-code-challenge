using BackendCodeChallengeApi.Contracts.FinancialReports;
using BackendCodeChallengeApi.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace BackendCodeChallengeApi.Controllers;

[ApiController]
[Route("api/ships/{code:regex(^[[A-Za-z0-9]]{{2,10}}$)}/financial-reports")]
public sealed class FinancialReportsController(IFinancialReportRepository reports) : ControllerBase
{
    /// <summary>Gets the account-level financial report for a ship and month, with year-to-date columns.</summary>
    /// <response code="200">The report. Missing actual or budget values are null, not 0.</response>
    /// <response code="400">Missing or malformed period.</response>
    /// <response code="401">Missing or invalid API key.</response>
    /// <response code="404">Ship not found or inactive.</response>
    [HttpGet("detail")]
    [ProducesResponseType<FinancialReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FinancialReportResponse>> GetDetail(
        string code, [FromQuery] ReportPeriodQuery query, CancellationToken ct) =>
        FinancialReportResponse.From(await reports.GetDetailAsync(code.ToUpperInvariant(), query.ToFirstOfMonth(), ct));

    /// <summary>Gets the ship-level monthly totals from the start of the fiscal year to the month, plus the YTD total.</summary>
    /// <response code="200">The summary. Months without data have null actual and budget.</response>
    /// <response code="400">Missing or malformed period.</response>
    /// <response code="401">Missing or invalid API key.</response>
    /// <response code="404">Ship not found or inactive.</response>
    [HttpGet("summary")]
    [ProducesResponseType<FinancialSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FinancialSummaryResponse>> GetSummary(
        string code, [FromQuery] ReportPeriodQuery query, CancellationToken ct) =>
        FinancialSummaryResponse.From(await reports.GetSummaryAsync(code.ToUpperInvariant(), query.ToFirstOfMonth(), ct));
}