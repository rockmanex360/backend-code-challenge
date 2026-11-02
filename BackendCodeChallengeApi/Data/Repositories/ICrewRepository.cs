using BackendCodeChallengeApi.Data.Models;

namespace BackendCodeChallengeApi.Data.Repositories;

public interface ICrewRepository
{
    Task<PagedResult<CrewListItem>> ListAsync(
        string shipCode, int pageNumber, int pageSize, string? sortBy, string sortDirection, string? search,
        CancellationToken cancellationToken);
}