using Daeha.CleanArch.Application.Common.Interfaces;

namespace Daeha.CleanArch.Infrastructure.Persistence;

/// <summary>
/// EF Core 마이그레이션 CLI 전용 더미 ICurrentUserService 구현체.
/// <c>dotnet ef migrations add ...</c> 실행 시 AppDbContextFactory에서 사용.
/// 런타임에는 DI로 실제 CurrentUserService가 주입되므로 절대 DI 등록 금지.
/// </summary>
public sealed class DesignTimeCurrentUserService : ICurrentUserService
{
    /// <inheritdoc />
    public Guid Id => Guid.Empty;

    /// <inheritdoc />
    public Guid TenantId => Guid.Empty;

    /// <inheritdoc />
    public string Role => string.Empty;

    /// <inheritdoc />
    public bool IsAuthenticated => false;

    /// <inheritdoc />
    public bool IsSystemAdmin => false;

    /// <inheritdoc />
    public bool IsCompanyAdmin => false;
}
