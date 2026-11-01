using BackendCodeChallengeApi.Contracts;
using BackendCodeChallengeApi.Contracts.Users;
using BackendCodeChallengeApi.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace BackendCodeChallengeApi.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IUserRepository users, ILogger<UsersController> logger) : ControllerBase
{
    /// <summary>Creates a user with a role.</summary>
    /// <response code="201">User created.</response>
    /// <response code="400">Validation failed or the role does not exist.</response>
    /// <response code="401">Missing or invalid API key.</response>
    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        var user = await users.CreateAsync(request.Name!.Trim(), request.Role!.Trim(), ct);
        logger.LogInformation("User created {UserId}", user.UserId);
        return CreatedAtAction(nameof(GetUser), new { userId = user.UserId }, UserResponse.From(user));
    }

    /// <summary>Lists users, one page at a time.</summary>
    /// <response code="200">A page of users.</response>
    /// <response code="400">Invalid paging parameters.</response>
    /// <response code="401">Missing or invalid API key.</response>
    [HttpGet]
    [ProducesResponseType<PagedResponse<UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResponse<UserResponse>>> ListUsers([FromQuery] PageQuery query, CancellationToken ct)
    {
        var result = await users.ListAsync(query.Page, query.PageSize, ct);
        return PagedResponse<UserResponse>.Create(
            result.Items.Select(UserResponse.From).ToList(), query.Page, query.PageSize, result.TotalCount);
    }

    /// <summary>Gets one user by id.</summary>
    /// <response code="200">The user.</response>
    /// <response code="401">Missing or invalid API key.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("{userId:int:min(1)}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetUser(int userId, CancellationToken ct) =>
        UserResponse.From(await users.GetAsync(userId, ct));
}