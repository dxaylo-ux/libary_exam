using Application.ApiResult;

namespace Tests;

public class ResultTests
{
    [Fact]
    public void Fail_SetsErrorFields()
    {
        // Arrange & Act
        var fail = Result<int>.Fail("Ошибка", ErrorType.NotFound);
        var success = Result<int>.Success(42);

        // Assert
        Assert.False(fail.IsSuccess);
        Assert.Equal(ErrorType.NotFound, fail.ErrorType);
        Assert.Equal("Ошибка", fail.ErrorMessage);

        Assert.True(success.IsSuccess);
        Assert.Equal(42, success.Data);
        Assert.Null(success.ErrorType);
        Assert.Null(success.ErrorMessage);
    }

    [Theory]
    [InlineData(1, false, true)]
    [InlineData(2, true, true)]
    [InlineData(3, true, false)]
    public void PagedResult_CalculatesPaging(int page, bool hasPrevious, bool hasNext)
    {
        // Arrange
        var paged = new PagedResult<int> { Page = page, PageSize = 10, TotalCount = 25 };

        // Act & Assert
        Assert.Equal(3, paged.TotalPages);
        Assert.Equal(hasPrevious, paged.HasPrevious);
        Assert.Equal(hasNext, paged.HasNext);
    }
}
