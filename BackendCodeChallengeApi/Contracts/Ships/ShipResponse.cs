using BackendCodeChallengeApi.Data.Models;

namespace BackendCodeChallengeApi.Contracts.Ships;

public sealed record ShipResponse(string Code, string Name, string FiscalYearCode, bool IsActive)
{
    public static ShipResponse From(Ship s) => new(s.ShipCode, s.Name, s.FiscalYearCode, s.IsActive);
}