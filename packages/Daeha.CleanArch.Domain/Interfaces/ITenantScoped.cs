namespace Daeha.CleanArch.Domain.Interfaces;

/// <summary>
/// 테넌트(회원사) 범위 데이터를 나타내는 마커 인터페이스.
/// 이 인터페이스를 구현한 엔티티는 BaseDbContext 글로벌 필터에 의해
/// 현재 로그인 테넌트의 데이터만 자동으로 필터링됨.
/// </summary>
/// <remarks>
/// SystemAdmin은 <c>IgnoreQueryFilters()</c>로 전체 데이터 접근 가능.
/// </remarks>
public interface ITenantScoped
{
    /// <summary>소속 테넌트(회원사) ID</summary>
    Guid TenantId { get; }
}
