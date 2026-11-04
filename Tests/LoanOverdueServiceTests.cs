using Application.Identity;
using Application.Interfaces;
using Application.Services;
using Application.Settings;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Tests.Helpers;

namespace Tests;

public class LoanOverdueServiceTests
{
    private readonly Mock<ILoanRepository> _loans = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<UserManager<User>> _userManager = UserManagerFactory.CreateUserManager();
    private readonly FakeDateTimeProvider _clock = new();
    private readonly LoanOverdueService _service;

    public LoanOverdueServiceTests()
    {
        _userManager.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => (User?)new User { Id = int.Parse(id), Email = $"u{id}@mail.com" });

        _service = new LoanOverdueService(
            _loans.Object, _uow.Object, _clock, _email.Object, _userManager.Object,
            Options.Create(new LoanSettings { FinePerDay = 0.50m }),
            NullLogger<LoanOverdueService>.Instance);
    }

    private Loan NewActiveLoan(int id, int readerId, int dueInDays) => new()
    {
        Id = id,
        BookId = id,
        ReaderId = readerId,
        Status = LoanStatus.Active,
        BorrowedAt = _clock.UtcNow.AddDays(dueInDays - 14),
        DueAt = _clock.UtcNow.AddDays(dueInDays),
        Book = new Book { Id = id, Title = "Book " + id }
    };

    private void SetupActive(params Loan[] loans) =>
        _loans.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(loans.ToList());

    [Fact]
    public async Task MarkOverdue_SomeDue_MarksOnlyDue()
    {
        // Arrange: две из трёх просрочены
        var due1 = NewActiveLoan(1, 1, -1);
        var due2 = NewActiveLoan(2, 2, -2);
        var notDue = NewActiveLoan(3, 3, 5);
        SetupActive(due1, due2, notDue);

        // Act
        var count = await _service.MarkOverdueAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, count);
        Assert.Equal(LoanStatus.Overdue, due1.Status);
        Assert.Equal(LoanStatus.Overdue, due2.Status);
        Assert.Equal(LoanStatus.Active, notDue.Status);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkOverdue_NothingDue_DoesNothing()
    {
        // Arrange
        SetupActive(NewActiveLoan(1, 1, 5));

        // Act
        var count = await _service.MarkOverdueAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, count);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkOverdue_SendsEmailPerLoan()
    {
        // Arrange
        SetupActive(NewActiveLoan(1, 1, -1), NewActiveLoan(2, 2, -2));

        // Act
        await _service.MarkOverdueAsync(CancellationToken.None);

        // Assert
        _email.Verify(e => e.SendAsync("u1@mail.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(e => e.SendAsync("u2@mail.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkOverdue_EmailFails_ContinuesWithOthers()
    {
        // Arrange: письмо первому читателю падает
        var first = NewActiveLoan(1, 1, -1);
        var second = NewActiveLoan(2, 2, -2);
        SetupActive(first, second);
        _email.Setup(e => e.SendAsync("u1@mail.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP недоступен"));

        // Act
        var count = await _service.MarkOverdueAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, count);
        Assert.Equal(LoanStatus.Overdue, first.Status);
        Assert.Equal(LoanStatus.Overdue, second.Status);
        _email.Verify(e => e.SendAsync("u2@mail.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
