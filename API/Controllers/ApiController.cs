using System.Security.Claims;
using Application.ApiResult;
using Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiController : ControllerBase
{
    protected int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected string CurrentUserRole => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    protected IActionResult ToResponse<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Data);

        var body = new ErrorResponseDto { Error = result.ErrorMessage ?? string.Empty };

        return result.ErrorType switch
        {
            ErrorType.Validation => BadRequest(body),
            ErrorType.Unauthorized => Unauthorized(body),
            ErrorType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, body),
            ErrorType.NotFound => NotFound(body),
            ErrorType.Conflict => Conflict(body),
            _ => StatusCode(StatusCodes.Status500InternalServerError, body)
        };
    }
}
