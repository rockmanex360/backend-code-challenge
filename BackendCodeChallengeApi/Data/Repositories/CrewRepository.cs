using System;
using System.Data;
using BackendCodeChallengeApi.Data.Errors;
using BackendCodeChallengeApi.Data.Models;
using BackendCodeChallengeApi.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BackendCodeChallengeApi.Data.Repositories;

public sealed class CrewRepository(IDbConnectionFactory connection) : ICrewRepository
{
    public async Task<PagedResult<CrewListItem>> ListAsync(string shipCode, int pageNumber, int pageSize, string? sortBy, string sortDirection, string? search, CancellationToken cancellationToken)
    {
        await using var db = await connection.OpenAsync(cancellationToken);
        try
        {
            using var list = await db.QueryMultipleAsync(new CommandDefinition(
                "dbo.usp_Crew_List",
                new
                {
                    ShipCode = shipCode,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    SortBy = sortBy,
                    SortDirection = sortDirection,
                    Search = search,
                    AsOfDate = (DateTime?) null
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            ));

            var total = await list.ReadSingleAsync<int>();
            var items = (await list.ReadAsync<CrewListItem>()).AsList();
            return new PagedResult<CrewListItem>(total, items);
        }
        catch (SqlException ex) when (SqlErrorTranslator.TryTranslate(ex, out var domain))
        {
            throw domain;
        }
    }
}
