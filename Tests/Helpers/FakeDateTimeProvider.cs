using Application.Interfaces;

namespace Tests.Helpers;

public class FakeDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
}
