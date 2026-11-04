using Application.Identity;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace Tests.Helpers;

public static class UserManagerFactory
{
    public static Mock<UserManager<User>> CreateUserManager() =>
        new(new Mock<IUserStore<User>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
}
