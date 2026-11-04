using Application.ApiResult;
using Application.Identity;
using Domain.Filters;

namespace Application.Interfaces;

public interface IUserRepository
{
    Task<PagedResult<User>> GetPagedAsync(UserFilter filter, CancellationToken ct = default);
}
