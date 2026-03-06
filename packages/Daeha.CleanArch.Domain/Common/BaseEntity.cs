namespace Daeha.CleanArch.Domain.Common;

/// <summary>
/// 모든 도메인 엔티티의 기반 클래스.
/// Id, 감사 컬럼(CreatedAt/UpdatedAt/CreatedBy/UpdatedBy)을 포함.
/// AppDbContext.SaveChangesAsync에서 자동으로 감사 컬럼을 설정함.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>엔티티 고유 식별자 (UUID v4)</summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>생성 일시 (UTC)</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>마지막 수정 일시 (UTC)</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>생성한 사용자 ID. 익명 작업 시 null.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>마지막으로 수정한 사용자 ID. 익명 작업 시 null.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>
    /// INSERT 시 BaseDbContext.SaveChangesAsync에서 자동 호출.
    /// 직접 호출 금지.
    /// </summary>
    public void SetCreatedAudit(Guid? userId, DateTimeOffset now)
    {
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = userId;
        UpdatedBy = userId;
    }

    /// <summary>
    /// UPDATE 시 BaseDbContext.SaveChangesAsync에서 자동 호출.
    /// 직접 호출 금지.
    /// </summary>
    public void SetUpdatedAudit(Guid? userId, DateTimeOffset now)
    {
        UpdatedAt = now;
        UpdatedBy = userId;
    }
}
