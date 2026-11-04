namespace Application.Settings;

public class LoanSettings
{
    public const string SectionName = "Loan";

    public int LoanDays { get; set; } = 14;
    public int MaxActiveLoans { get; set; } = 3;
    public decimal FinePerDay { get; set; } = 0.50m;
    public int OverdueCheckSeconds { get; set; } = 30;
}
