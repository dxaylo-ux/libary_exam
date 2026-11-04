using Application.ApiResult;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Filters;
using Infrastructure.Persistence.Data;
using Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class LoanRepository(AppDbContext db) : ILoanRepository
{
    public Task<Loan?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Loans.Include(l => l.Book).FirstOrDefaultAsync(l => l.Id == id, ct);

    public Task<List<Loan>> GetUnreturnedByReaderAsync(int readerId, CancellationToken ct = default) =>
        db.Loans
            .AsNoTracking()
            .Where(l => l.ReaderId == readerId &&
                        (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue))
            .ToListAsync(ct);

    public Task<List<Loan>> GetActiveAsync(CancellationToken ct = default) =>
        db.Loans
            .Include(l => l.Book)
            .Where(l => l.Status == LoanStatus.Active)
            .ToListAsync(ct);

    public async Task<PagedResult<Loan>> GetPagedAsync(LoanFilter filter, CancellationToken ct = default)
    {
        var query = db.Loans.AsNoTracking().Include(l => l.Book).AsQueryable();

        if (filter.Status.HasValue)
            query = query.Where(l => l.Status == filter.Status.Value);

        if (filter.ReaderId.HasValue)
            query = query.Where(l => l.ReaderId == filter.ReaderId.Value);

        return await query
            .OrderByDescending(l => l.BorrowedAt)
            .ThenBy(l => l.Id)
            .ToPagedListAsync(filter.Page, filter.PageSize, ct);
    }

    public async Task AddAsync(Loan loan, CancellationToken ct = default) =>
        await db.Loans.AddAsync(loan, ct);
}
