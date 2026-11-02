namespace BackendCodeChallengeApi.Data.Models;

public sealed record FinancialReportHeader(
    string ShipCode, string ShipName, string FiscalYearCode, DateTime Period, DateTime YtdFrom, DateTime YtdTo);