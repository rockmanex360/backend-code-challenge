namespace BackendCodeChallengeApi.Data.Models;

public sealed record FinancialSummaryMonth(DateTime AccountPeriod, decimal? Actual, decimal? Budget, decimal Variance);