namespace BackendCodeChallengeApi.Data.Models;

public sealed record FinancialSummaryYtd(decimal? ActualYtd, decimal? BudgetYtd, decimal VarianceYtd);