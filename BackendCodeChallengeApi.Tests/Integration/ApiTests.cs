using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BackendCodeChallengeApi.Contracts.Ships;
using BackendCodeChallengeApi.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BackendCodeChallengeApi.Tests.Integration;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ApiTests : IDisposable
{
    private readonly SqlServerFixture _db;
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ApiTests(SqlServerFixture db)
    {
        _db = db;
        _factory = new ApiFactory(db.ConnectionString);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, ApiFactory.Key);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        public const string Key = "integration-key";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Auth:ApiKey", Key);
            builder.UseSetting("ConnectionStrings:Default", connectionString);
        }
    }

    private static async Task<JsonElement> Problem(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Post_ship_then_get_round_trips()
    {
        var created = await _client.PostAsJsonAsync("/api/ships", new { code = "tapi1", name = "Api Ship", fiscalYearCode = "0403" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.EndsWith("/api/ships/TAPI1", created.Headers.Location!.AbsolutePath);

        var got = await _client.GetFromJsonAsync<ShipResponse>(created.Headers.Location);
        Assert.Equal(new ShipResponse("TAPI1", "Api Ship", "0403", true), got);
    }

    [Fact]
    public async Task T_A2_duplicate_ship_post_is_409()
    {
        var body = new { code = "TAPI2", name = "Api Dup", fiscalYearCode = "0112" };
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/ships", body)).StatusCode);

        var second = await _client.PostAsJsonAsync("/api/ships", body);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(409, (await Problem(second)).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Crew_list_with_bogus_sort_is_400_problem_details()
    {
        await _db.OnceAsync("TAPI3", () => _db.AddShipAsync("TAPI3"));
        var response = await _client.GetAsync("/api/ships/TAPI3/crew?sortBy=bogus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(400, (await Problem(response)).GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("detail")]
    [InlineData("summary")]
    public async Task T_F11_inactive_ship_report_is_404_mentioning_inactive(string report)
    {
        await _db.OnceAsync("TAPI4", () => _db.AddShipAsync("TAPI4", active: false));
        var response = await _client.GetAsync($"/api/ships/TAPI4/financial-reports/{report}?period=2025-07");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("inactive", (await Problem(response)).GetProperty("detail").GetString());
    }
}