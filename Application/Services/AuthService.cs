using Application.ApiResult;
using Application.DTOs;
using Application.Identity;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Application.Services;

public class AuthService(UserManager<User> userManager, IJwtTokenGenerator jwtTokenGenerator)
{
    private const string InvalidCredentials = "Неверный email или пароль";
    private const string LockedOut = "Аккаунт заблокирован на 5 минут из-за неверных попыток входа";

    public async Task<Result<UserDto>> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            request.BirthDate == default)
            return Result<UserDto>.Fail("Все поля обязательны для заполнения", ErrorType.Validation);

        if (request.FirstName.Length > 250 || request.LastName.Length > 250)
            return Result<UserDto>.Fail("Имя и фамилия не должны быть длиннее 250 символов", ErrorType.Validation);

        var email = request.Email.Trim();

        if (await userManager.FindByEmailAsync(email) != null)
            return Result<UserDto>.Fail("Email уже занят", ErrorType.Conflict);

        var user = new User
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            BirthDate = request.BirthDate
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            if (createResult.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
                return Result<UserDto>.Fail("Email уже занят", ErrorType.Conflict);

            return Result<UserDto>.Fail(
                string.Join("; ", createResult.Errors.Select(e => e.Description)),
                ErrorType.Validation);
        }

        var roleName = nameof(UserRole.Reader);
        var roleResult = await userManager.AddToRoleAsync(user, roleName);
        if (!roleResult.Succeeded)
            return Result<UserDto>.Fail(
                string.Join("; ", roleResult.Errors.Select(e => e.Description)),
                ErrorType.Validation);

        return Result<UserDto>.Success(new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            BirthDate = user.BirthDate,
            Role = roleName
        });
    }

    public async Task<Result<AuthResponseDto>> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Result<AuthResponseDto>.Fail("Email и пароль обязательны", ErrorType.Validation);

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user == null)
            return Result<AuthResponseDto>.Fail(InvalidCredentials, ErrorType.Unauthorized);

        if (await userManager.IsLockedOutAsync(user))
            return Result<AuthResponseDto>.Fail(LockedOut, ErrorType.Unauthorized);

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);

            if (await userManager.IsLockedOutAsync(user))
                return Result<AuthResponseDto>.Fail(LockedOut, ErrorType.Unauthorized);

            return Result<AuthResponseDto>.Fail(InvalidCredentials, ErrorType.Unauthorized);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? string.Empty;
        var token = jwtTokenGenerator.Generate(user, role);

        return Result<AuthResponseDto>.Success(new AuthResponseDto
        {
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                BirthDate = user.BirthDate,
                Role = role
            }
        });
    }

    public async Task<Result<UserDto>> GetMeAsync(int userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return Result<UserDto>.Fail("Пользователь не авторизован", ErrorType.Unauthorized);

        var roles = await userManager.GetRolesAsync(user);

        return Result<UserDto>.Success(new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            BirthDate = user.BirthDate,
            Role = roles.FirstOrDefault() ?? string.Empty
        });
    }
}
