using System.Data;
using BackendCodeChallengeApi.Data.Errors;
using BackendCodeChallengeApi.Data.Models;
using BackendCodeChallengeApi.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BackendCodeChallengeApi.Data.Repositories;

public sealed class ShipRepository(IDbConnectionFactory connection) : IShipRepository
{
    public async Task<Ship> CreateAsync(string shipCode, string name, string fiscalYearCode, bool isActive, CancellationToken ct)
    {
        await using var conn = await connection.OpenAsync(ct);
        try
        {
            return await conn.QuerySingleAsync<Ship>(new CommandDefinition(
                "dbo.usp_Ship_Create",
                new { ShipCode = shipCode, Name = name, FiscalYearCode = fiscalYearCode, IsActive = isActive },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
        }
        catch (SqlException ex) when (SqlErrorTranslator.TryTranslate(ex, out var domain))
        {
            throw domain;
        }
    }

    public async Task<Ship> GetAsync(string shipCode, CancellationToken ct)
    {
        await using var conn = await connection.OpenAsync(ct);
        try
        {
            return await conn.QuerySingleAsync<Ship>(new CommandDefinition(
                "dbo.usp_Ship_Get",
                new { ShipCode = shipCode },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
        }
        catch (SqlException ex) when (SqlErrorTranslator.TryTranslate(ex, out var domain))
        {
            throw domain;
        }
    }

    public async Task<PagedResult<Ship>> ListAsync(string? status, int pageNumber, int pageSize, CancellationToken ct)
    {
        await using var conn = await connection.OpenAsync(ct);
        try
        {
            using var grid = await conn.QueryMultipleAsync(new CommandDefinition(
                "dbo.usp_Ship_List",
                new { Status = status, PageNumber = pageNumber, PageSize = pageSize },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
            var total = await grid.ReadSingleAsync<int>();
            var items = (await grid.ReadAsync<Ship>()).AsList();
            return new PagedResult<Ship>(total, items);
        }
        catch (SqlException ex) when (SqlErrorTranslator.TryTranslate(ex, out var domain))
        {
            throw domain;
        }
    }

    public async Task AssignAsync(int userId, string shipCode, CancellationToken ct)
    {
        await using var conn = await connection.OpenAsync(ct);
        try
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "dbo.usp_UserShip_Assign",
                new { UserId = userId, ShipCode = shipCode },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
        }
        catch (SqlException ex) when (SqlErrorTranslator.TryTranslate(ex, out var domain))
        {
            throw domain;
        }
    }

    public async Task UnassignAsync(int userId, string shipCode, CancellationToken ct)
    {
        await using var conn = await connection.OpenAsync(ct);
        try
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "dbo.usp_UserShip_Unassign",
                new { UserId = userId, ShipCode = shipCode },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
        }
        catch (SqlException ex) when (SqlErrorTranslator.TryTranslate(ex, out var domain))
        {
            throw domain;
        }
    }

    public async Task<IReadOnlyList<Ship>> ListByUserAsync(int userId, string? status, CancellationToken ct)
    {
        await using var conn = await connection.OpenAsync(ct);
        try
        {
            return (await conn.QueryAsync<Ship>(new CommandDefinition(
                "dbo.usp_Ship_ListByUser",
                new { UserId = userId, Status = status },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct))).AsList();
        }
        catch (SqlException ex) when (SqlErrorTranslator.TryTranslate(ex, out var domain))
        {
            throw domain;
        }
    }
}