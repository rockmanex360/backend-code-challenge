using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BackendCodeChallengeApi.Data.Errors;
using BackendCodeChallengeApi.Data.Models;
using BackendCodeChallengeApi.Data.Repositories;
using BackendCodeChallengeApi.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace BackendCodeChallengeApi.Tests.Unit;

public sealed class FinancialReportsControllerTests : IClassFixture<FinancialReportsControllerTests.Factory>
{
    private const string ValidKey = "test-key";
    private static readonly DateTime July = new(2025, 7, 1);
    private static readonly FinancialReportHeader Header = new("T01", "Flying Dutchman", "0112", July, new(2025, 1, 1), July);

    private readonly Mock<IFinancialReportRepository> _repo = new();
    private readonly HttpClient _client;
    private readonly HttpClient _anonymous;

    public FinancialReportsControllerTests(Factory factory)
    {
        var customized = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IFinancialReportRepository>();
            s.AddSingleton(_repo.Object);
        }));
        _client = customized.CreateClient();
        _anonymous = customized.CreateClient();
        _client.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, ValidKey);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Auth:ApiKey", ValidKey);
            builder.UseSetting("ConnectionStrings:Default", "Server=127.0.0.1,1;Connect Timeout=1;Encrypt=False");
        }
    }

    [Theory]
    [InlineData("detail?period=2025-07")]
    [InlineData("summary")]
    public async Task Report_with_11_char_ship_code_returns_404_without_hitting_repo(string tail)
    {
        var response = await _client.GetAsync($"/api/ships/ABCDEFGHIJK/financial-reports/{tail}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Detail_returns_report_shape_with_golden_numbers_and_nulls()
    {
        FinancialReportLine[] lines =
        [
            new("7120000", "AWARDS", 3, false, "7100000", 1000m, 1100m, -100m, 3200m, 3300m, -100m),
            new("7135000", "SCHOLARSHIPS", 3, false, "7100000", 2300m, 2200m, 100m, 6400m, 6300m, 100m),
            new("7140000", "NO ACTUAL", 3, false, "7100000", null, 50m, -50m, null, 50m, -50m)
        ];
        _repo.Setup(r => r.GetDetailAsync("T01", July, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialReportDetail(Header, lines));

        var response = await _client.GetAsync("/api/ships/T01/financial-reports/detail?period=2025-07");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("T01", body.GetProperty("ship").GetProperty("code").GetString());
        Assert.Equal("Flying Dutchman", body.GetProperty("ship").GetProperty("name").GetString());
        Assert.Equal("0112", body.GetProperty("ship").GetProperty("fiscalYearCode").GetString());
        Assert.Equal("2025-07", body.GetProperty("period").GetString());
        Assert.Equal("2025-01", body.GetProperty("ytdFrom").GetString());
        Assert.Equal("2025-07", body.GetProperty("ytdTo").GetString());
        var items = body.GetProperty("lines");
        Assert.Equal(3, items.GetArrayLength());
        var l = items[1];
        Assert.Equal("7135000", l.GetProperty("accountNumber").GetString());
        Assert.Equal("SCHOLARSHIPS", l.GetProperty("description").GetString());
        Assert.Equal(3, l.GetProperty("level").GetInt32());
        Assert.False(l.GetProperty("isParent").GetBoolean());
        Assert.Equal(2300m, l.GetProperty("actual").GetDecimal());
        Assert.Equal(2200m, l.GetProperty("budget").GetDecimal());
        Assert.Equal(100m, l.GetProperty("variance").GetDecimal());
        Assert.Equal(6400m, l.GetProperty("actualYtd").GetDecimal());
        Assert.Equal(6300m, l.GetProperty("budgetYtd").GetDecimal());
        Assert.Equal(100m, l.GetProperty("varianceYtd").GetDecimal());
        Assert.False(l.TryGetProperty("parentAccountNumber", out _));
        var nulls = items[2];
        Assert.Equal(JsonValueKind.Null, nulls.GetProperty("actual").ValueKind);
        Assert.Equal(JsonValueKind.Null, nulls.GetProperty("actualYtd").ValueKind);
        Assert.Equal(50m, nulls.GetProperty("budget").GetDecimal());
    }

    [Fact]
    public async Task Summary_returns_months_with_null_month_and_ytd()
    {
        FinancialSummaryMonth[] months =
        [
            new(new(2025, 1, 1), null, null, 0m),
            new(new(2025, 2, 1), 2000m, 2100m, -100m),
            new(new(2025, 7, 1), 3300m, 3300m, 0m)
        ];
        _repo.Setup(r => r.GetSummaryAsync("T01", July, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialReportSummary(Header, months, new(9600m, 9600m, 0m)));

        var response = await _client.GetAsync("/api/ships/T01/financial-reports/summary?period=2025-07");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2025-07", body.GetProperty("period").GetString());
        Assert.Equal("2025-01", body.GetProperty("ytdFrom").GetString());
        var m = body.GetProperty("months");
        Assert.Equal(3, m.GetArrayLength());
        Assert.Equal("2025-01", m[0].GetProperty("period").GetString());
        Assert.Equal(JsonValueKind.Null, m[0].GetProperty("actual").ValueKind);
        Assert.Equal(JsonValueKind.Null, m[0].GetProperty("budget").ValueKind);
        Assert.Equal(0m, m[0].GetProperty("variance").GetDecimal());
        Assert.Equal(-100m, m[1].GetProperty("variance").GetDecimal());
        var ytd = body.GetProperty("ytd");
        Assert.Equal(9600m, ytd.GetProperty("actual").GetDecimal());
        Assert.Equal(9600m, ytd.GetProperty("budget").GetDecimal());
        Assert.Equal(0m, ytd.GetProperty("variance").GetDecimal());
    }

    [Theory]
    [InlineData("detail", "?period=2025-7")]
    [InlineData("detail", "?period=2025-13")]
    [InlineData("detail", "?period=2025-00")]
    [InlineData("detail", "?period=07-2025")]
    [InlineData("detail", "?period=")]
    [InlineData("detail", "")]
    [InlineData("summary", "?period=2025-7")]
    [InlineData("summary", "?period=2025-13")]
    [InlineData("summary", "?period=07-2025")]
    [InlineData("summary", "")]
    public async Task Bad_or_missing_period_returns_400_validation_problem(string report, string query)
    {
        var response = await _client.GetAsync($"/api/ships/T01/financial-reports/{report}{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").EnumerateObject().Any());
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Lowercase_code_is_upper_cased_and_period_is_first_of_month()
    {
        _repo.Setup(r => r.GetDetailAsync("T01", July, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialReportDetail(Header, []));
        _repo.Setup(r => r.GetSummaryAsync("T01", July, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialReportSummary(Header, [], new(null, null, 0m)));

        var detail = await _client.GetAsync("/api/ships/t01/financial-reports/detail?period=2025-07");
        var summary = await _client.GetAsync("/api/ships/t01/financial-reports/summary?period=2025-07");

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
        _repo.VerifyAll();
    }

    [Theory]
    [InlineData("detail")]
    [InlineData("summary")]
    public async Task Missing_or_inactive_ship_returns_404_problem(string report)
    {
        var ex = new NotFoundException("Ship 'X' not found.", null);
        _repo.Setup(r => r.GetDetailAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ThrowsAsync(ex);
        _repo.Setup(r => r.GetSummaryAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ThrowsAsync(ex);

        var response = await _client.GetAsync($"/api/ships/X/financial-reports/{report}?period=2025-07");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("detail")]
    [InlineData("summary")]
    public async Task Missing_api_key_returns_401(string report)
    {
        var response = await _anonymous.GetAsync($"/api/ships/T01/financial-reports/{report}?period=2025-07");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _repo.VerifyNoOtherCalls();
    }
}