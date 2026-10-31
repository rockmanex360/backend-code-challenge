using System.Text.Json;
using BackendCodeChallengeApi.Data.Errors;
using BackendCodeChallengeApi.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BackendCodeChallengeApi.Tests.Unit;

public sealed class GlobalExceptionHandlerTests
{
    [Theory]
    [InlineData(typeof(BadRequestException), 400)]
    [InlineData(typeof(NotFoundException), 404)]
    [InlineData(typeof(ConflictException), 409)]
    public async Task Domain_exception_maps_to_status_and_keeps_message(Type exceptionType, int expectedStatus)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Ship not found.", null)!;

        var (status, body) = await HandleAsync(exception);

        Assert.Equal(expectedStatus, status);
        Assert.Equal("Ship not found.", body.GetProperty("detail").GetString());
        Assert.True(body.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Unknown_exception_returns_500_without_leaking_message()
    {
        var (status, body) = await HandleAsync(new InvalidOperationException("Login failed for user 'sa'."));

        Assert.Equal(500, status);
        Assert.Equal("An unexpected error occurred.", body.GetProperty("detail").GetString());
        Assert.DoesNotContain("Login failed", body.GetRawText());
    }

    private static async Task<(int Status, JsonElement Body)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(),
            NullLogger<GlobalExceptionHandler>.Instance);

        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));

        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, json.RootElement.Clone());
    }
}