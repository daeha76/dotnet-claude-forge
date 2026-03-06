using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Daeha.CleanArch.Application.Common.Behaviors;

/// <summary>
/// MediatR 파이프라인: 모든 요청의 이름/소요시간/성공여부를 자동 로깅.
/// 파이프라인 순서: Logging → Validation → Authorization → Transaction → Handler
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("요청 시작: {RequestName}", requestName);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next(ct);
            sw.Stop();
            logger.LogInformation(
                "요청 완료: {RequestName} ({ElapsedMs}ms)", requestName, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(
                ex, "요청 실패: {RequestName} ({ElapsedMs}ms)", requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
