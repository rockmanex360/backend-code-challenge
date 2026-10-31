using System;
using Microsoft.Data.SqlClient;

namespace BackendCodeChallengeApi.Infrastructure;

public sealed class SqlConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
