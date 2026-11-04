using Domain.Enums;

namespace Domain.Filters;

public class LoanFilter
{
    public LoanStatus? Status { get; set; }
    public int? ReaderId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
