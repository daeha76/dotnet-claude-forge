namespace Daeha.CleanArch.Domain.Common;

/// <summary>
/// DDD Aggregate Root. 도메인 이벤트를 발행하고 수집하는 기반 클래스.
/// Application 레이어에서 SaveChanges 후 MediatR를 통해 이벤트를 디스패치함.
/// </summary>
public abstract class AggregateRoot : BaseEntity
{
    private readonly List<DomainEvent> _domainEvents = [];

    /// <summary>아직 디스패치되지 않은 도메인 이벤트 목록</summary>
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>도메인 이벤트 등록. 하위 도메인 메서드에서만 호출.</summary>
    protected void AddDomainEvent(DomainEvent @event) => _domainEvents.Add(@event);

    /// <summary>디스패치 완료 후 이벤트 목록 초기화.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
