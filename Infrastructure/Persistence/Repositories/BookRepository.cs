using Application.ApiResult;
using Application.Interfaces;
using Domain.Entities;
using Domain.Filters;
using Infrastructure.Persistence.Data;
using Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class BookRepository(AppDbContext db) : IBookRepository
{
    public Task<Book?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Books.FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<bool> IsbnExistsAsync(string isbn, int? excludeBookId, CancellationToken ct = default) =>
        db.Books.AsNoTracking().AnyAsync(
            b => b.Isbn == isbn && (excludeBookId == null || b.Id != excludeBookId), ct);

    public async Task<PagedResult<Book>> GetPagedAsync(BookFilter filter, CancellationToken ct = default)
    {
        var query = db.Books.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(b => b.Title.ToLower().Contains(search) || b.Author.ToLower().Contains(search));
        }

        if (filter.OnlyAvailable)
            query = query.Where(b => b.AvailableCopies > 0);

        if (filter.MinYear.HasValue)
            query = query.Where(b => b.PublishedYear >= filter.MinYear.Value);

        if (filter.MaxYear.HasValue)
            query = query.Where(b => b.PublishedYear <= filter.MaxYear.Value);

        IOrderedQueryable<Book> ordered = (filter.SortBy, filter.Desc) switch
        {
            ("author", false) => query.OrderBy(b => b.Author),
            ("author", true) => query.OrderByDescending(b => b.Author),
            ("year", false) => query.OrderBy(b => b.PublishedYear),
            ("year", true) => query.OrderByDescending(b => b.PublishedYear),
            (_, true) => query.OrderByDescending(b => b.Title),
            _ => query.OrderBy(b => b.Title)
        };

        return await ordered.ThenBy(b => b.Id).ToPagedListAsync(filter.Page, filter.PageSize, ct);
    }

    public async Task AddAsync(Book book, CancellationToken ct = default) =>
        await db.Books.AddAsync(book, ct);
}
