using BackendCodeChallengeApi.Data.Errors;
using BackendCodeChallengeApi.Data.Models;
using BackendCodeChallengeApi.Data.Repositories;
using Microsoft.Data.SqlClient;

namespace BackendCodeChallengeApi.Tests.Integration;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class FinancialReportTests(SqlServerFixture db)
{
    private readonly FinancialReportRepository _repo = new(db.Factory);

    private static DateTime P(string yyyyMm) => DateTime.Parse(yyyyMm + "-01");

    private static void AssertLine(FinancialReportLine l, decimal? actual, decimal? budget, decimal? variance,
        decimal? actualYtd, decimal? budgetYtd, decimal? varianceYtd)
    {
        Assert.Equal((actual, budget, variance, actualYtd, budgetYtd, varianceYtd),
            (l.ActualPeriod, l.BudgetPeriod, l.VarianceActual, l.ActualYtd, l.BudgetYtd, l.VarianceYtd));
    }

    private Task Golden() => db.OnceAsync("T01", async () =>
    {
        await db.AddShipAsync("T01", "0112");
        await db.AddActualAsync("T01", "7135000", ("2025-02", 2000), ("2025-06", 2100), ("2025-07", 2300), ("2024-12", 999), ("2025-08", 888));
        await db.AddBudgetAsync("T01", "7135000", ("2025-02", 2100), ("2025-06", 2000), ("2025-07", 2200));
        await db.AddActualAsync("T01", "7120000", ("2025-03", 1200), ("2025-06", 1000), ("2025-07", 1000));
        await db.AddBudgetAsync("T01", "7120000", ("2025-03", 1100), ("2025-06", 1100), ("2025-07", 1100));
        await db.AddActualAsync("T01", "7140000", ("2025-07", 0));
        await db.AddBudgetAsync("T01", "7140000", ("2025-07", 0));
    });

    private Task Boundary() => db.OnceAsync("T02", async () =>
    {
        await db.AddShipAsync("T02", "0403");
        await db.AddActualAsync("T02", "7135000", ("2024-03", 500), ("2024-04", 100), ("2024-12", 200), ("2025-02", 300), ("2025-03", 50), ("2025-04", 70));
    });

    [Theory]
    [InlineData("2025-07-01")]
    [InlineData("2025-07-19")]
    public async Task T_F1_golden_detail_matches_the_brief(string period)
    {
        await Golden();
        var d = await _repo.GetDetailAsync("T01", DateTime.Parse(period), default);

        Assert.Equal(new FinancialReportHeader("T01", "Test T01", "0112", P("2025-07"), P("2025-01"), P("2025-07")), d.Header);
        Assert.Equal(["7000000", "7100000", "7120000", "7135000"], d.Lines.Select(l => l.AccountNumber));
        Assert.Equal(["OPERATING EXPENSES", "AWARD AND GRANT TO INDIVIDUALS", "AWARDS", "SCHOLARSHIPS"], d.Lines.Select(l => l.CoaDescription));
        Assert.Equal([1, 2, 3, 3], d.Lines.Select(l => l.Level));
        Assert.Equal([true, true, false, false], d.Lines.Select(l => l.IsParent));
        Assert.Equal([null, "7000000", "7100000", "7100000"], d.Lines.Select(l => l.ParentAccountNumber));
        AssertLine(d.Lines[0], 3300, 3300, 0, 9600, 9600, 0);
        AssertLine(d.Lines[1], 3300, 3300, 0, 9600, 9600, 0);
        AssertLine(d.Lines[2], 1000, 1100, -100, 3200, 3300, -100);
        AssertLine(d.Lines[3], 2300, 2200, 100, 6400, 6300, 100);
    }

    [Fact]
    public async Task T_F2_summary_skips_months_without_values()
    {
        await Golden();
        var s = await _repo.GetSummaryAsync("T01", P("2025-07"), default);

        Assert.Equal(P("2025-01"), s.Header.YtdFrom);
        Assert.Equal(4, s.Months.Count);
        Assert.Equal(new[] { "2025-02", "2025-03", "2025-06", "2025-07" }.Select(P), s.Months.Select(m => m.AccountPeriod));
        Assert.Equal<(decimal?, decimal?, decimal)>(
            [(2000, 2100, -100), (1200, 1100, 100), (3100, 3100, 0), (3300, 3300, 0)],
            s.Months.Select(m => (m.Actual, m.Budget, m.Variance)));
        Assert.Equal(new FinancialSummaryYtd(9600, 9600, 0), s.Ytd);
    }

    [Fact]
    public async Task T_F3_ytd_starts_at_the_fiscal_year_not_the_calendar_year()
    {
        await Boundary();
        var d = await _repo.GetDetailAsync("T02", P("2025-02"), default);
        Assert.Equal(P("2024-04"), d.Header.YtdFrom);
        AssertLine(d.Lines.Single(l => l.AccountNumber == "7135000"), 300, null, 300, 600, null, 600);

        var last = await _repo.GetDetailAsync("T02", P("2025-03"), default);
        AssertLine(last.Lines.Single(l => l.AccountNumber == "7135000"), 50, null, 50, 650, null, 650);
    }

    [Fact]
    public async Task T_F4_new_fiscal_year_month_has_ytd_of_that_month_only()
    {
        await Boundary();
        var d = await _repo.GetDetailAsync("T02", P("2025-04"), default);
        Assert.Equal(P("2025-04"), d.Header.YtdFrom);
        AssertLine(d.Lines.Single(l => l.AccountNumber == "7135000"), 70, null, 70, 70, null, 70);
    }

    [Fact]
    public async Task T_F5_leaf_with_only_zero_rows_is_omitted()
    {
        await Golden();
        var d = await _repo.GetDetailAsync("T01", P("2025-07"), default);
        Assert.DoesNotContain(d.Lines, l => l.AccountNumber == "7140000");
    }

    [Fact]
    public async Task T_F6_budget_without_actual_is_null_actual_and_negative_variance()
    {
        await db.OnceAsync("TF06", async () =>
        {
            await db.AddShipAsync("TF06");
            await db.AddBudgetAsync("TF06", "7120000", ("2025-07", 400));
        });
        var d = await _repo.GetDetailAsync("TF06", P("2025-07"), default);
        AssertLine(d.Lines.Single(l => l.AccountNumber == "7120000"), null, 400, -400, null, 400, -400);
    }

    [Fact]
    public async Task T_F7_duplicate_rows_are_summed()
    {
        await db.OnceAsync("TF07", async () =>
        {
            await db.AddShipAsync("TF07");
            await db.AddActualAsync("TF07", "7120000", ("2025-07", 100), ("2025-07", 150));
            await db.AddBudgetAsync("TF07", "7120000", ("2025-07", 50), ("2025-07", 50));
        });
        var d = await _repo.GetDetailAsync("TF07", P("2025-07"), default);
        AssertLine(d.Lines.Single(l => l.AccountNumber == "7120000"), 250, 100, 150, 250, 100, 150);
    }

    [Fact]
    public async Task T_F8_unknown_ship_is_50001()
    {
        var detail = await Assert.ThrowsAsync<NotFoundException>(() => _repo.GetDetailAsync("NOPE1", P("2025-07"), default));
        var summary = await Assert.ThrowsAsync<NotFoundException>(() => _repo.GetSummaryAsync("NOPE1", P("2025-07"), default));
        Assert.Equal(50001, ((SqlException)detail.InnerException!).Number);
        Assert.Equal(50001, ((SqlException)summary.InnerException!).Number);
    }

    [Fact]
    public async Task T_F9_seed_has_no_amounts_on_parent_accounts()
    {
        Assert.True(await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.AccountTransaction WHERE ShipCode LIKE 'SHIP%'") > 0);
        Assert.Equal(0, await db.ScalarAsync<int>("""
            SELECT (SELECT COUNT(*) FROM dbo.AccountTransaction t WHERE EXISTS (SELECT 1 FROM dbo.Account c WHERE c.ParentAccountNumber = t.AccountNumber))
                 + (SELECT COUNT(*) FROM dbo.Budget b WHERE EXISTS (SELECT 1 FROM dbo.Account c WHERE c.ParentAccountNumber = b.AccountNumber))
            """));
    }

    [Fact]
    public async Task T_F10_summary_for_april_fiscal_year_has_three_months()
    {
        await Boundary();
        var s = await _repo.GetSummaryAsync("T02", P("2025-02"), default);
        Assert.Equal(3, s.Months.Count);
        Assert.Equal(P("2024-04"), s.Months[0].AccountPeriod);
        Assert.Equal(P("2025-02"), s.Months[^1].AccountPeriod);
        Assert.Equal(100m, s.Months[0].Actual);
        Assert.Equal(200m, s.Months.Single(m => m.AccountPeriod == P("2024-12")).Actual);
        Assert.Equal(new FinancialSummaryYtd(600, null, 600), s.Ytd);
    }

    [Fact]
    public async Task T_F11_inactive_ship_is_50004_on_both_reports()
    {
        await db.OnceAsync("TF11", () => db.AddShipAsync("TF11", active: false));
        var detail = await Assert.ThrowsAsync<NotFoundException>(() => _repo.GetDetailAsync("TF11", P("2025-07"), default));
        var summary = await Assert.ThrowsAsync<NotFoundException>(() => _repo.GetSummaryAsync("TF11", P("2025-07"), default));
        Assert.Equal(50004, ((SqlException)detail.InnerException!).Number);
        Assert.Equal(50004, ((SqlException)summary.InnerException!).Number);
        Assert.Contains("inactive", detail.Message);
        Assert.Contains("inactive", summary.Message);
    }

    [Fact]
    public async Task T_F12_summary_without_any_values_has_null_ytd()
    {
        await Boundary();
        var s = await _repo.GetSummaryAsync("T02", P("2027-04"), default);
        Assert.Empty(s.Months);
        Assert.Equal(new FinancialSummaryYtd(null, null, null), s.Ytd);
    }

}