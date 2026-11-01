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

public sealed class UsersControllerTests : IClassFixture<UsersControllerTests.Factory>
{
    private const string ValidKey = "test-key";
    private static readonly DateTime Created = new(2025, 7, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly Mock<IUserRepository> _repo = new();
    private readonly HttpClient _client;
    private readonly HttpClient _anonymous;

    public UsersControllerTests(Factory factory)
    {
        var customized = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IUserRepository>();
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
    public async Task Post_creates_user_with_trimmed_values_and_location()
    {
        _repo.Setup(r => r.CreateAsync("TestAdmin", "Admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User(7, "TestAdmin", "Admin", Created));

        var response = await _client.PostAsJsonAsync("/api/users", new { name = "  TestAdmin  ", role = " Admin " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith("/api/users/7", response.Headers.Location!.AbsolutePath);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(7, body.GetProperty("userId").GetInt32());
        Assert.Equal("Admin", body.GetProperty("role").GetString());
        _repo.VerifyAll();
    }

    [Theory]
    [InlineData("""{"name":"   ","role":"Admin"}""")]
    [InlineData("""{"name":"","role":"Admin"}""")]
    [InlineData("""{"role":"Admin"}""")]
    [InlineData("""{"name":"TestAdmin"}""")]
    [InlineData("""{"name":"TestAdmin","role":"  "}""")]
    public async Task Post_with_invalid_body_returns_400_validation_problem(string json)
    {
        var response = await PostRawAsync(json);

        await AssertValidationProblemAsync(response);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Post_with_101_char_name_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/users", new { name = new string('a', 101), role = "Admin" });

        await AssertValidationProblemAsync(response);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Post_with_unknown_role_returns_400_from_domain_error()
    {
        _repo.Setup(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Unknown role.", null));

        var response = await _client.PostAsJsonAsync("/api/users", new { name = "TestAdmin", role = "Nope" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_returns_user()
    {
        _repo.Setup(r => r.GetAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(new User(3, "Soka", "Crew", Created));

        var response = await _client.GetAsync("/api/users/3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Soka", body.GetProperty("name").GetString());
        Assert.EndsWith("Z", body.GetProperty("createdAtUtc").GetString());
    }

    [Fact]
    public async Task Get_missing_user_returns_404_problem()
    {
        _repo.Setup(r => r.GetAsync(99, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("User not found.", null));

        var response = await _client.GetAsync("/api/users/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/api/users/0")]
    [InlineData("/api/users/-1")]
    [InlineData("/api/users/abc")]
    public async Task Get_with_non_positive_id_returns_404_without_hitting_repo(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task List_returns_paged_shape()
    {
        _repo.Setup(r => r.ListAsync(2, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<User>(5, [new User(3, "A", "Crew", Created), new User(4, "B", "Admin", Created)]));

        var response = await _client.GetAsync("/api/users?page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
        Assert.Equal(2, body.GetProperty("page").GetInt32());
        Assert.Equal(2, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(5, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, body.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task List_defaults_to_page_1_size_20()
    {
        _repo.Setup(r => r.ListAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<User>(0, []));

        var response = await _client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _repo.VerifyAll();
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    public async Task List_with_bad_paging_returns_400(string query)
    {
        var response = await _client.GetAsync("/api/users" + query);

        await AssertValidationProblemAsync(response);
        _repo.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("GET", "/api/users")]
    [InlineData("GET", "/api/users/1")]
    [InlineData("POST", "/api/users")]
    public async Task Missing_api_key_returns_401(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
            request.Content = JsonContent.Create(new { name = "x", role = "Admin" });

        var response = await _anonymous.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _repo.VerifyNoOtherCalls();
    }

    private Task<HttpResponseMessage> PostRawAsync(string json) =>
        _client.PostAsync("/api/users", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").EnumerateObject().Any());
    }
}