using Application.ApiResult;
using Application.DTOs;
using Application.Identity;
using Application.Interfaces;
using Application.Services;
using Microsoft.AspNetCore.Identity;
using Moq;
using Tests.Helpers;

namespace Tests;

public class UserServiceTests
{
    private readonly Mock<UserManager<User>> _userManager = UserManagerFactory.CreateUserManager();
    private readonly Mock<IUserRepository> _users = new();
    private readonly UserService _service;

    public UserServiceTests()
    {
        _service = new UserService(_userManager.Object, _users.Object);
    }

    [Fact]
    public async Task ChangeRole_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        _userManager.Setup(m => m.FindByIdAsync("5")).ReturnsAsync((User?)null);

        // Act
        var result = await _service.ChangeRoleAsync(5, new ChangeRoleRequest { Role = "Librarian" });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        _userManager.Verify(m => m.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangeRole_Success_ReplacesRole()
    {
        // Arrange
        var user = new User { Id = 5, Email = "reader@mail.com" };
        IList<string> roles = new List<string> { "Reader" };
        _userManager.Setup(m => m.FindByIdAsync("5")).ReturnsAsync(user);
        _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(roles);
        _userManager.Setup(m => m.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.AddToRoleAsync(user, "Librarian")).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.ChangeRoleAsync(5, new ChangeRoleRequest { Role = "Librarian" });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Librarian", result.Data!.Role);
        _userManager.Verify(m => m.RemoveFromRolesAsync(user, It.Is<IEnumerable<string>>(r => r.Contains("Reader"))), Times.Once);
        _userManager.Verify(m => m.AddToRoleAsync(user, "Librarian"), Times.Once);
    }
}
