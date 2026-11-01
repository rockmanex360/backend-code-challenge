namespace BackendCodeChallengeApi.Data.Models;

public sealed record PagedResult<T>(int TotalCount, IReadOnlyList<T> Items);