using System.Data;
using BackendCodeChallengeApi.Data.Errors;
using BackendCodeChallengeApi.Data.Models;
using BackendCodeChallengeApi.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BackendCodeChallengeApi.Data.Repositories;

public sealed class FinancialReportRepository(IDbConnectionFactory connection) : IFinancialReportRepository
{
    public async Task<FinancialReportDetail> GetDetailAsync(string shipCode, DateTime period, CancellationToken ct)
    {
        await using var conn = await connection.OpenAsync(ct);
        try
        {
            using var grid = await conn.QueryMultipleAsync(new CommandDefinition
            (
                "dbo.usp_FinancialReport_Detail",
                new { ShipCode = shipCode, Period = period },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct
            ));

            var header = await grid.ReadSingleAsync<FinancialReportHeader>();
            var lines = (await grid.ReadAsync<FinancialReportLine>()).AsList();

            return new FinancialReportDetail(header, lines);
        }
        catch (SqlException ex) when (SqlErrorTranslator.TryTranslate(ex, out var domain))
        {
            throw domain;
        }
    }

    public async Task<FinancialReportSummary> GetSummaryAsync(string shipCode, DateTime period, CancellationToken ct)
    {
        await using var conn = await connection.OpenAsync(ct);
        try
        {
            using var grid = await conn.QueryMultipleAsync(new CommandDefinition
            (
                "dbo.usp_FinancialReport_Summary",
                new { ShipCode = shipCode, Period = period },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct
            ));
            
            var header = await grid.ReadSingleAsync<FinancialReportHeader>();
            var months = (await grid.ReadAsync<FinancialSummaryMonth>()).AsList();
            var ytd = await grid.ReadSingleAsync<FinancialSummaryYtd>();
            
            return new FinancialReportSummary(header, months, ytd);
        }
        catch (SqlException ex) when (SqlErrorTranslator.TryTranslate(ex, out var domain))
        {
            throw domain;
        }
    }
}