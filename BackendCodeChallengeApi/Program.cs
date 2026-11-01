using System.Text.Json.Serialization;
using BackendCodeChallengeApi.Data.Repositories;
using BackendCodeChallengeApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
builder.Services.AddOptions<ApiKeyAuthenticationOptions>(ApiKeyAuthenticationHandler.SchemeName)
    .BindConfiguration("Auth")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "Auth:ApiKey must be configured.")
    .ValidateOnStart();
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddSingleton<IDbConnectionFactory>(sp => new SqlConnectionFactory(
    sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default must be configured.")));

builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IShipRepository, ShipRepository>();

builder.Services.AddOpenApi(o => o.AddDocumentTransformer<ApiKeySecuritySchemeTransformer>());

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi().AllowAnonymous();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Ship Management API v1"));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", async (IDbConnectionFactory db, CancellationToken ct) =>
{
    try
    {
        await using var connection = await db.OpenAsync(ct);
        return Results.Ok(new { status = "ok" });
    }
    catch (SqlException)
    {
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail: "Database unavailable.");
    }
}).AllowAnonymous();

app.Run();