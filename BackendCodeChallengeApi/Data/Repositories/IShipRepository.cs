using System;
using BackendCodeChallengeApi.Data.Models;

namespace BackendCodeChallengeApi.Data.Repositories;

public interface IShipRepository
{
    Task<Ship> CreateAsync(string shipCode, string name, string fiscalYearCode, bool isActive, CancellationToken cancellationToken);
    Task<Ship> GetAsync(string shipCode, CancellationToken cancellationToken);
    Task<PagedResult<Ship>> ListAsync(string? status, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task AssignAsync(int userId, string shipCode, CancellationToken cancellationToken);
    Task UnassignAsync(int userId, string shipCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<Ship>> ListByUserAsync(int userId, string? status, CancellationToken cancellationToken);
}
