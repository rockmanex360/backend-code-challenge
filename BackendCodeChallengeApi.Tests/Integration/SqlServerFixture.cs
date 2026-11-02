using System.Data;
using BackendCodeChallengeApi.Data.Models;
using BackendCodeChallengeApi.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace BackendCodeChallengeApi.Tests.Integration;

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}

public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string Database = "ShipManagement";
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString { get; private set; } = "";
    public IDbConnectionFactory Factory => new SqlConnectionFactory(ConnectionString);

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var master = _container.GetConnectionString();
        await using (var conn = new SqlConnection(master))
            await conn.ExecuteAsync($"CREATE DATABASE {Database};");

        ConnectionString = new SqlConnectionStringBuilder(master) { InitialCatalog = Database }.ConnectionString;

        var scripts = Directory.GetFiles(Path.Combine(SolutionRoot(), "database"), "*.sql")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal);
        foreach (var script in scripts)
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            foreach (var batch in SplitOnGo(await File.ReadAllTextAsync(script)))
                await conn.ExecuteAsync(new CommandDefinition(batch, commandTimeout: 300));
        }

        await using var check = new SqlConnection(ConnectionString);
        if (await check.ExecuteScalarAsync<int>("SELECT CAST(SESSIONPROPERTY('QUOTED_IDENTIFIER') AS INT)") != 1)
            throw new InvalidOperationException("QUOTED_IDENTIFIER must be ON, as in db-init (sqlcmd -I).");
    }

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BackendCodeChallenge.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("BackendCodeChallenge.slnx not found above " + AppContext.BaseDirectory);
    }

    private static IEnumerable<string> SplitOnGo(string script)
    {
        var batch = new System.Text.StringBuilder();
        foreach (var line in script.Split('\n'))
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                if (batch.ToString().Trim().Length > 0) yield return batch.ToString();
                batch.Clear();
            }
            else batch.AppendLine(line.TrimEnd('\r'));
        }
        if (batch.ToString().Trim().Length > 0) yield return batch.ToString();
    }

    public async Task<T> ScalarAsync<T>(string sql, object? param = null)
    {
        await using var conn = new SqlConnection(ConnectionString);
        return (await conn.ExecuteScalarAsync<T>(sql, param))!;
    }

    public async Task ExecAsync(string sql, object? param = null)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.ExecuteAsync(sql, param);
    }

    public async Task<int> UserShipCountAsync(int userId, string shipCode) =>
        await ScalarAsync<int>("SELECT COUNT(*) FROM dbo.UserShip WHERE UserId = @userId AND ShipCode = @shipCode", new { userId, shipCode });

    public async Task OnceAsync(string shipCode, Func<Task> seed)
    {
        if (await ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Ship WHERE ShipCode = @shipCode", new { shipCode }) == 0)
            await seed();
    }

    public Task AddShipAsync(string code, string fiscalYear = "0112", bool active = true) =>
        ExecAsync("INSERT dbo.Ship (ShipCode, Name, FiscalYearCode, IsActive) VALUES (@code, @name, @fiscalYear, @active)",
            new { code, name = "Test " + code, fiscalYear, active });

    public Task AddCrewAsync(string ship, string id, string signOn, string eoc, string? signOff = null,
        string birth = "1980-01-01", string rank = "Master", string nat = "GR", string first = "Test", string last = "Crew") =>
        ExecAsync("""
            IF NOT EXISTS (SELECT 1 FROM dbo.CrewMember WHERE CrewMemberId = @id)
                INSERT dbo.CrewMember (CrewMemberId, FirstName, LastName, BirthDate, NationalityCode)
                VALUES (@id, @first, @last, CAST(@birth AS DATE), @nat);
            INSERT dbo.CrewService (CrewMemberId, ShipCode, CrewRankId, SignOnDate, SignOffDate, EndOfContractDate)
            SELECT @id, @ship, r.CrewRankId, CAST(@signOn AS DATE), CAST(@signOff AS DATE), CAST(@eoc AS DATE)
            FROM dbo.CrewRank r WHERE r.Name = @rank;
            """,
            new { ship, id, signOn, eoc, signOff, birth, rank, nat, first, last });

    public Task AddActualAsync(string ship, string account, params (string Period, decimal Value)[] rows) =>
        AddAmountsAsync("AccountTransaction", "ActualValue", ship, account, rows);

    public Task AddBudgetAsync(string ship, string account, params (string Period, decimal Value)[] rows) =>
        AddAmountsAsync("Budget", "BudgetValue", ship, account, rows);

    private async Task AddAmountsAsync(string table, string column, string ship, string account, (string Period, decimal Value)[] rows)
    {
        foreach (var (period, value) in rows)
            await ExecAsync($"INSERT dbo.{table} (ShipCode, AccountNumber, AccountPeriod, {column}) VALUES (@ship, @account, CAST(@p AS DATE), @value)",
                new { ship, account, p = period + "-01", value });
    }

    public async Task<(int Total, List<CrewListItem> Rows)> CrewListAsync(string ship, DateTime asOf, int page = 1, int size = 20,
        string? sortBy = null, string dir = "ASC", string? search = null)
    {
        await using var conn = new SqlConnection(ConnectionString);
        using var grid = await conn.QueryMultipleAsync("dbo.usp_Crew_List",
            new { ShipCode = ship, PageNumber = page, PageSize = size, SortBy = sortBy, SortDirection = dir, Search = search, AsOfDate = asOf },
            commandType: CommandType.StoredProcedure);
        var total = await grid.ReadSingleAsync<int>();
        return (total, (await grid.ReadAsync<CrewListItem>()).AsList());
    }

    public static async Task<int> SqlErrorAsync(Func<Task> act) => (await Assert.ThrowsAsync<SqlException>(act)).Number;
}