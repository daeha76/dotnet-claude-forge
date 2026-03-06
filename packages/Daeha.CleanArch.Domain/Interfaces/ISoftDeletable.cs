namespace Daeha.CleanArch.Domain.Interfaces;

/// <summary>
/// 소프트 삭제를 지원하는 엔티티 마커 인터페이스.
/// 이 인터페이스를 구현한 엔티티는 BaseDbContext 글로벌 필터에 의해
/// <c>IsDeleted = false</c>인 데이터만 자동으로 조회됨.
/// 실제 DB 행 삭제 없이 IsDeleted 플래그만 변경.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>삭제 여부 플래그</summary>
    bool IsDeleted { get; }

    /// <summary>삭제된 일시 (UTC). 미삭제 시 null.</summary>
    DateTimeOffset? DeletedAt { get; }

    /// <summary>삭제를 수행한 사용자 ID. 미삭제 시 null.</summary>
    Guid? DeletedBy { get; }
}
