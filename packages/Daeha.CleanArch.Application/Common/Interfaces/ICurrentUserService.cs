namespace Daeha.CleanArch.Application.Common.Interfaces;

/// <summary>
/// 현재 로그인한 사용자의 컨텍스트를 제공.
/// Infrastructure 레이어의 CurrentUserService가 JWT Claim에서 파싱하여 구현.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>사용자 ID (Supabase auth.users.id)</summary>
    Guid Id { get; }

    /// <summary>소속 테넌트(회원사) ID. SystemAdmin은 Guid.Empty.</summary>
    Guid TenantId { get; }

    /// <summary>역할 문자열 (system_admin / company_admin / member 등)</summary>
    string Role { get; }

    /// <summary>인증된 사용자 여부</summary>
    bool IsAuthenticated { get; }

    /// <summary>시스템 관리자 여부</summary>
    bool IsSystemAdmin { get; }

    /// <summary>회사 관리자 이상 여부 (system_admin 포함)</summary>
    bool IsCompanyAdmin { get; }
}
