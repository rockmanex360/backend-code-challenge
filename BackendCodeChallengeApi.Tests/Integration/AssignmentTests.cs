using BackendCodeChallengeApi.Data.Errors;
using BackendCodeChallengeApi.Data.Repositories;
using Microsoft.Data.SqlClient;

namespace BackendCodeChallengeApi.Tests.Integration;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AssignmentTests(SqlServerFixture db)
{
    private readonly ShipRepository _ships = new(db.Factory);
    private readonly UserRepository _users = new(db.Factory);

    [Fact]
    public async Task T_A1_assigning_twice_keeps_one_row_and_both_calls_succeed()
    {
        await db.OnceAsync("TA01", () => db.AddShipAsync("TA01"));
        var user = await _users.CreateAsync("Assign Twice", "Viewer", default);

        await _ships.AssignAsync(user.UserId, "TA01", default);
        await _ships.AssignAsync(user.UserId, "TA01", default);

        Assert.Equal(1, await db.UserShipCountAsync(user.UserId, "TA01"));
    }

    [Fact]
    public async Task T_A2_duplicate_ship_code_is_50003_conflict()
    {
        await _ships.CreateAsync("TA02", "First", "0112", true, default);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _ships.CreateAsync("TA02", "Second", "0112", true, default));
        Assert.Equal(50003, ((SqlException)ex.InnerException!).Number);
    }
}