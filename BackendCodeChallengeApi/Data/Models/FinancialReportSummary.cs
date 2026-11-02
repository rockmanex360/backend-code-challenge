namespace BackendCodeChallengeApi.Data.Models;

public sealed record FinancialReportSummary(FinancialReportHeader Header, IReadOnlyList<FinancialSummaryMonth> Months, FinancialSummaryYtd Ytd);