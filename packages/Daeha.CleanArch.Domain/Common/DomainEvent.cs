namespace Daeha.CleanArch.Domain.Common;

/// <summary>
/// 도메인 이벤트 베이스 레코드. 상태 변경 시 AggregateRoot에서 발행.
/// MediatR INotificationHandler로 구독하여 사이드 이펙트 처리.
/// </summary>
public abstract record DomainEvent
{
    /// <summary>이벤트 고유 식별자</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>이벤트 발생 일시 (UTC)</summary>
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
