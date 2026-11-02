using BackendCodeChallengeApi.Data.Models;

namespace BackendCodeChallengeApi.Contracts.FinancialReports;

public sealed record FinancialReportResponse(
    ReportShip Ship, string Period, string YtdFrom, string YtdTo, IReadOnlyList<FinancialReportLineResponse> Lines)
{
    public static FinancialReportResponse From(FinancialReportDetail d) => new(
        new ReportShip(d.Header.ShipCode, d.Header.ShipName, d.Header.FiscalYearCode),
        ReportFormat.Month(d.Header.Period), ReportFormat.Month(d.Header.YtdFrom), ReportFormat.Month(d.Header.YtdTo),
        d.Lines.Select(FinancialReportLineResponse.From).ToList());
}