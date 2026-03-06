namespace Daeha.CleanArch.Application.Common.Interfaces;

/// <summary>
/// 트랜잭션 경계를 추상화하는 인터페이스.
/// TransactionBehavior에서 사용하며, Infrastructure의 BaseDbContext 구현체가 이를 구현.
/// Application 레이어는 DbContext를 직접 참조하지 않고 이 인터페이스만 사용.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>변경 사항을 DB에 저장 (감사 컬럼 자동 설정 포함)</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>지정된 작업을 DB 트랜잭션으로 래핑하여 실행. 예외 발생 시 자동 롤백.</summary>
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);
}
