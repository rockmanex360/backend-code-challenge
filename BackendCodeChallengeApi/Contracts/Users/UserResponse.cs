using BackendCodeChallengeApi.Data.Models;

namespace BackendCodeChallengeApi.Contracts.Users;

public sealed record UserResponse(int UserId, string Name, string Role, DateTime CreatedAtUtc)
{
    public static UserResponse From(User u) =>
        new(u.UserId, u.Name, u.RoleName, DateTime.SpecifyKind(u.CreatedAtUtc, DateTimeKind.Utc));
}