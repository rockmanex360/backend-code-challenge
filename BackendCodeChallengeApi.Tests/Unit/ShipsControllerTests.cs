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

public sealed class ShipsControllerTests : IClassFixture<ShipsControllerTests.Factory>
{
    private const string ValidKey = "test-key";

    private readonly Mock<IShipRepository> _repo = new();
    private readonly HttpClient _client;
    private readonly HttpClient _anonymous;

    public ShipsControllerTests(Factory factory)
    {
        var customized = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IShipRepository>();
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

    [Fact]
    public async Task Post_creates_ship_with_uppercased_code_and_location()
    {
        _repo.Setup(r => r.CreateAsync("SHIP9", "Nautilus", "0403", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ship("SHIP9", "Nautilus", "0403", true));

        var response = await _client.PostAsJsonAsync("/api/ships",
            new { code = "ship9", name = "  Nautilus ", fiscalYearCode = "0403" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith("/api/ships/SHIP9", response.Headers.Location!.AbsolutePath);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SHIP9", body.GetProperty("code").GetString());
        Assert.Equal("0403", body.GetProperty("fiscalYearCode").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());
        _repo.VerifyAll();
    }

    [Fact]
    public async Task Post_passes_explicit_inactive_flag()
    {
        _repo.Setup(r => r.CreateAsync("SHIP9", "N", "0112", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ship("SHIP9", "N", "0112", false));

        var response = await _client.PostAsJsonAsync("/api/ships",
            new { code = "SHIP9", name = "N", fiscalYearCode = "0112", isActive = false });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        _repo.VerifyAll();
    }

    [Theory]
    [InlineData("""{"name":"N","fiscalYearCode":"0112"}""")]
    [InlineData("""{"code":"A","name":"N","fiscalYearCode":"0112"}""")]
    [InlineData("""{"code":"ABCDEFGHIJK","name":"N","fiscalYearCode":"0112"}""")]
    [InlineData("""{"code":"SH-1","name":"N","fiscalYearCode":"0112"}""")]
    [InlineData("""{"code":"SHIP1","name":"  ","fiscalYearCode":"0112"}""")]
    [InlineData("""{"code":"SHIP1","fiscalYearCode":"0112"}""")]
    [InlineData("""{"code":"SHIP1","name":"N"}""")]
    [InlineData("""{"code":"SHIP1","name":"N","fiscalYearCode":"0412"}""")]
    [InlineData("""{"code":"SHIP1","name":"N","fiscalYearCode":"00ab"}""")]
    [InlineData("""{"code":"SHIP1","name":"N","fiscalYearCode":"1301"}""")]
    public async Task Post_with_invalid_body_returns_400_validation_problem(string json)
    {
        var response = await _client.PostAsync("/api/ships",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        await AssertValidationProblemAsync(response);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Post_with_101_char_name_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/ships",
            new { code = "SHIP1", name = new string('a', 101), fiscalYearCode = "0112" });

        await AssertValidationProblemAsync(response);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Post_duplicate_returns_409_problem()
    {
        _repo.Setup(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Ship 'SHIP1' already exists.", null));

        var response = await _client.PostAsJsonAsync("/api/ships", new { code = "SHIP1", name = "N", fiscalYearCode = "0112" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_with_unknown_valid_format_fiscal_year_returns_400_from_domain_error()
    {
        _repo.Setup(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Unknown fiscal year code.", null));

        var response = await _client.PostAsJsonAsync("/api/ships", new { code = "SHIP1", name = "N", fiscalYearCode = "0112" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_uppercases_code_and_returns_ship()
    {
        _repo.Setup(r => r.GetAsync("SHIP01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ship("SHIP01", "Flying Dutchman", "0112", true));

        var response = await _client.GetAsync("/api/ships/ship01");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Flying Dutchman", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Get_missing_ship_returns_404_problem()
    {
        _repo.Setup(r => r.GetAsync("NOPE", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Ship 'NOPE' was not found.", null));

        var response = await _client.GetAsync("/api/ships/NOPE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/api/ships/A")]
    [InlineData("/api/ships/ABCDEFGHIJK")]
    [InlineData("/api/ships/SH-1")]
    public async Task Get_with_malformed_code_returns_404_without_hitting_repo(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _repo.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("?status=all", null)]
    [InlineData("?status=active", "Active")]
    [InlineData("?status=inactive", "Inactive")]
    public async Task List_maps_status_to_sp_value(string query, string? expected)
    {
        _repo.Setup(r => r.ListAsync(expected, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Ship>(0, []));

        var response = await _client.GetAsync("/api/ships" + query);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _repo.VerifyAll();
    }

    [Theory]
    [InlineData("?status=bogus")]
    [InlineData("?status=Active")]
    [InlineData("?status=")]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    public async Task List_with_bad_query_returns_400(string query)
    {
        var response = await _client.GetAsync("/api/ships" + query);

        await AssertValidationProblemAsync(response);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task List_returns_paged_shape()
    {
        _repo.Setup(r => r.ListAsync("Active", 2, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Ship>(5, [new Ship("SHIP03", "Black Pearl", "0112", true), new Ship("SHIP04", "Going Merry", "0403", true)]));

        var response = await _client.GetAsync("/api/ships?status=active&page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
        Assert.Equal("SHIP03", body.GetProperty("items")[0].GetProperty("code").GetString());
        Assert.Equal(2, body.GetProperty("page").GetInt32());
        Assert.Equal(2, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(5, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, body.GetProperty("totalPages").GetInt32());
    }

    [Theory]
    [InlineData("GET", "/api/ships")]
    [InlineData("GET", "/api/ships/SHIP01")]
    [InlineData("POST", "/api/ships")]
    public async Task Missing_api_key_returns_401(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
            request.Content = JsonContent.Create(new { code = "SHIP1", name = "x", fiscalYearCode = "0112" });

        var response = await _anonymous.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _repo.VerifyNoOtherCalls();
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").EnumerateObject().Any());
    }
}