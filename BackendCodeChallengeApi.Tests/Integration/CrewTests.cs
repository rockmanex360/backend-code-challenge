using BackendCodeChallengeApi.Contracts.Crew;

namespace BackendCodeChallengeApi.Tests.Integration;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CrewTests(SqlServerFixture db)
{
    private static readonly DateTime AsOf = new(2025, 7, 20);

    private Task Fixture93() => db.OnceAsync("T03", async () =>
    {
        await db.AddShipAsync("T03");
        await db.AddCrewAsync("T03", "T03-C1", "2025-04-05", "2025-07-25");
        await db.AddCrewAsync("T03", "T03-C2", "2025-03-01", "2025-06-20");
        await db.AddCrewAsync("T03", "T03-C3", "2025-03-01", "2025-06-19");
        await db.AddCrewAsync("T03", "T03-C4", "2025-08-01", "2025-11-01");
        await db.AddCrewAsync("T03", "T03-C5", "2025-01-10", "2025-04-10", signOff: "2025-06-30");
    });

    [Fact]
    public async Task T_C1_eoc_30_days_before_asof_is_onboard()
    {
        await Fixture93();
        var (_, rows) = await db.CrewListAsync("T03", AsOf);
        Assert.Equal("Onboard", rows.Single(r => r.CrewMemberId == "T03-C1").Status);
        Assert.Equal("Onboard", rows.Single(r => r.CrewMemberId == "T03-C2").Status);
    }

    [Fact]
    public async Task T_C2_eoc_31_days_before_asof_is_relief_due()
    {
        await Fixture93();
        var (_, rows) = await db.CrewListAsync("T03", AsOf);
        Assert.Equal("Relief Due", rows.Single(r => r.CrewMemberId == "T03-C3").Status);
    }

    [Fact]
    public async Task T_C3_future_sign_on_is_excluded()
    {
        await Fixture93();
        var (total, rows) = await db.CrewListAsync("T03", AsOf);
        Assert.DoesNotContain(rows, r => r.CrewMemberId == "T03-C4");
        Assert.Equal(3, total);
    }

    [Fact]
    public async Task T_C4_signed_off_is_excluded()
    {
        await Fixture93();
        var (_, rows) = await db.CrewListAsync("T03", AsOf);
        Assert.DoesNotContain(rows, r => r.CrewMemberId == "T03-C5");
        Assert.Equal(["T03-C1", "T03-C2", "T03-C3"], rows.Select(r => r.CrewMemberId).Order());
    }

    [Theory]
    [InlineData("2025-07-29", 44)]
    [InlineData("2025-07-30", 45)]
    public async Task T_C5_age_turns_on_the_birthday(string asOf, int age)
    {
        await db.OnceAsync("TC05", async () =>
        {
            await db.AddShipAsync("TC05");
            await db.AddCrewAsync("TC05", "TC05-1", "2025-01-01", "2025-12-31", birth: "1980-07-30");
        });
        var (_, rows) = await db.CrewListAsync("TC05", DateTime.Parse(asOf));
        Assert.Equal(age, rows.Single().Age);
    }

    [Fact]
    public async Task T_C6_search_by_sign_on_date_matches_any_year()
    {
        await db.OnceAsync("TC06", async () =>
        {
            await db.AddShipAsync("TC06");
            await db.AddCrewAsync("TC06", "TC06-1", "2024-04-05", "2025-12-31");
            await db.AddCrewAsync("TC06", "TC06-2", "2025-04-05", "2025-12-31");
            await db.AddCrewAsync("TC06", "TC06-3", "2025-04-15", "2025-12-31");
            await db.AddCrewAsync("TC06", "TC06-4", "2025-04-06", "2025-12-31");
            await db.AddCrewAsync("TC06", "TC06-5", "2025-05-05", "2025-12-31");
        });
        var (total, rows) = await db.CrewListAsync("TC06", AsOf, search: "05 Apr");
        Assert.Equal(2, total);
        Assert.Equal(["TC06-1", "TC06-2"], rows.Select(r => r.CrewMemberId));
    }

    [Theory]
    [InlineData("%", "TC07-1")]
    [InlineData("_", "TC07-2")]
    [InlineData("[", "TC07-3")]
    [InlineData("\\", "TC07-4")]
    public async Task T_C7_wildcards_in_search_match_literally(string search, string expectedId)
    {
        await db.OnceAsync("TC07", async () =>
        {
            await db.AddShipAsync("TC07");
            await db.AddCrewAsync("TC07", "TC07-1", "2025-01-01", "2025-12-31", last: "50%off");
            await db.AddCrewAsync("TC07", "TC07-2", "2025-01-01", "2025-12-31", last: "a_b");
            await db.AddCrewAsync("TC07", "TC07-3", "2025-01-01", "2025-12-31", last: "x[1]");
            await db.AddCrewAsync("TC07", "TC07-4", "2025-01-01", "2025-12-31", last: @"back\slash");
            await db.AddCrewAsync("TC07", "TC07-5", "2025-01-01", "2025-12-31", last: "plain");
        });
        var (total, rows) = await db.CrewListAsync("TC07", AsOf, search: search);
        Assert.Equal(1, total);
        Assert.Equal(expectedId, rows.Single().CrewMemberId);
    }

    [Theory]
    [InlineData("greek", 1)]
    [InlineData("GREEK", 1)]
    [InlineData("onboard", 0)]
    [InlineData("relief", 0)]
    public async Task T_C8_search_ignores_case_and_status_text(string search, int expected)
    {
        await db.OnceAsync("TC08", async () =>
        {
            await db.AddShipAsync("TC08");
            await db.AddCrewAsync("TC08", "TC08-1", "2025-01-01", "2025-12-31");
            await db.AddCrewAsync("TC08", "TC08-2", "2025-01-01", "2025-05-01", nat: "PH");
        });
        var (total, _) = await db.CrewListAsync("TC08", AsOf, search: search);
        Assert.Equal(expected, total);
    }

    [Theory]
    [InlineData(1, 10, 10, "TC09-01")]
    [InlineData(2, 10, 10, "TC09-11")]
    [InlineData(3, 10, 5, "TC09-21")]
    [InlineData(4, 10, 0, null)]
    public async Task T_C9_paging_keeps_total_count(int page, int size, int expectedRows, string? firstId)
    {
        await db.OnceAsync("TC09", async () =>
        {
            await db.AddShipAsync("TC09");
            foreach (var n in Enumerable.Range(1, 25))
                await db.AddCrewAsync("TC09", $"TC09-{n:00}", "2025-01-01", "2025-12-31");
        });
        var (total, rows) = await db.CrewListAsync("TC09", AsOf, page, size);
        Assert.Equal(25, total);
        Assert.Equal(expectedRows, rows.Count);
        Assert.Equal(firstId, rows.FirstOrDefault()?.CrewMemberId);
    }

    public static TheoryData<string?, string, string> SortCases => new()
    {
        { null, "ASC", "1,5,4,2,3" },
        { nameof(CrewSortColumn.RankName), "ASC", "2,4,3,1,5" },
        { nameof(CrewSortColumn.RankName), "DESC", "1,5,3,4,2" },
        { nameof(CrewSortColumn.CrewMemberId), "ASC", "1,2,3,4,5" },
        { nameof(CrewSortColumn.CrewMemberId), "DESC", "5,4,3,2,1" },
        { nameof(CrewSortColumn.FirstName), "ASC", "3,1,5,4,2" },
        { nameof(CrewSortColumn.FirstName), "DESC", "2,4,5,1,3" },
        { nameof(CrewSortColumn.LastName), "ASC", "1,5,2,4,3" },
        { nameof(CrewSortColumn.LastName), "DESC", "3,4,2,1,5" },
        { nameof(CrewSortColumn.Age), "ASC", "3,2,4,1,5" },
        { nameof(CrewSortColumn.Age), "DESC", "1,5,4,2,3" },
        { nameof(CrewSortColumn.Nationality), "ASC", "2,1,5,4,3" },
        { nameof(CrewSortColumn.Nationality), "DESC", "3,1,5,4,2" },
        { nameof(CrewSortColumn.SignOnDate), "ASC", "2,3,1,5,4" },
        { nameof(CrewSortColumn.SignOnDate), "DESC", "1,5,4,3,2" },
        { nameof(CrewSortColumn.Status), "ASC", "1,5,4,3,2" },
        { nameof(CrewSortColumn.Status), "DESC", "2,1,5,4,3" },
    };

    [Theory]
    [MemberData(nameof(SortCases))]
    public async Task T_C10_sorts_every_column_both_directions(string? sortBy, string dir, string expectedOrder)
    {
        await db.OnceAsync("TC10", async () =>
        {
            await db.AddShipAsync("TC10");
            await db.AddCrewAsync("TC10", "TC10-1", "2025-03-01", "2025-12-01", birth: "1980-01-01", rank: "Master", first: "Wu", last: "Alpha");
            await db.AddCrewAsync("TC10", "TC10-2", "2025-01-15", "2025-06-01", birth: "1990-12-31", rank: "Bosun", nat: "PH", first: "Zed", last: "Beta");
            await db.AddCrewAsync("TC10", "TC10-3", "2025-02-01", "2025-12-01", birth: "2000-07-20", rank: "Deck Cadet", nat: "IN", first: "Vic", last: "Gamma");
            await db.AddCrewAsync("TC10", "TC10-4", "2025-03-01", "2025-12-01", birth: "1985-05-05", rank: "Chief Engineer", first: "Yan", last: "Delta");
            await db.AddCrewAsync("TC10", "TC10-5", "2025-03-01", "2025-12-01", birth: "1980-06-06", rank: "Master", first: "Xia", last: "Alpha");
        });
        var (total, rows) = await db.CrewListAsync("TC10", AsOf, sortBy: sortBy, dir: dir);
        Assert.Equal(5, total);
        Assert.Equal(expectedOrder, string.Join(',', rows.Select(r => r.CrewMemberId[^1])));
    }

    [Fact]
    public async Task T_C11_unknown_ship_throws_50001()
    {
        Assert.Equal(50001, await SqlServerFixture.SqlErrorAsync(() => db.CrewListAsync("NOPE1", AsOf)));
    }

    [Fact]
    public async Task T_C11_inactive_ship_throws_50004()
    {
        await db.OnceAsync("TC11", () => db.AddShipAsync("TC11", active: false));
        Assert.Equal(50004, await SqlServerFixture.SqlErrorAsync(() => db.CrewListAsync("TC11", AsOf)));
    }

    [Fact]
    public async Task T_C12_sort_injection_throws_50010_and_drops_nothing()
    {
        await db.OnceAsync("TC12", () => db.AddShipAsync("TC12"));
        Assert.Equal(50010, await SqlServerFixture.SqlErrorAsync(() => db.CrewListAsync("TC12", AsOf, sortBy: "x; DROP TABLE Ship")));
        Assert.NotNull(await db.ScalarAsync<int?>("SELECT OBJECT_ID('dbo.Ship')"));
    }

    [Fact]
    public async Task T_C13_seed_has_no_crew_member_on_two_ships_at_once()
    {
        Assert.True(await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.CrewService WHERE ShipCode LIKE 'SHIP%'") > 0);
        Assert.Equal(0, await db.ScalarAsync<int>("""
            SELECT COUNT(*) FROM (
                SELECT CrewMemberId FROM dbo.CrewService
                WHERE ShipCode LIKE 'SHIP%' AND SignOffDate IS NULL AND SignOnDate <= CAST(SYSUTCDATETIME() AS DATE)
                GROUP BY CrewMemberId HAVING COUNT(*) > 1) x
            """));
    }

    [Fact]
    public async Task T_C14_onboard_here_and_planned_there_is_listed_only_here()
    {
        await db.OnceAsync("TC14A", async () =>
        {
            await db.AddShipAsync("TC14A");
            await db.AddShipAsync("TC14B");
            await db.AddCrewAsync("TC14A", "TC14-1", "2025-03-01", "2025-12-01");
            await db.AddCrewAsync("TC14B", "TC14-1", "2025-08-01", "2025-12-01");
        });
        var (totalA, rowsA) = await db.CrewListAsync("TC14A", AsOf);
        var (totalB, rowsB) = await db.CrewListAsync("TC14B", AsOf);
        Assert.Equal(1, totalA);
        Assert.Equal("Onboard", rowsA.Single().Status);
        Assert.Equal(0, totalB);
        Assert.Empty(rowsB);
    }
}