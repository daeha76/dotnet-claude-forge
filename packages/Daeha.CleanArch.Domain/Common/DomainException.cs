namespace Daeha.CleanArch.Domain.Common;

/// <summary>
/// 도메인 비즈니스 규칙 위반 시 발생하는 예외.
/// 기술적 예외(DB 연결 실패 등)와 구분하기 위해 사용.
/// </summary>
/// <remarks>
/// 비즈니스 오류는 가능하면 <see cref="Result{T}"/> 패턴을 우선 사용.
/// DomainException은 불변식(invariant) 위반처럼 절대 발생해서는 안 되는 상황에만 사용.
/// </remarks>
public sealed class DomainException : Exception
{
    /// <summary>도메인 예외 생성</summary>
    public DomainException(string message) : base(message) { }

    /// <summary>내부 예외를 포함한 도메인 예외 생성</summary>
    public DomainException(string message, Exception inner) : base(message, inner) { }
}
