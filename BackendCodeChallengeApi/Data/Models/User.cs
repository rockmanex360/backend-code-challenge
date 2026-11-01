namespace BackendCodeChallengeApi.Data.Models;

public sealed record User(int UserId, string Name, string RoleName, DateTime CreatedAtUtc);