using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Authorize]
public class LoansController(LoanService loanService) : ApiController
{
    [HttpPost]
    public async Task<IActionResult> Borrow([FromBody] BorrowRequest request, CancellationToken ct) =>
        ToResponse(await loanService.BorrowAsync(request.BookId, CurrentUserId, ct));

    [HttpGet("my")]
    public async Task<IActionResult> GetMy(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default) =>
        ToResponse(await loanService.GetMyAsync(CurrentUserId, status, page, pageSize, ct));

    [HttpPost("{id:int}/return")]
    public async Task<IActionResult> Return(int id, CancellationToken ct) =>
        ToResponse(await loanService.ReturnAsync(id, CurrentUserId, CurrentUserRole, ct));

    [Authorize(Roles = "Librarian,Admin")]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status, [FromQuery] int? readerId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default) =>
        ToResponse(await loanService.GetAllAsync(status, readerId, page, pageSize, ct));
}
