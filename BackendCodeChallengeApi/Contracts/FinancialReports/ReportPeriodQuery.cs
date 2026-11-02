using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace BackendCodeChallengeApi.Contracts.FinancialReports;

public sealed class ReportPeriodQuery
{
    [Required]
    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Period must be in yyyy-MM format.")]
    public string? Period { get; init; }

    public DateTime ToFirstOfMonth() => DateTime.ParseExact(Period!, "yyyy-MM", CultureInfo.InvariantCulture);
}