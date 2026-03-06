using Daeha.CleanArch.Domain.Common;
using FluentAssertions;

namespace Daeha.CleanArch.Tests.Domain;

[Trait("Category", "Unit")]
public class ResultTests
{
    [Fact]
    public void Success_ShouldReturnSuccessResult()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldReturnFailureResult()
    {
        var result = Result.Failure("오류 발생");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("오류 발생");
    }

    [Fact]
    public void SuccessGeneric_ShouldReturnValueWithSuccess()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void FailureGeneric_ShouldReturnNullValueWithError()
    {
        var result = Result.Failure<int>("잔액 부족");

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Be(default);
        result.Error.Should().Be("잔액 부족");
    }

    [Fact]
    public void SuccessGeneric_ShouldWorkWithReferenceTypes()
    {
        var dto = new { Name = "홍길동" };
        var result = Result.Success(dto);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }
}
