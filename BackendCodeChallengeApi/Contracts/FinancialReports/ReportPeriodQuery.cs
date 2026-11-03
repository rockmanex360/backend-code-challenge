using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BackendCodeChallengeApi.Contracts.FinancialReports;

public sealed class ReportPeriodQuery
{
    [Required]
    [FromQuery(Name = "period")]
    [BindRequired]
    [RegularExpression(@"^[1-9]\d{3}-(0[1-9]|1[0-2])$", ErrorMessage = "Period must be in yyyy-MM format.")]
    public string? Period { get; init; }

    public DateTime ToFirstOfMonth() => DateTime.ParseExact(Period!, "yyyy-MM", CultureInfo.InvariantCulture);
}