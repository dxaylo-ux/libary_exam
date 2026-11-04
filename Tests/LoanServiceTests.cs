using Application.ApiResult;
using Application.Identity;
using Application.Interfaces;
using Application.Services;
using Application.Settings;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Tests.Helpers;

namespace Tests;

public class LoanServiceTests
{
    private readonly Mock<ILoanRepository> _loans = new();
    private readonly Mock<IBookRepository> _books = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<UserManager<User>> _userManager = UserManagerFactory.CreateUserManager();
    private readonly FakeDateTimeProvider _clock = new();
    private readonly LoanSettings _settings = new() { LoanDays = 14, MaxActiveLoans = 3, FinePerDay = 0.50m };
    private readonly LoanService _service;

    public LoanServiceTests()
    {
        var cache = new BookCache(new MemoryCache(new MemoryCacheOptions()), NullLogger<BookCache>.Instance);

        _loans.Setup(r => r.GetUnreturnedByReaderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Loan>());

        _service = new LoanService(
            _loans.Object, _books.Object, _uow.Object, _clock, _email.Object,
            _userManager.Object, cache, Options.Create(_settings), NullLogger<LoanService>.Instance);
    }

    private static Book NewBook(int id = 1, int available = 2) => new()
    {
        Id = id,
        Title = "Clean Code",
        Author = "Robert Martin",
        TotalCopies = 5,
        AvailableCopies = available
    };

    private static Loan NewLoan(int id, int bookId, LoanStatus status, DateTime dueAt, int readerId = 1) => new()
    {
        Id = id,
        BookId = bookId,
        ReaderId = readerId,
        Status = status,
        BorrowedAt = dueAt.AddDays(-14),
        DueAt = dueAt,
        Book = new Book { Id = bookId, Title = "Book " + bookId, TotalCopies = 5, AvailableCopies = 1 }
    };

    private void SetupBook(int id, Book? book) =>
        _books.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(book);

    private void SetupUnreturned(int readerId, params Loan[] loans) =>
        _loans.Setup(r => r.GetUnreturnedByReaderAsync(readerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loans.ToList());

    [Fact]
    public async Task Borrow_BookNotFound_ReturnsNotFound()
    {
        // Arrange
        SetupBook(1, null);

        // Act
        var result = await _service.BorrowAsync(1, 1, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(LoanStatus.Overdue)]
    [InlineData(LoanStatus.Active)]
    public async Task Borrow_ReaderHasOverdue_ReturnsConflict(LoanStatus status)
    {
        // Arrange: срок вышел вчера; для Active фоновый сервис ещё не пометил выдачу
        SetupBook(1, NewBook());
        SetupUnreturned(1, NewLoan(10, 2, status, _clock.UtcNow.AddDays(-1)));

        // Act
        var result = await _service.BorrowAsync(1, 1, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Borrow_LimitReached_ReturnsConflict()
    {
        // Arrange
        var due = _clock.UtcNow.AddDays(5);
        SetupBook(1, NewBook());
        SetupUnreturned(1,
            NewLoan(10, 11, LoanStatus.Active, due),
            NewLoan(11, 12, LoanStatus.Active, due),
            NewLoan(12, 13, LoanStatus.Active, due));

        // Act
        var result = await _service.BorrowAsync(1, 1, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Borrow_SameBookAlreadyBorrowed_ReturnsConflict()
    {
        // Arrange
        SetupBook(1, NewBook());
        SetupUnreturned(1, NewLoan(10, 1, LoanStatus.Active, _clock.UtcNow.AddDays(5)));

        // Act
        var result = await _service.BorrowAsync(1, 1, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Borrow_NoCopies_ReturnsConflict()
    {
        // Arrange
        SetupBook(1, NewBook(available: 0));

        // Act
        var result = await _service.BorrowAsync(1, 1, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Borrow_Success_CreatesActiveLoan()
    {
        // Arrange
        var book = NewBook(available: 2);
        Loan? added = null;
        SetupBook(1, book);
        _loans.Setup(r => r.AddAsync(It.IsAny<Loan>(), It.IsAny<CancellationToken>()))
            .Callback<Loan, CancellationToken>((l, _) => added = l)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.BorrowAsync(1, 42, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(LoanStatus.Active, added!.Status);
        Assert.Equal(42, added.ReaderId);
        Assert.Equal(1, added.BookId);
        Assert.Equal(_clock.UtcNow, added.BorrowedAt);
        Assert.Equal(_clock.UtcNow.AddDays(_settings.LoanDays), added.DueAt);
        Assert.Equal(1, book.AvailableCopies);
        Assert.Equal("Active", result.Data!.Status);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Borrow_EmailFails_StillSucceeds()
    {
        // Arrange
        SetupBook(1, NewBook());
        _loans.Setup(r => r.AddAsync(It.IsAny<Loan>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _userManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(new User { Id = 1, Email = "reader@mail.com" });
        _email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP недоступен"));

        // Act
        var result = await _service.BorrowAsync(1, 1, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Return_ByStranger_ReturnsForbidden()
    {
        // Arrange: выдача принадлежит читателю 1, возвращает читатель 2
        var loan = NewLoan(10, 1, LoanStatus.Active, _clock.UtcNow.AddDays(5), readerId: 1);
        _loans.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(loan);

        // Act
        var result = await _service.ReturnAsync(10, 2, "Reader", CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.Equal(LoanStatus.Active, loan.Status);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Return_Overdue_CalculatesFine()
    {
        // Arrange: просрочка ровно 3 дня, свободных экземпляров 1
        var loan = NewLoan(10, 1, LoanStatus.Overdue, _clock.UtcNow.AddDays(-3), readerId: 1);
        loan.Book.AvailableCopies = 1;
        _loans.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(loan);

        // Act
        var result = await _service.ReturnAsync(10, 1, "Reader", CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(LoanStatus.Returned, loan.Status);
        Assert.Equal(_clock.UtcNow, loan.ReturnedAt);
        Assert.Equal(3 * _settings.FinePerDay, loan.Fine);
        Assert.Equal(3 * _settings.FinePerDay, result.Data!.Fine);
        Assert.Equal(2, loan.Book.AvailableCopies);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
