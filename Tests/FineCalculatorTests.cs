using Application.Utils;

namespace Tests;

public class FineCalculatorTests
{
    private const decimal FinePerDay = 0.50m;
    private static readonly DateTime Due = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Calculate_ReturnedOnTime_ReturnsZero()
    {
        // Arrange
        var returnedAt = Due;

        // Act
        var fine = FineCalculator.Calculate(Due, returnedAt, FinePerDay);

        // Assert
        Assert.Equal(0m, fine);
    }

    [Fact]
    public void Calculate_OneHourLate_ChargesOneDay()
    {
        // Arrange
        var returnedAt = Due.AddHours(1);

        // Act
        var fine = FineCalculator.Calculate(Due, returnedAt, FinePerDay);

        // Assert
        Assert.Equal(1 * FinePerDay, fine);
    }

    [Fact]
    public void Calculate_TwoDaysOneHourLate_ChargesThreeDays()
    {
        // Arrange
        var returnedAt = Due.AddDays(2).AddHours(1);

        // Act
        var fine = FineCalculator.Calculate(Due, returnedAt, FinePerDay);

        // Assert
        Assert.Equal(3 * FinePerDay, fine);
    }
}
