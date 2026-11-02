using BackendCodeChallengeApi.Data.Models;

namespace BackendCodeChallengeApi.Contracts.FinancialReports;

public sealed record FinancialSummaryMonthResponse(string Period, decimal? Actual, decimal? Budget, decimal Variance)
{
    public static FinancialSummaryMonthResponse From(FinancialSummaryMonth m) =>
        new(ReportFormat.Month(m.AccountPeriod), m.Actual, m.Budget, m.Variance);
}