# Shared: n8n 워크플로우 연동 패턴

## 역할 분담 원칙

```
n8n 역할: 스케줄/트리거/외부시스템 크롤링
.NET API 역할: 비즈니스 로직 처리, DB 저장, 감사 로그

n8n → .NET API (Webhook) → 비즈니스 처리
.NET API → n8n (Webhook 호출) → 외부 알림/연동
```

**n8n에서 직접 DB 접근 금지** — 반드시 .NET API Webhook 엔드포인트를 통해 데이터 처리.

---

## .NET API Webhook 엔드포인트 규칙

```csharp
// Api/Controllers/WebhooksController.cs
[ApiController]
[Route("api/v1/webhooks")]
public class WebhooksController : ControllerBase
{
    /// <summary>n8n 스케줄 트리거: 일일 알림 체크</summary>
    [HttpPost("daily-notification-check")]
    [ApiKey]   // n8n 전용 API Key 인증 (JWT 아님)
    public async Task<IActionResult> DailyNotificationCheck()

    /// <summary>n8n → 나라장터 공고 데이터 수신</summary>
    [HttpPost("g2b-notices")]
    [ApiKey]
    public async Task<IActionResult> ReceiveG2bNotices([FromBody] G2bNoticePayload[] notices)

    /// <summary>n8n → 외부 시스템 동기화 완료 알림</summary>
    [HttpPost("sync-completed")]
    [ApiKey]
    public async Task<IActionResult> SyncCompleted([FromBody] SyncCompletedPayload payload)
}
```

### API Key 인증 (n8n 전용)

```csharp
// n8n은 JWT 인증 없이 API Key로 인증
// Header: X-Api-Key: {n8n-webhook-secret}
public class ApiKeyAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var key)
            || key != context.HttpContext.RequestServices
                .GetRequiredService<IConfiguration>()["N8n:WebhookSecret"])
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        await next();
    }
}
```

---

## BMS 주요 n8n 워크플로우 목록

### 1. 나라장터(G2B) 공고 수집 [Schedule: 매일 09:00, 14:00]
```
n8n Schedule Trigger
  → HTTP Request: 나라장터 API (또는 스크래핑)
  → Filter: 건설사업관리/감리 관련 공고
  → HTTP Request: POST /api/v1/webhooks/g2b-notices
  → BMS가 신규 공고 분석 후 담당자 알림 발송
```

### 2. 일일 알림 체크 [Schedule: 매일 08:30]
```
n8n Schedule Trigger
  → HTTP Request: POST /api/v1/webhooks/daily-notification-check
  → BMS가 처리:
    - 자격증 만료 D-7, D-30 알림
    - 입찰 마감 D-1, D-3 알림
    - 계속교육 만료 D-60 알림
    - 하자보수 기간 만료 D-30 알림
```

### 3. 에러 알림 수신 [Webhook: 항상 대기]
```
Serilog HTTP Sink (Error 이상)
  → n8n Webhook 수신
  → n8n이 Slack/Teams 알림 발송
  (비즈니스 로직 없음 — 순수 알림 중계)
```

### 4. 월간 보고서 리마인더 [Schedule: 매월 25일]
```
n8n Schedule Trigger
  → HTTP Request: POST /api/v1/webhooks/daily-notification-check
    { TriggerType: "MonthlyReportReminder" }
  → BMS가 현장별 담당자에게 알림 발송
```

---

## n8n 서버 설정

### 자체 호스팅 (권장 — 비용 절감)
```yaml
# docker-compose.yml (n8n 전용 서버 또는 동일 서버 사이드카)
services:
  n8n:
    image: n8nio/n8n
    environment:
      - N8N_HOST=n8n.your-domain.com
      - N8N_PORT=5678
      - N8N_PROTOCOL=https
      - WEBHOOK_URL=https://n8n.your-domain.com/
      - N8N_BASIC_AUTH_ACTIVE=true
      - N8N_BASIC_AUTH_USER=${N8N_USER}
      - N8N_BASIC_AUTH_PASSWORD=${N8N_PASSWORD}
    volumes:
      - n8n_data:/home/node/.n8n
    ports:
      - "5678:5678"
```

### n8n 접근 제어
- 외부 인터넷에서 n8n UI 접근 제한 (VPN 또는 IP 화이트리스트)
- Webhook 엔드포인트는 공개 접근 허용 (n8n → .NET API 호출)
- n8n → .NET API 호출 시 `X-Api-Key` 헤더 필수

---

## Secret 관리

```bash
# .NET API — n8n 검증용
dotnet user-secrets set "N8n:WebhookSecret" "..."

# n8n 환경변수
N8N_USER=admin
N8N_PASSWORD=...  # 강력한 패스워드
```

```
# Azure Key Vault
N8n--WebhookSecret
```

---

## 워크플로우 버전 관리

n8n 워크플로우 JSON 파일은 **코드 저장소에 포함**:

```
.n8n-workflows/
├── g2b-notice-collector.json
├── daily-notification-check.json
├── error-alert.json
└── monthly-report-reminder.json
```

워크플로우 변경 시 JSON export → commit → 다른 환경에서 import.
