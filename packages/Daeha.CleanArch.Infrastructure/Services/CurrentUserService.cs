using System.Security.Claims;
using Daeha.CleanArch.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Daeha.CleanArch.Infrastructure.Services;

/// <summary>
/// HttpContext의 JWT 클레임에서 현재 사용자 정보를 파싱하는 ICurrentUserService 구현체.
/// </summary>
/// <remarks>
/// 파싱 클레임:
/// <list type="bullet">
///   <item><c>sub</c> (ClaimTypes.NameIdentifier) → User ID</item>
///   <item><c>tenant_id</c> → Tenant ID (SystemAdmin은 없을 수 있음)</item>
///   <item><c>role</c> (ClaimTypes.Role 또는 "role") → 역할 문자열</item>
/// </list>
/// </remarks>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    /// <inheritdoc />
    public Guid Id => ParseGuid(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("인증된 사용자의 Id 클레임을 찾을 수 없습니다.");

    /// <inheritdoc />
    public Guid TenantId => ParseGuid("tenant_id") ?? Guid.Empty;

    /// <inheritdoc />
    public string Role => User?.FindFirstValue(ClaimTypes.Role)
        ?? User?.FindFirstValue("role")
        ?? string.Empty;

    /// <inheritdoc />
    public bool IsSystemAdmin => Role == "system_admin";

    /// <inheritdoc />
    public bool IsCompanyAdmin => Role is "system_admin" or "company_admin";

    private Guid? ParseGuid(string claimType)
    {
        var value = User?.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
