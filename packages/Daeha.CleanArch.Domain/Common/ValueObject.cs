namespace Daeha.CleanArch.Domain.Common;

/// <summary>
/// DDD Value Object 베이스 클래스. 값 기반 동등성 비교를 제공.
/// 주소, 금액, 전화번호 등 불변 값 객체에 사용.
/// </summary>
/// <example>
/// <code>
/// public class Money : ValueObject
/// {
///     public decimal Amount { get; }
///     public string Currency { get; }
///     public Money(decimal amount, string currency) { Amount = amount; Currency = currency; }
///     protected override IEnumerable&lt;object?&gt; GetEqualityComponents()
///     {
///         yield return Amount;
///         yield return Currency;
///     }
/// }
/// </code>
/// </example>
public abstract class ValueObject
{
    /// <summary>동등성 비교에 사용할 구성 요소를 반환.</summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType()) return false;
        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    /// <inheritdoc />
    public override int GetHashCode()
        => GetEqualityComponents()
            .Aggregate(0, (hash, component) =>
                HashCode.Combine(hash, component?.GetHashCode() ?? 0));

    /// <summary>값 동등 비교 연산자</summary>
    public static bool operator ==(ValueObject? left, ValueObject? right)
        => left?.Equals(right) ?? right is null;

    /// <summary>값 비동등 비교 연산자</summary>
    public static bool operator !=(ValueObject? left, ValueObject? right)
        => !(left == right);
}
