using Application.ApiResult;
using Domain.Entities;
using Domain.Filters;

namespace Application.Interfaces;

public interface ILoanRepository
{
    Task<Loan?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Loan>> GetUnreturnedByReaderAsync(int readerId, CancellationToken ct = default);
    Task<List<Loan>> GetActiveAsync(CancellationToken ct = default);
    Task<PagedResult<Loan>> GetPagedAsync(LoanFilter filter, CancellationToken ct = default);
    Task AddAsync(Loan loan, CancellationToken ct = default);
}
