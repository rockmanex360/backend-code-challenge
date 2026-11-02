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

public sealed class CrewControllerTests : IClassFixture<CrewControllerTests.Factory>
{
    private const string ValidKey = "test-key";
    private static readonly CrewListItem Soka =
        new("Master", "CREW001", "Soka", "Philip", 46, "Greek", new DateTime(2025, 4, 5), "Relief Due");

    private readonly Mock<ICrewRepository> _repo = new();
    private readonly HttpClient _client;
    private readonly HttpClient _anonymous;

    public CrewControllerTests(Factory factory)
    {
        var customized = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<ICrewRepository>();
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

    private void SetupAny(PagedResult<CrewListItem>? result = null) =>
        _repo.Setup(r => r.ListAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result ?? new PagedResult<CrewListItem>(0, []));

    [Fact]
    public async Task List_returns_paged_shape_and_item_json()
    {
        _repo.Setup(r => r.ListAsync("SHIP01", 2, 1, null, "asc", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CrewListItem>(3, [Soka]));

        var response = await _client.GetAsync("/api/ships/SHIP01/crew?page=2&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("page").GetInt32());
        Assert.Equal(1, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, body.GetProperty("totalPages").GetInt32());
        var item = body.GetProperty("items")[0];
        Assert.Equal("Master", item.GetProperty("rankName").GetString());
        Assert.Equal("CREW001", item.GetProperty("crewMemberId").GetString());
        Assert.Equal("Soka", item.GetProperty("firstName").GetString());
        Assert.Equal("Philip", item.GetProperty("lastName").GetString());
        Assert.Equal(46, item.GetProperty("age").GetInt32());
        Assert.Equal("Greek", item.GetProperty("nationality").GetString());
        Assert.Equal("2025-04-05", item.GetProperty("signOnDate").GetString());
        Assert.Equal("Relief Due", item.GetProperty("status").GetString());
        _repo.VerifyAll();
    }

    [Theory]
    [InlineData("RankName", "desc", "RankName")]
    [InlineData("rankName", "desc", "RankName")]
    [InlineData("SignOnDate", "asc", "SignOnDate")]
    [InlineData("age", "desc", "Age")]
    public async Task List_passes_sort_values_to_repo(string sortBy, string sortDir, string expectedBy)
    {
        SetupAny();

        var response = await _client.GetAsync($"/api/ships/SHIP01/crew?sortBy={sortBy}&sortDir={sortDir}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _repo.Verify(r => r.ListAsync("SHIP01", 1, 20, expectedBy, sortDir, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("?sortBy=salary")]
    [InlineData("?sortBy=99")]
    [InlineData("?sortDir=up")]
    [InlineData("?sortDir=DESC")]
    [InlineData("?pageSize=101")]
    [InlineData("?page=0")]
    public async Task List_with_invalid_query_returns_400(string query)
    {
        var response = await _client.GetAsync("/api/ships/SHIP01/crew" + query);

        await AssertValidationProblemAsync(response);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task List_with_invalid_sortDir_names_allowed_values()
    {
        var response = await _client.GetAsync("/api/ships/SHIP01/crew?sortDir=up");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SortDir must be either 'asc' or 'desc'.", body.GetProperty("errors").GetProperty("SortDir")[0].GetString());
    }

    [Fact]
    public async Task List_with_101_char_search_returns_400()
    {
        var response = await _client.GetAsync($"/api/ships/SHIP01/crew?search={new string('a', 101)}");

        await AssertValidationProblemAsync(response);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task List_with_blank_search_passes_null()
    {
        SetupAny();

        var response = await _client.GetAsync("/api/ships/SHIP01/crew?search=%20%20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _repo.Verify(r => r.ListAsync("SHIP01", 1, 20, null, "asc", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_trims_search()
    {
        SetupAny();

        await _client.GetAsync("/api/ships/SHIP01/crew?search=%20%2005%20Apr%20");

        _repo.Verify(r => r.ListAsync("SHIP01", 1, 20, null, "asc", "05 Apr", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_upper_cases_ship_code()
    {
        SetupAny();

        var response = await _client.GetAsync("/api/ships/ship01/crew");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _repo.Verify(r => r.ListAsync("SHIP01", 1, 20, null, "asc", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Ship not found.")]
    [InlineData("Ship is inactive.")]
    public async Task List_for_missing_or_inactive_ship_returns_404_problem(string message)
    {
        _repo.Setup(r => r.ListAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException(message, null));

        var response = await _client.GetAsync("/api/ships/SHIP99/crew");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task List_with_11_char_ship_code_returns_404_without_hitting_repo()
    {
        var response = await _client.GetAsync("/api/ships/ABCDEFGHIJK/crew");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Missing_api_key_returns_401()
    {
        var response = await _anonymous.GetAsync("/api/ships/SHIP01/crew");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public void DateOnly_serializes_as_iso_date() =>
        Assert.Equal("\"2025-04-05\"", JsonSerializer.Serialize(new DateOnly(2025, 4, 5)));

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").EnumerateObject().Any());
    }
}