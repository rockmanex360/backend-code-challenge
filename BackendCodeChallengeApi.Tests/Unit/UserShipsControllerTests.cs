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

public sealed class UserShipsControllerTests : IClassFixture<UserShipsControllerTests.Factory>
{
    private const string ValidKey = "test-key";

    private readonly Mock<IShipRepository> _repo = new();
    private readonly HttpClient _client;
    private readonly HttpClient _anonymousClient;

    public UserShipsControllerTests(Factory factory)
    {
        var custom = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IShipRepository>();
            s.AddSingleton(_repo.Object);
        }));
        _client = custom.CreateClient();
        _anonymousClient = custom.CreateClient();

        _client.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, ValidKey);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Auth:ApiKey", ValidKey);
            builder.UseSetting("ConnectionStrings:Default", "Server=127.0.0.1,1;Connect Timeout=1;Encrypt=False)");
        }
    }

    [Fact]
    public async Task Put_uppercases_code_and_returns_204()
    {
        _repo.Setup(r => r.AssignAsync(5, "SHIP01", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PutAsync("/api/users/5/ships/ship01", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        _repo.VerifyAll();
    }

    [Fact]
    public async Task Put_is_idempotent_at_the_api_level()
    {
        _repo.Setup(r => r.AssignAsync(5, "SHIP01", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsync("/api/users/5/ships/SHIP01", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsync("/api/users/5/ships/SHIP01", null)).StatusCode);
    }

    [Fact]
    public async Task Put_unknown_ship_or_user_returns_404_problem()
    {
        _repo.Setup(r => r.AssignAsync(5, "NOPE", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Ship 'NOPE' was not found.", null));

        var response = await _client.PutAsync("/api/users/5/ships/NOPE", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Delete_uppercases_code_and_returns_204()
    {
        _repo.Setup(r => r.UnassignAsync(5, "SHIP01", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var response = await _client.DeleteAsync("/api/users/5/ships/ship01");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        _repo.VerifyAll();
    }

    [Fact]
    public async Task Delete_unknown_user_returns_404_problem()
    {
        _repo.Setup(r => r.UnassignAsync(99, "SHIP01", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("User 99 was not found.", null));

        var response = await _client.DeleteAsync("/api/users/99/ships/SHIP01");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("PUT", "/api/users/0/ships/SHIP01")]
    [InlineData("PUT", "/api/users/5/ships/A")]
    [InlineData("DELETE", "/api/users/-1/ships/SHIP01")]
    [InlineData("DELETE", "/api/users/5/ships/ABCDEFGHIJK")]
    [InlineData("GET", "/api/users/0/ships")]
    public async Task Bad_route_values_return_404_without_hitting_repo(string method, string path)
    {
        var response = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _repo.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("?status=all", null)]
    [InlineData("?status=active", "Active")]
    [InlineData("?status=inactive", "Inactive")]
    public async Task List_returns_plain_array_and_maps_status(string query, string? expected)
    {
        _repo.Setup(r => r.ListByUserAsync(5, expected, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Ship("SHIP01", "Flying Dutchman", "0112", true), new Ship("SHIP05", "Queen Anne's Revenge", "0112", false)]);

        var response = await _client.GetAsync("/api/users/5/ships" + query);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Equal(2, body.GetArrayLength());
        Assert.Equal("SHIP05", body[1].GetProperty("code").GetString());
        Assert.False(body[1].GetProperty("isActive").GetBoolean());
        _repo.VerifyAll();
    }

    [Fact]
    public async Task List_with_invalid_status_returns_400()
    {
        var response = await _client.GetAsync("/api/users/5/ships?status=bogus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task List_unknown_user_returns_404_problem()
    {
        _repo.Setup(r => r.ListByUserAsync(99, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("User 99 was not found.", null));

        var response = await _client.GetAsync("/api/users/99/ships");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("GET", "/api/users/5/ships")]
    [InlineData("PUT", "/api/users/5/ships/SHIP01")]
    [InlineData("DELETE", "/api/users/5/ships/SHIP01")]
    public async Task Missing_api_key_returns_401(string method, string path)
    {
        var response = await _anonymousClient.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _repo.VerifyNoOtherCalls();
    }
}