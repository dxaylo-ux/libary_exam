using Application.ApiResult;
using Domain.Entities;
using Domain.Filters;

namespace Application.Interfaces;

public interface IBookRepository
{
    Task<Book?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> IsbnExistsAsync(string isbn, int? excludeBookId, CancellationToken ct = default);
    Task<PagedResult<Book>> GetPagedAsync(BookFilter filter, CancellationToken ct = default);
    Task AddAsync(Book book, CancellationToken ct = default);
}
