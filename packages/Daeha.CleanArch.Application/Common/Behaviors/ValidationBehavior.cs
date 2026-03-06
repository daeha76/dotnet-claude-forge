using FluentValidation;
using MediatR;

namespace Daeha.CleanArch.Application.Common.Behaviors;

/// <summary>
/// MediatR 파이프라인: 모든 Command/Query 진입 전 FluentValidation 자동 실행.
/// Validator가 없으면 통과. ValidationException 발생 시 Api 레이어에서 400 Problem Details로 변환.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!validators.Any())
            return await next(ct);

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next(ct);
    }
}
