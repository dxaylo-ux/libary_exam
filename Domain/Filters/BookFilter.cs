namespace Domain.Filters;

public class BookFilter
{
    public string? Search { get; set; }
    public bool OnlyAvailable { get; set; }
    public int? MinYear { get; set; }
    public int? MaxYear { get; set; }
    public string SortBy { get; set; } = "title";
    public bool Desc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
