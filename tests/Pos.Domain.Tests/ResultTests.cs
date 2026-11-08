using Pos.Domain.Common;

namespace Pos.Domain.Tests;

public class ResultTests
{
    private static readonly Error Failure = new(ErrorCode.OrderNotOpen, "Closed.");

    [Fact]
    public void Ok_has_no_error()
    {
        Assert.True(Result.Ok().IsSuccess);
        Assert.Null(Result.Ok().Error);
    }

    [Fact]
    public void Ok_with_data_has_the_data_and_no_error()
    {
        var result = Result.Ok(5);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Data);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Ok_may_have_null_data()
    {
        var result = Result.Ok<string?>(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Fail_has_the_error()
    {
        var result = Result.Fail(Failure);

        Assert.False(result.IsSuccess);
        Assert.Equal(Failure, result.Error);
    }

    [Fact]
    public void Fail_with_data_type_has_the_error_and_no_data()
    {
        var result = Result.Fail<string>(Failure);

        Assert.False(result.IsSuccess);
        Assert.Equal(Failure, result.Error);
        Assert.Null(result.Data);
    }
}
