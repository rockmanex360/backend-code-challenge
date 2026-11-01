using System.Data;
using BackendCodeChallengeApi.Data.Errors;
using BackendCodeChallengeApi.Data.Models;
using BackendCodeChallengeApi.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BackendCodeChallengeApi.Data.Repositories;

public sealed class UserRepository(IDbConnectionFactory connection) : IUserRepository
{
    public async Task<User> CreateAsync(string name, string roleName, CancellationToken ct)
    {
        await using var conn = await connection.OpenAsync(ct);
        try
        {
            return await conn.QuerySingleAsync<User>(new CommandDefinition(
                "dbo.usp_User_Create",
                new { Name = name, RoleName = roleName },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
        }
        catch (SqlException ex) when (SqlErrorTranslator.TryTranslate(ex, out var domain))
        {
            throw domain;
        }
    }

    public async Task<User> GetAsync(int userId, CancellationToken ct)
    {
        await using var conn = await connection.OpenAsync(ct);
        try
        {
            return await conn.QuerySingleAsync<User>(new CommandDefinition(
                "dbo.usp_User_Get",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
        }
        catch (SqlException ex) when (SqlErrorTranslator.TryTranslate(ex, out var domain))
        {
            throw domain;
        }
    }

    public async Task<PagedResult<User>> ListAsync(int pageNumber, int pageSize, CancellationToken ct)
    {
        await using var conn = await connection.OpenAsync(ct);
        try
        {
            using var grid = await conn.QueryMultipleAsync(new CommandDefinition(
                "dbo.usp_User_List",
                new { PageNumber = pageNumber, PageSize = pageSize },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
            var total = await grid.ReadSingleAsync<int>();
            var items = (await grid.ReadAsync<User>()).AsList();
            return new PagedResult<User>(total, items);
        }
        catch (SqlException ex) when (SqlErrorTranslator.TryTranslate(ex, out var domain))
        {
            throw domain;
        }
    }
}