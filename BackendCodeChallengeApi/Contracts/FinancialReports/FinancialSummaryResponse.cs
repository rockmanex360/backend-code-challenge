using BackendCodeChallengeApi.Data.Models;

namespace BackendCodeChallengeApi.Contracts.FinancialReports;

public sealed record FinancialSummaryResponse(
    ReportShip Ship, string Period, string YtdFrom, string YtdTo,
    IReadOnlyList<FinancialSummaryMonthResponse> Months, FinancialSummaryYtdResponse Ytd)
{
    public static FinancialSummaryResponse From(FinancialReportSummary s) => new(
        new ReportShip(s.Header.ShipCode, s.Header.ShipName, s.Header.FiscalYearCode),
        ReportFormat.Month(s.Header.Period), ReportFormat.Month(s.Header.YtdFrom), ReportFormat.Month(s.Header.YtdTo),
        s.Months.Select(FinancialSummaryMonthResponse.From).ToList(),
        new FinancialSummaryYtdResponse(s.Ytd.ActualYtd, s.Ytd.BudgetYtd, s.Ytd.VarianceYtd));
}