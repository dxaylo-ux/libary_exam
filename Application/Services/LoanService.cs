using System.Globalization;
using Application.ApiResult;
using Application.DTOs;
using Application.Identity;
using Application.Interfaces;
using Application.Settings;
using Application.Utils;
using Domain.Entities;
using Domain.Enums;
using Domain.Filters;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

public class LoanService(
    ILoanRepository loans,
    IBookRepository books,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IEmailService emailService,
    UserManager<User> userManager,
    IBookCache cache,
    IOptions<LoanSettings> settings,
    ILogger<LoanService> logger)
{
    public async Task<Result<LoanDto>> BorrowAsync(int bookId, int readerId, CancellationToken ct = default)
    {
        var book = await books.GetByIdAsync(bookId, ct);
        if (book == null)
            return Result<LoanDto>.Fail("Книга не найдена", ErrorType.NotFound);

        var now = clock.UtcNow;
        var unreturned = await loans.GetUnreturnedByReaderAsync(readerId, ct);

        if (unreturned.Any(l => l.Status == LoanStatus.Overdue ||
                                (l.Status == LoanStatus.Active && l.DueAt <= now)))
            return Result<LoanDto>.Fail("У вас есть просроченная выдача. Сначала верните книгу", ErrorType.Conflict);

        if (unreturned.Count >= settings.Value.MaxActiveLoans)
            return Result<LoanDto>.Fail(
                $"Достигнут лимит невозвращённых книг ({settings.Value.MaxActiveLoans})",
                ErrorType.Conflict);

        if (unreturned.Any(l => l.BookId == bookId))
            return Result<LoanDto>.Fail("Эта книга уже у вас на руках", ErrorType.Conflict);

        if (book.AvailableCopies == 0)
            return Result<LoanDto>.Fail("Свободных экземпляров нет", ErrorType.Conflict);

        var loan = new Loan
        {
            BookId = book.Id,
            ReaderId = readerId,
            BorrowedAt = now,
            DueAt = now.AddDays(settings.Value.LoanDays),
            Status = LoanStatus.Active,
            Fine = 0m
        };
        book.AvailableCopies--;

        await loans.AddAsync(loan, ct);
        await unitOfWork.SaveChangesAsync(ct);
        cache.Invalidate();

        await TrySendEmailAsync(
            readerId,
            "Книга выдана",
            $"Вам выдана книга «{book.Title}». Срок возврата: {Format(loan.DueAt)} (UTC).",
            ct);

        return Result<LoanDto>.Success(new LoanDto
        {
            Id = loan.Id,
            BookId = loan.BookId,
            BookTitle = book.Title,
            ReaderId = loan.ReaderId,
            BorrowedAt = loan.BorrowedAt,
            DueAt = loan.DueAt,
            ReturnedAt = loan.ReturnedAt,
            Status = loan.Status.ToString(),
            Fine = loan.Fine
        });
    }

    public async Task<Result<LoanDto>> ReturnAsync(int loanId, int userId, string role, CancellationToken ct = default)
    {
        var loan = await loans.GetByIdAsync(loanId, ct);
        if (loan == null)
            return Result<LoanDto>.Fail("Выдача не найдена", ErrorType.NotFound);

        if (loan.ReaderId != userId &&
            role != nameof(UserRole.Librarian) &&
            role != nameof(UserRole.Admin))
            return Result<LoanDto>.Fail("Вернуть книгу может владелец выдачи, библиотекарь или администратор", ErrorType.Forbidden);

        if (loan.Status == LoanStatus.Returned)
            return Result<LoanDto>.Fail("Книга уже возвращена", ErrorType.Conflict);

        var now = clock.UtcNow;
        loan.ReturnedAt = now;
        loan.Status = LoanStatus.Returned;
        loan.Fine = FineCalculator.Calculate(loan.DueAt, now, settings.Value.FinePerDay);
        loan.Book.AvailableCopies++;

        await unitOfWork.SaveChangesAsync(ct);
        cache.Invalidate();

        var body = $"Книга «{loan.Book.Title}» возвращена.";
        if (loan.Fine > 0)
            body += $" Штраф за просрочку: {loan.Fine.ToString("0.00", CultureInfo.InvariantCulture)}.";

        await TrySendEmailAsync(loan.ReaderId, "Книга возвращена", body, ct);

        return Result<LoanDto>.Success(new LoanDto
        {
            Id = loan.Id,
            BookId = loan.BookId,
            BookTitle = loan.Book.Title,
            ReaderId = loan.ReaderId,
            BorrowedAt = loan.BorrowedAt,
            DueAt = loan.DueAt,
            ReturnedAt = loan.ReturnedAt,
            Status = loan.Status.ToString(),
            Fine = loan.Fine
        });
    }

    public Task<Result<PagedResult<LoanDto>>> GetMyAsync(int userId, string? status, int page, int pageSize, CancellationToken ct = default) =>
        GetPageAsync(status, userId, page, pageSize, ct);

    public Task<Result<PagedResult<LoanDto>>> GetAllAsync(string? status, int? readerId, int page, int pageSize, CancellationToken ct = default) =>
        GetPageAsync(status, readerId, page, pageSize, ct);

    private async Task<Result<PagedResult<LoanDto>>> GetPageAsync(string? status, int? readerId, int page, int pageSize, CancellationToken ct)
    {
        LoanStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            foreach (var value in Enum.GetValues<LoanStatus>())
            {
                if (string.Equals(value.ToString(), status.Trim(), StringComparison.OrdinalIgnoreCase))
                    parsedStatus = value;
            }

            if (parsedStatus == null)
                return Result<PagedResult<LoanDto>>.Fail("Неизвестный статус. Допустимо: Active, Returned, Overdue", ErrorType.Validation);
        }

        if (page < 1) page = 1;
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 10;

        var filter = new LoanFilter
        {
            Status = parsedStatus,
            ReaderId = readerId,
            Page = page,
            PageSize = pageSize
        };

        var result = await loans.GetPagedAsync(filter, ct);

        return Result<PagedResult<LoanDto>>.Success(new PagedResult<LoanDto>
        {
            Items = result.Items.Select(loan => new LoanDto
            {
                Id = loan.Id,
                BookId = loan.BookId,
                BookTitle = loan.Book.Title,
                ReaderId = loan.ReaderId,
                BorrowedAt = loan.BorrowedAt,
                DueAt = loan.DueAt,
                ReturnedAt = loan.ReturnedAt,
                Status = loan.Status.ToString(),
                Fine = loan.Fine
            }).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount
        });
    }

    private async Task TrySendEmailAsync(int readerId, string subject, string body, CancellationToken ct)
    {
        try
        {
            var user = await userManager.FindByIdAsync(readerId.ToString());
            if (string.IsNullOrWhiteSpace(user?.Email))
                return;

            await emailService.SendAsync(user.Email, subject, body, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось отправить письмо читателю {ReaderId}", readerId);
        }
    }

    private static string Format(DateTime value) =>
        value.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
}
