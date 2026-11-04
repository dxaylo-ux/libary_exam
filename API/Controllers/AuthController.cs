using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class AuthController(AuthService authService) : ApiController
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request) =>
        ToResponse(await authService.RegisterAsync(request));

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request) =>
        ToResponse(await authService.LoginAsync(request));

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me() =>
        ToResponse(await authService.GetMeAsync(CurrentUserId));
}
