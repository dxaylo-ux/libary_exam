using System.Globalization;
using Application.Identity;
using Application.Interfaces;
using Application.Settings;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

public class LoanOverdueService(
    ILoanRepository loans,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IEmailService emailService,
    UserManager<User> userManager,
    IOptions<LoanSettings> settings,
    ILogger<LoanOverdueService> logger)
{
    public async Task<int> MarkOverdueAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;

        var active = await loans.GetActiveAsync(ct);
        var due = active
            .Where(l => l.Status == LoanStatus.Active && l.DueAt <= now)
            .ToList();

        if (due.Count == 0)
            return 0;

        foreach (var loan in due)
            loan.Status = LoanStatus.Overdue;

        await unitOfWork.SaveChangesAsync(ct);

        foreach (var loan in due)
        {
            try
            {
                var user = await userManager.FindByIdAsync(loan.ReaderId.ToString());
                if (string.IsNullOrWhiteSpace(user?.Email))
                    continue;

                var body =
                    $"Срок возврата книги «{loan.Book.Title}» истёк " +
                    $"{loan.DueAt.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)} (UTC). " +
                    $"Штраф за каждый день просрочки: {settings.Value.FinePerDay.ToString("0.00", CultureInfo.InvariantCulture)}.";

                await emailService.SendAsync(user.Email, "Выдача просрочена", body, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Не удалось отправить письмо о просрочке по выдаче {LoanId}", loan.Id);
            }
        }

        return due.Count;
    }
}
