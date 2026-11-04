namespace Application.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
    public string Role { get; set; } = string.Empty;
}

public class ChangeRoleRequest
{
    public string Role { get; set; } = string.Empty;
}
