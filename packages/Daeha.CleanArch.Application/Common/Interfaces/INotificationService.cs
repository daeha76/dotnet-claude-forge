namespace Daeha.CleanArch.Application.Common.Interfaces;

/// <summary>알림 발송 채널</summary>
public enum NotificationChannel
{
    /// <summary>이메일 (Azure Communication Services / SendGrid)</summary>
    Email,
    /// <summary>카카오 알림톡 (비즈뿌리오)</summary>
    KakaoTalk,
    /// <summary>SMS (비즈뿌리오 Fallback)</summary>
    Sms,
    /// <summary>웹 푸시 알림</summary>
    WebPush
}

/// <summary>
/// 알림 발송 메시지.
/// NotificationType은 각 프로젝트에서 문자열 상수로 정의하여 사용.
/// </summary>
/// <param name="RecipientId">수신자 ID (Personnel.Id 또는 외부 이메일)</param>
/// <param name="NotificationType">알림 유형 식별자. 각 프로젝트에서 상수로 정의.</param>
/// <param name="Variables">템플릿 변수 (치환 키 → 값)</param>
/// <param name="Channels">발송 채널 목록. null이면 수신자 설정에 따라 자동 선택.</param>
public record NotificationMessage(
    string RecipientId,
    string NotificationType,
    Dictionary<string, string> Variables,
    NotificationChannel[]? Channels = null);

/// <summary>
/// 알림 발송 추상화 인터페이스. 단일 인터페이스로 이메일/알림톡/SMS를 통합 발송.
/// </summary>
public interface INotificationService
{
    /// <summary>알림 발송. 채널은 수신자 설정 및 NotificationType에 따라 자동 선택.</summary>
    Task SendAsync(NotificationMessage message, CancellationToken ct = default);
}
