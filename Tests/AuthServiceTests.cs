using Application.ApiResult;
using Application.DTOs;
using Application.Identity;
using Application.Interfaces;
using Application.Services;
using Microsoft.AspNetCore.Identity;
using Moq;
using Tests.Helpers;

namespace Tests;

public class AuthServiceTests
{
    private readonly Mock<UserManager<User>> _userManager = UserManagerFactory.CreateUserManager();
    private readonly Mock<IJwtTokenGenerator> _jwt = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _service = new AuthService(_userManager.Object, _jwt.Object);
    }

    private static RegisterRequest ValidRequest() => new()
    {
        Email = "reader@mail.com",
        Password = "Password1",
        FirstName = "Ivan",
        LastName = "Ivanov",
        BirthDate = new DateOnly(2000, 1, 1)
    };

    [Fact]
    public async Task Register_EmailTaken_ReturnsConflict()
    {
        // Arrange
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new User { Id = 1, Email = "reader@mail.com" });

        // Act
        var result = await _service.RegisterAsync(ValidRequest());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        _userManager.Verify(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Register_WeakPassword_ReturnsValidation()
    {
        // Arrange
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = "Слишком короткий пароль" }));

        // Act
        var result = await _service.RegisterAsync(ValidRequest());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        _userManager.Verify(m => m.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Register_Success_AssignsReaderRole()
    {
        // Arrange
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.RegisterAsync(ValidRequest());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Reader", result.Data!.Role);
        _userManager.Verify(m => m.AddToRoleAsync(It.IsAny<User>(), "Reader"), Times.Once);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var user = new User { Id = 1, Email = "reader@mail.com" };
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        _userManager.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        _userManager.Setup(m => m.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(false);
        _userManager.Setup(m => m.AccessFailedAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.LoginAsync(new LoginRequest { Email = "reader@mail.com", Password = "wrong" });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
        _jwt.Verify(g => g.Generate(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }
}
