namespace BackendCodeChallengeApi.Contracts.FinancialReports;

public sealed record FinancialSummaryYtdResponse(decimal? Actual, decimal? Budget, decimal Variance);