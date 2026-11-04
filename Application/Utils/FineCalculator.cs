namespace Application.Utils;

public static class FineCalculator
{
    public static decimal Calculate(DateTime dueAt, DateTime returnedAt, decimal finePerDay)
    {
        if (returnedAt <= dueAt)
            return 0m;

        var days = (decimal)Math.Ceiling((returnedAt - dueAt).TotalDays);
        return days * finePerDay;
    }
}
