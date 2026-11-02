using System.Globalization;

namespace BackendCodeChallengeApi.Contracts.FinancialReports;

internal static class ReportFormat
{
    public static string Month(DateTime d) => d.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}