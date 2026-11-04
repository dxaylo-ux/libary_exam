using Application.DTOs;
using Application.Services;
using Domain.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController(UserService userService) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] UserFilter filter, CancellationToken ct) =>
        ToResponse(await userService.GetUsersAsync(filter, ct));

    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleRequest request) =>
        ToResponse(await userService.ChangeRoleAsync(id, request));
}
