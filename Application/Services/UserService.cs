using Application.ApiResult;
using Application.DTOs;
using Application.Identity;
using Application.Interfaces;
using Domain.Enums;
using Domain.Filters;
using Microsoft.AspNetCore.Identity;

namespace Application.Services;

public class UserService(UserManager<User> userManager, IUserRepository users)
{
    public async Task<Result<PagedResult<UserDto>>> GetUsersAsync(UserFilter filter, CancellationToken ct = default)
    {
        if (filter.Page < 1) filter.Page = 1;
        if (filter.PageSize > 100) filter.PageSize = 100;
        if (filter.PageSize < 1) filter.PageSize = 10;
        filter.Search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();

        var page = await users.GetPagedAsync(filter, ct);

        var items = new List<UserDto>();
        foreach (var user in page.Items)
        {
            var roles = await userManager.GetRolesAsync(user);
            items.Add(new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                BirthDate = user.BirthDate,
                Role = roles.FirstOrDefault() ?? string.Empty
            });
        }

        return Result<PagedResult<UserDto>>.Success(new PagedResult<UserDto>
        {
            Items = items,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        });
    }

    public async Task<Result<UserDto>> ChangeRoleAsync(int id, ChangeRoleRequest request)
    {
        UserRole newRole;
        if (string.Equals(request.Role, nameof(UserRole.Librarian), StringComparison.OrdinalIgnoreCase))
            newRole = UserRole.Librarian;
        else if (string.Equals(request.Role, nameof(UserRole.Reader), StringComparison.OrdinalIgnoreCase))
            newRole = UserRole.Reader;
        else
            return Result<UserDto>.Fail("Назначить можно только роль Librarian или Reader", ErrorType.Validation);

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null)
            return Result<UserDto>.Fail("Пользователь не найден", ErrorType.NotFound);

        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Contains(nameof(UserRole.Admin)))
            return Result<UserDto>.Fail("Роль администратора менять нельзя", ErrorType.Validation);

        var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
            return Result<UserDto>.Fail(
                string.Join("; ", removeResult.Errors.Select(e => e.Description)),
                ErrorType.Validation);

        var addResult = await userManager.AddToRoleAsync(user, newRole.ToString());
        if (!addResult.Succeeded)
            return Result<UserDto>.Fail(
                string.Join("; ", addResult.Errors.Select(e => e.Description)),
                ErrorType.Validation);

        return Result<UserDto>.Success(new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            BirthDate = user.BirthDate,
            Role = newRole.ToString()
        });
    }
}
