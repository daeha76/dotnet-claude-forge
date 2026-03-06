# Shared: 알림 시스템 (이메일 + 비즈뿌리오 알림톡)

## 원칙: 단일 인터페이스, 다중 채널

모든 알림은 `INotificationService` 단일 인터페이스를 통해 발송.
채널(이메일/알림톡/SMS)은 수신자 설정 및 알림 유형에 따라 자동 선택.

```csharp
// Application/Interfaces/INotificationService.cs
public interface INotificationService
{
    Task SendAsync(NotificationMessage message, CancellationToken ct = default);
}

public record NotificationMessage(
    string RecipientId,        // Personnel.Id or 외부 이메일
    NotificationType Type,     // 어떤 알림인지
    Dictionary<string, string> Variables,  // 템플릿 변수
    NotificationChannel[] Channels = default  // null이면 수신자 설정 따름
);

public enum NotificationChannel { Email, KakaoTalk, Sms, WebPush }
```

---

## 이메일 (Azure Communication Services 또는 SendGrid)

### 패키지 선택 기준
- **Azure Communication Services (Email)**: Azure 인프라 통일, Key Vault 연동 간편 → **권장**
- **SendGrid**: 템플릿 관리 UI, 무료 100건/일 → 소규모 시작 시

```csharp
// Infrastructure/Notifications/EmailNotificationSender.cs
public class EmailNotificationSender
{
    // Azure Communication Services 사용
    // Nuget: Azure.Communication.Email
    private readonly EmailClient _emailClient;

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var message = new EmailMessage(
            senderAddress: _config["Email:SenderAddress"],
            recipients: new EmailRecipients([new EmailAddress(to)]),
            content: new EmailContent(subject) { Html = htmlBody }
        );
        await _emailClient.SendAsync(WaitUntil.Started, message);
    }
}
```

### 이메일 템플릿 위치
```
src/Infrastructure/Notifications/Templates/Email/
├── bid-deadline-reminder.html      # 입찰 마감 D-3 알림
├── certification-expiry.html       # 자격증 만료 알림
├── site-assignment.html            # 현장 배치 통보
├── monthly-report-due.html         # 월간 보고서 제출 알림
└── contract-signed.html            # 계약 체결 알림
```

---

## 비즈뿌리오 알림톡 (카카오 알림톡)

### 비즈뿌리오 API 연동
- 발신번호 등록 필수 (사전 심사)
- 알림톡 템플릿 승인 필요 (카카오 심사 1~3일)
- 실패 시 **SMS 자동 대체(Fallback)** 설정 필수

```csharp
// Infrastructure/Notifications/BizppurioSender.cs
public class BizppurioSender
{
    // 비즈뿌리오 API: https://bizppurio.com
    // 문서: API 가이드 참조
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;        // Key Vault에서 주입
    private readonly string _senderId;      // 발신번호

    public async Task<bool> SendAlimtalkAsync(
        string phone,
        string templateCode,    // 카카오 승인된 템플릿 코드
        Dictionary<string, string> variables,
        bool fallbackSms = true)
    {
        // 알림톡 발송 → 실패 시 SMS fallback
    }
}
```

### 알림톡 템플릿 관리 규칙
```
src/Infrastructure/Notifications/Templates/Kakao/
├── templates.json     # 템플릿 코드 ↔ 내용 매핑 (승인된 것만)
```

```json
// templates.json 예시
{
  "BID_DEADLINE_D3": {
    "templateCode": "BMS_BID_001",
    "content": "[#{회사명}] 입찰 마감 알림\n공고명: #{공고명}\n마감일: #{마감일}\n서류 준비를 확인해주세요."
  },
  "CERT_EXPIRY_30D": {
    "templateCode": "BMS_CERT_001",
    "content": "[#{회사명}] 자격증 만료 예정\n#{기술인명}님의 #{자격증명}이 #{만료일}에 만료됩니다.\n갱신 교육을 확인해주세요."
  }
}
```

---

## 알림 트리거 목록 (BMS 도메인)

| 알림 유형 | 트리거 조건 | 채널 | 수신자 |
|:---------|:-----------|:-----|:------|
| 입찰 마감 D-3 | 마감일 기준 3일 전 | 알림톡 + 이메일 | 입찰 담당자 |
| 입찰 마감 D-1 | 마감일 기준 1일 전 | 알림톡 | 입찰 담당자 |
| 자격증 만료 D-30 | 만료일 30일 전 | 이메일 | 기술인 본인 + 관리자 |
| 자격증 만료 D-7 | 만료일 7일 전 | 알림톡 | 기술인 본인 + 관리자 |
| 현장 배치 확정 | 배치 등록 시 즉시 | 알림톡 | 배치된 기술인 |
| 월간보고서 제출 | 매월 25일 | 이메일 | 현장 담당 기술인 |
| 계속교육 만료 | 교육 만료 D-60 | 이메일 | 기술인 본인 |
| 하자보수 기간 만료 | 만료 D-30 | 이메일 | 현장 PM |

---

## n8n 스케줄 연동

알림은 n8n Schedule Trigger → .NET API Webhook 방식으로 실행:

```
n8n Schedule (매일 09:00)
  → POST /api/v1/notifications/trigger
  → NotificationTriggerCommand { TriggerType: "DailyCheck" }
  → Application: 오늘 발송할 알림 목록 조회 후 일괄 발송
```

직접 n8n에서 이메일/알림톡 발송 금지 — 반드시 .NET API를 통해 발송 (감사 로그 보장).

---

## Secret 관리

```bash
# 개발 환경
dotnet user-secrets set "BizppurioApiKey" "..."
dotnet user-secrets set "BizppurioSenderId" "..."
dotnet user-secrets set "Email:SenderAddress" "no-reply@company.com"
dotnet user-secrets set "Email:ConnectionString" "endpoint=https://..."
```

```
# Azure Key Vault (운영)
BizppurioApiKey
BizppurioSenderId
Email--SenderAddress
Email--ConnectionString
```
