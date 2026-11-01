using System;
using BackendCodeChallengeApi.Data.Models;

namespace BackendCodeChallengeApi.Data.Repositories;

public interface IUserRepository
{
    Task<User> CreateAsync(string name, string roleName, CancellationToken cancellationToken);
    Task<User> GetAsync(int userId, CancellationToken cancellationToken);
    Task<PagedResult<User>> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken);
}
