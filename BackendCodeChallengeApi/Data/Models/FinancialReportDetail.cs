namespace BackendCodeChallengeApi.Data.Models;

public sealed record FinancialReportDetail(FinancialReportHeader Header, IReadOnlyList<FinancialReportLine> Lines);