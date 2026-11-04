using Application.DTOs;
using Application.Services;
using Domain.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Authorize]
public class BooksController(BookService bookService) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetBooks([FromQuery] BookFilter filter, CancellationToken ct) =>
        ToResponse(await bookService.GetBooksAsync(filter, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        ToResponse(await bookService.GetByIdAsync(id, ct));

    [Authorize(Roles = "Librarian,Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BookRequest request, CancellationToken ct) =>
        ToResponse(await bookService.CreateAsync(request, CurrentUserId, ct));

    [Authorize(Roles = "Librarian,Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] BookRequest request, CancellationToken ct) =>
        ToResponse(await bookService.UpdateAsync(id, request, CurrentUserId, CurrentUserRole, ct));
}
