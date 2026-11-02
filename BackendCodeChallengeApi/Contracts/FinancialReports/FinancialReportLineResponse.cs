using BackendCodeChallengeApi.Data.Models;

namespace BackendCodeChallengeApi.Contracts.FinancialReports;

public sealed record FinancialReportLineResponse(
    string AccountNumber, string Description, int Level, bool IsParent,
    decimal? Actual, decimal? Budget, decimal? Variance,
    decimal? ActualYtd, decimal? BudgetYtd, decimal? VarianceYtd)
{
    public static FinancialReportLineResponse From(FinancialReportLine l) => new(
        l.AccountNumber, l.CoaDescription, l.Level, l.IsParent,
        l.ActualPeriod, l.BudgetPeriod, l.VarianceActual, l.ActualYtd, l.BudgetYtd, l.VarianceYtd);
}