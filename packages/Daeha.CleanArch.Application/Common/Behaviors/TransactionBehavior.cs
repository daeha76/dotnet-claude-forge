using Daeha.CleanArch.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Daeha.CleanArch.Application.Common.Behaviors;

/// <summary>
/// Command에 이 인터페이스를 구현하면 TransactionBehavior가 DB 트랜잭션으로 자동 래핑.
/// Query에는 적용하지 않음.
/// </summary>
/// <example>
/// <code>
/// public record CreateOrderCommand(...) : IRequest&lt;Result&lt;Guid&gt;&gt;, ITransactionalCommand { }
/// </code>
/// </example>
public interface ITransactionalCommand { }

/// <summary>
/// MediatR 파이프라인: ITransactionalCommand를 구현한 Command를 DB 트랜잭션으로 래핑.
/// 예외 발생 시 자동 롤백.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (request is not ITransactionalCommand)
            return await next(ct);

        var requestName = typeof(TRequest).Name;
        TResponse response = default!;

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            response = await next(ct);
            logger.LogDebug("트랜잭션 커밋: {RequestName}", requestName);
        }, ct);

        return response;
    }
}
