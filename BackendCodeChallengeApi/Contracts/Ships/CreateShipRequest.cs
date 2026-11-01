using System;
using System.ComponentModel.DataAnnotations;
using BackendCodeChallengeApi.Contracts.Validation;

namespace BackendCodeChallengeApi.Contracts.Ships;

public sealed record CreateShipRequest
{
    [Required, RegularExpression("^[A-Za-z0-9]{2,10}$", ErrorMessage = "Code must be 2-10 letters or digits.")]
    public string? Code { get; init; }

    [Required, StringLength(100)]
    public string? Name { get; init; }

    [Required, FiscalYearCode]
    public string? FiscalYearCode { get; init; }

    public bool? IsActive { get; init; }
}
