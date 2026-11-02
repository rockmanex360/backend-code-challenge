namespace BackendCodeChallengeApi.Data.Models;

public sealed record FinancialReportLine(
    string AccountNumber, string CoaDescription, int Level, bool IsParent, string? ParentAccountNumber,
    decimal? ActualPeriod, decimal? BudgetPeriod, decimal VarianceActual,
    decimal? ActualYtd, decimal? BudgetYtd, decimal VarianceYtd);