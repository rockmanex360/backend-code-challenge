using System.Net;
using BackendCodeChallengeApi.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BackendCodeChallengeApi.Tests.Unit;

public sealed class ApiKeyAuthTests(ApiKeyAuthTests.Factory factory) : IClassFixture<ApiKeyAuthTests.Factory>
{
    private const string ValidKey = "test-key";
    private readonly HttpClient _client = factory.CreateClient();

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Auth:ApiKey", ValidKey);
            // Unreachable on purpose: these tests must never need a database.
            builder.UseSetting("ConnectionStrings:Default", "Server=127.0.0.1,1;Connect Timeout=1;Encrypt=False");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    public async Task Api_without_valid_key_returns_401_problem(string? key)
    {
        var response = await SendAsync("/api/anything", key);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Api_with_valid_key_passes_auth()
    {
        var response = await SendAsync("/api/anything", ValidKey);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OpenApi_document_is_public()
    {
        var response = await SendAsync("/openapi/v1.json", key: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_is_public_and_reports_503_when_db_is_down()
    {
        var response = await SendAsync("/health", key: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendAsync(string path, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (key is not null)
            request.Headers.Add(ApiKeyAuthenticationHandler.HeaderName, key);
        return await _client.SendAsync(request);
    }
}