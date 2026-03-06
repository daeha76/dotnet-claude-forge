namespace Daeha.CleanArch.Domain.Common;

/// <summary>
/// 비즈니스 오류를 Exception 없이 전달하는 Result 패턴 (non-generic).
/// Exception은 진짜 예외(네트워크 단절, DB 연결 실패 등)에만 사용.
/// </summary>
public class Result
{
    /// <summary>작업 성공 여부</summary>
    public bool IsSuccess { get; }

    /// <summary>실패 시 오류 메시지. 성공 시 null.</summary>
    public string? Error { get; }

    /// <summary>Result 생성자. 하위 클래스에서만 호출.</summary>
    protected Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>성공 Result 생성</summary>
    public static Result Success() => new(true, null);

    /// <summary>실패 Result 생성</summary>
    public static Result Failure(string error) => new(false, error);

    /// <summary>값을 포함한 성공 Result 생성 (제네릭 오버로드)</summary>
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    /// <summary>값을 포함한 실패 Result 생성 (제네릭 오버로드)</summary>
    public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);
}

/// <summary>
/// 비즈니스 오류를 Exception 없이 전달하는 Result 패턴 (generic).
/// Handler에서 <c>return Result.Success(value)</c> 또는 <c>return Result.Failure&lt;T&gt;("오류")</c> 로 사용.
/// </summary>
public sealed class Result<T> : Result
{
    /// <summary>성공 시 반환 값. 실패 시 default.</summary>
    public T? Value { get; }

    private Result(bool isSuccess, T? value, string? error)
        : base(isSuccess, error)
    {
        Value = value;
    }

    /// <summary>값을 포함한 성공 Result 생성</summary>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>실패 Result 생성</summary>
    public new static Result<T> Failure(string error) => new(false, default, error);
}
