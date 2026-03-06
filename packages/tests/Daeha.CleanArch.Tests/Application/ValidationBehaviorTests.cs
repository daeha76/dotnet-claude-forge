using Daeha.CleanArch.Application.Common.Behaviors;
using FluentAssertions;
using FluentValidation;
using MediatR;

namespace Daeha.CleanArch.Tests.Application;

[Trait("Category", "Unit")]
public class ValidationBehaviorTests
{
    private record TestRequest(string Name) : IRequest<string>;

    private sealed class TestValidator : AbstractValidator<TestRequest>
    {
        public TestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("이름은 필수입니다.");
            RuleFor(x => x.Name).MaximumLength(10).WithMessage("이름은 10자 이하여야 합니다.");
        }
    }

    [Fact]
    public async Task Handle_WithValidRequest_ShouldCallNext()
    {
        var validators = new IValidator<TestRequest>[] { new TestValidator() };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var nextCalled = false;

        var result = await behavior.Handle(
            new TestRequest("홍길동"),
            (ct) => { nextCalled = true; return Task.FromResult("ok"); },
            CancellationToken.None);

        nextCalled.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ShouldThrowValidationException()
    {
        var validators = new IValidator<TestRequest>[] { new TestValidator() };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);

        var act = async () => await behavior.Handle(
            new TestRequest(""),
            (ct) => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*이름은 필수입니다.*");
    }

    [Fact]
    public async Task Handle_WithNoValidators_ShouldCallNext()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([]);
        var nextCalled = false;

        await behavior.Handle(
            new TestRequest(""),
            (ct) => { nextCalled = true; return Task.FromResult("ok"); },
            CancellationToken.None);

        nextCalled.Should().BeTrue();
    }
}
