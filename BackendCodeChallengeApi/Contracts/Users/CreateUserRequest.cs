using System.ComponentModel.DataAnnotations;

namespace BackendCodeChallengeApi.Contracts.Users;

public sealed record CreateUserRequest
{
    [Required, StringLength(100)]
    public string? Name { get; init; }

    [Required, StringLength(30)]
    public string? Role { get; init; }
}