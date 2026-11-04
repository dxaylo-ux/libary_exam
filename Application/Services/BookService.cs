using Application.ApiResult;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Filters;

namespace Application.Services;

public class BookService(IBookRepository books, IUnitOfWork unitOfWork, IDateTimeProvider clock, IBookCache cache)
{
    private static readonly string[] SortFields = ["title", "author", "year"];

    public async Task<Result<BookDto>> CreateAsync(BookRequest request, int userId, CancellationToken ct = default)
    {
        var data = Normalize(request);

        var error = Validate(data);
        if (error != null)
            return Result<BookDto>.Fail(error, ErrorType.Validation);

        if (await books.IsbnExistsAsync(data.Isbn, null, ct))
            return Result<BookDto>.Fail("Книга с таким ISBN уже существует", ErrorType.Conflict);

        var book = new Book
        {
            Title = data.Title,
            Author = data.Author,
            Isbn = data.Isbn,
            PublishedYear = data.PublishedYear,
            TotalCopies = data.TotalCopies,
            AvailableCopies = data.TotalCopies,
            CreatedAt = clock.UtcNow,
            CreatedByUserId = userId
        };

        await books.AddAsync(book, ct);
        await unitOfWork.SaveChangesAsync(ct);
        cache.Invalidate();

        return Result<BookDto>.Success(new BookDto
        {
            Id = book.Id,
            Title = book.Title,
            Author = book.Author,
            Isbn = book.Isbn,
            PublishedYear = book.PublishedYear,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.AvailableCopies,
            CreatedAt = book.CreatedAt
        });
    }

    public async Task<Result<BookDto>> UpdateAsync(int id, BookRequest request, int userId, string role, CancellationToken ct = default)
    {
        var book = await books.GetByIdAsync(id, ct);
        if (book == null)
            return Result<BookDto>.Fail("Книга не найдена", ErrorType.NotFound);

        if (role != nameof(UserRole.Admin) && book.CreatedByUserId != userId)
            return Result<BookDto>.Fail("Изменять книгу может только её создатель или администратор", ErrorType.Forbidden);

        var data = Normalize(request);

        var error = Validate(data);
        if (error != null)
            return Result<BookDto>.Fail(error, ErrorType.Validation);

        var borrowed = book.TotalCopies - book.AvailableCopies;
        if (data.TotalCopies < borrowed)
            return Result<BookDto>.Fail(
                $"TotalCopies не может быть меньше числа экземпляров на руках ({borrowed})",
                ErrorType.Validation);

        if (await books.IsbnExistsAsync(data.Isbn, id, ct))
            return Result<BookDto>.Fail("Книга с таким ISBN уже существует", ErrorType.Conflict);

        book.Title = data.Title;
        book.Author = data.Author;
        book.Isbn = data.Isbn;
        book.PublishedYear = data.PublishedYear;
        book.TotalCopies = data.TotalCopies;
        book.AvailableCopies = data.TotalCopies - borrowed;

        await unitOfWork.SaveChangesAsync(ct);
        cache.Invalidate();

        return Result<BookDto>.Success(new BookDto
        {
            Id = book.Id,
            Title = book.Title,
            Author = book.Author,
            Isbn = book.Isbn,
            PublishedYear = book.PublishedYear,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.AvailableCopies,
            CreatedAt = book.CreatedAt
        });
    }

    public async Task<Result<BookDto>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var key = cache.BookKey(id);
        if (cache.TryGet<BookDto>(key, out var cached) && cached != null)
            return Result<BookDto>.Success(cached);

        var book = await books.GetByIdAsync(id, ct);
        if (book == null)
            return Result<BookDto>.Fail("Книга не найдена", ErrorType.NotFound);

        var dto = new BookDto
        {
            Id = book.Id,
            Title = book.Title,
            Author = book.Author,
            Isbn = book.Isbn,
            PublishedYear = book.PublishedYear,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.AvailableCopies,
            CreatedAt = book.CreatedAt
        };

        cache.Set(key, dto);
        return Result<BookDto>.Success(dto);
    }

    public async Task<Result<PagedResult<BookDto>>> GetBooksAsync(BookFilter filter, CancellationToken ct = default)
    {
        filter.SortBy = string.IsNullOrWhiteSpace(filter.SortBy) ? "title" : filter.SortBy.Trim().ToLowerInvariant();

        if (!SortFields.Contains(filter.SortBy))
            return Result<PagedResult<BookDto>>.Fail("Неизвестное поле сортировки. Допустимо: title, author, year", ErrorType.Validation);

        if (filter.MinYear.HasValue && filter.MaxYear.HasValue && filter.MinYear > filter.MaxYear)
            return Result<PagedResult<BookDto>>.Fail("minYear не может быть больше maxYear", ErrorType.Validation);

        if (filter.Page < 1) filter.Page = 1;
        if (filter.PageSize > 100) filter.PageSize = 100;
        if (filter.PageSize < 1) filter.PageSize = 10;
        filter.Search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();

        var key = cache.ListKey(filter);
        if (cache.TryGet<PagedResult<BookDto>>(key, out var cached) && cached != null)
            return Result<PagedResult<BookDto>>.Success(cached);

        var page = await books.GetPagedAsync(filter, ct);

        var dto = new PagedResult<BookDto>
        {
            Items = page.Items.Select(book => new BookDto
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                Isbn = book.Isbn,
                PublishedYear = book.PublishedYear,
                TotalCopies = book.TotalCopies,
                AvailableCopies = book.AvailableCopies,
                CreatedAt = book.CreatedAt
            }).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        };

        cache.Set(key, dto);
        return Result<PagedResult<BookDto>>.Success(dto);
    }

    private static BookRequest Normalize(BookRequest request) => new()
    {
        Title = (request.Title ?? string.Empty).Trim(),
        Author = (request.Author ?? string.Empty).Trim(),
        Isbn = (request.Isbn ?? string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty),
        PublishedYear = request.PublishedYear,
        TotalCopies = request.TotalCopies
    };

    private string? Validate(BookRequest data)
    {
        if (data.Title.Length < 2 || data.Title.Length > 200)
            return "Название должно содержать от 2 до 200 символов";

        if (data.Author.Length < 2 || data.Author.Length > 150)
            return "Автор должен содержать от 2 до 150 символов";

        if ((data.Isbn.Length != 10 && data.Isbn.Length != 13) || !data.Isbn.All(char.IsAsciiDigit))
            return "ISBN должен состоять из 10 или 13 цифр";

        if (data.PublishedYear < 1450 || data.PublishedYear > clock.UtcNow.Year)
            return $"Год издания должен быть от 1450 до {clock.UtcNow.Year}";

        if (data.TotalCopies < 1 || data.TotalCopies > 1000)
            return "Количество экземпляров должно быть от 1 до 1000";

        return null;
    }
}
