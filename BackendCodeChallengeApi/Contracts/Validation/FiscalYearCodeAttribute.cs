using System.ComponentModel.DataAnnotations;

namespace BackendCodeChallengeApi.Contracts.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class FiscalYearCodeAttribute : ValidationAttribute
{
    public FiscalYearCodeAttribute() : base("The field {0} must be a 12-month fiscal year code such as 0112 or 0403.") { }

    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        if (value is not string s || s.Length != 4 || !s.All(char.IsAsciiDigit))
            return false;

        var start = int.Parse(s[..2]);
        var end = int.Parse(s[2..]);
        return start is >= 1 and <= 12 && end == (start == 1 ? 12 : start - 1);
    }
}