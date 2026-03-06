using Daeha.CleanArch.Application.Common.Interfaces;
using MediatR;

namespace Daeha.CleanArch.Application.Common.Behaviors;

/// <summary>
/// Command에 이 인터페이스를 구현하면 AuthorizationBehavior가 역할을 자동 검증.
/// RequiredRoles 중 하나와 현재 사용자 Role이 일치해야 통과.
/// </summary>
/// <example>
/// <code>
/// public record DeleteTenantCommand(Guid TenantId) : IRequest&lt;Result&gt;, IAuthorizedRequest
/// {
///     public IReadOnlyList&lt;string&gt; RequiredRoles => ["system_admin"];
/// }
/// </code>
/// </example>
public interface IAuthorizedRequest
{
    /// <summary>허용되는 역할 목록. 하나라도 일치하면 통과.</summary>
    IReadOnlyList<string> RequiredRoles { get; }
}

/// <summary>
/// MediatR 파이프라인: IAuthorizedRequest를 구현한 Command/Query의 역할(Role)을 자동 검증.
/// IAuthorizedRequest를 구현하지 않은 요청은 이 Behavior를 건너뜀.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(
    ICurrentUserService currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (request is not IAuthorizedRequest authorizedRequest)
            return await next(ct);

        if (!authorizedRequest.RequiredRoles.Contains(currentUser.Role))
            throw new UnauthorizedAccessException(
                $"역할 '{currentUser.Role}'은(는) 이 작업을 수행할 권한이 없습니다. " +
                $"필요 역할: [{string.Join(", ", authorizedRequest.RequiredRoles)}]");

        return await next(ct);
    }
}
