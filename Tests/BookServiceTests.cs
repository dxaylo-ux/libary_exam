using Application.ApiResult;
using Application.DTOs;
using Application.Services;
using Application.Interfaces;
using Domain.Entities;
using Domain.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Tests.Helpers;

namespace Tests;

public class BookServiceTests
{
    private readonly Mock<IBookRepository> _books = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly FakeDateTimeProvider _clock = new();
    private readonly BookService _service;

    public BookServiceTests()
    {
        var cache = new BookCache(new MemoryCache(new MemoryCacheOptions()), NullLogger<BookCache>.Instance);
        _service = new BookService(_books.Object, _uow.Object, _clock, cache);
    }

    private static BookRequest ValidRequest(int totalCopies = 5) => new()
    {
        Title = "Clean Code",
        Author = "Robert Martin",
        Isbn = "978-0-13-235088-4",
        PublishedYear = 2008,
        TotalCopies = totalCopies
    };

    [Fact]
    public async Task Create_DuplicateIsbn_ReturnsConflict()
    {
        // Arrange
        _books.Setup(r => r.IsbnExistsAsync("9780132350884", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _service.CreateAsync(ValidRequest(), 7, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        _books.Verify(r => r.AddAsync(It.IsAny<Book>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_Success_SetsAvailableCopies()
    {
        // Arrange
        Book? added = null;
        _books.Setup(r => r.IsbnExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _books.Setup(r => r.AddAsync(It.IsAny<Book>(), It.IsAny<CancellationToken>()))
            .Callback<Book, CancellationToken>((b, _) => added = b)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateAsync(ValidRequest(5), 7, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(5, added!.AvailableCopies);
        Assert.Equal("9780132350884", added.Isbn);
        Assert.Equal(7, added.CreatedByUserId);
        Assert.Equal(_clock.UtcNow, added.CreatedAt);
        Assert.Equal(5, result.Data!.AvailableCopies);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_NotOwner_ReturnsForbidden()
    {
        // Arrange
        var book = new Book { Id = 1, CreatedByUserId = 10, TotalCopies = 5, AvailableCopies = 5 };
        _books.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(book);

        // Act
        var result = await _service.UpdateAsync(1, ValidRequest(), 99, "Librarian", CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_TotalBelowBorrowed_ReturnsValidation()
    {
        // Arrange: 5 всего, 2 свободно, значит 3 на руках
        var book = new Book { Id = 1, CreatedByUserId = 10, TotalCopies = 5, AvailableCopies = 2 };
        _books.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(book);

        // Act
        var result = await _service.UpdateAsync(1, ValidRequest(totalCopies: 2), 10, "Librarian", CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("price", null, null)]
    [InlineData("title", 2000, 1990)]
    public async Task GetBooks_InvalidParams_ReturnsValidation(string sortBy, int? minYear, int? maxYear)
    {
        // Arrange
        var filter = new BookFilter { SortBy = sortBy, MinYear = minYear, MaxYear = maxYear };

        // Act
        var result = await _service.GetBooksAsync(filter, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        _books.Verify(r => r.GetPagedAsync(It.IsAny<BookFilter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetById_SecondCall_UsesCache()
    {
        // Arrange
        var book = new Book { Id = 1, Title = "Clean Code", TotalCopies = 5, AvailableCopies = 5 };
        _books.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(book);

        // Act
        var first = await _service.GetByIdAsync(1, CancellationToken.None);
        var second = await _service.GetByIdAsync(1, CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        _books.Verify(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }
}
