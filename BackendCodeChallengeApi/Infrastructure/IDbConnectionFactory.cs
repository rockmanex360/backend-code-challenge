using System;
using Microsoft.Data.SqlClient;

namespace BackendCodeChallengeApi.Infrastructure;

public interface IDbConnectionFactory
{
    Task<SqlConnection> OpenAsync(CancellationToken cancellationToken);
}
