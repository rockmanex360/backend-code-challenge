using BackendCodeChallengeApi.Data.Models;

namespace BackendCodeChallengeApi.Data.Repositories;

public interface IFinancialReportRepository
{
    Task<FinancialReportDetail> GetDetailAsync(string shipCode, DateTime period, CancellationToken cancellationToken);
    Task<FinancialReportSummary> GetSummaryAsync(string shipCode, DateTime period, CancellationToken cancellationToken);
}