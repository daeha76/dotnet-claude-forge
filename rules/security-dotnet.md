# Security: .NET 10 + Supabase Auth

## 인증 흐름: JWT → HttpOnly Cookie

**JWT를 localStorage에 저장 금지** (XSS 취약점). **HttpOnly Cookie 방식** 사용:

```
① Blazor → .NET API /auth/login 호출
② .NET API → Supabase Auth로 로그인 요청
③ Supabase가 JWT 발급 → .NET API가 HttpOnly Cookie로 설정
④ 이후 모든 요청: 브라우저가 Cookie 자동 전송
   - SSR 모드: 서버가 Cookie 자동 수신 ✅
   - WASM 모드: 브라우저가 Cookie 자동 전송 ✅
```

```csharp
// .NET API — 로그인 시 HttpOnly Cookie 설정
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest req)
{
    var session = await supabaseClient.Auth.SignIn(req.Email, req.Password);
    Response.Cookies.Append("auth-token", session.AccessToken, new CookieOptions
    {
        HttpOnly = true,    // JS 접근 불가 (XSS 방어)
        Secure = true,      // HTTPS에서만 전송
        SameSite = SameSiteMode.Strict,
        Expires = DateTimeOffset.UtcNow.AddHours(1)
    });
    return Ok();
}
```

```csharp
// Program.cs — Cookie에서 JWT 읽어 검증
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://{supabaseProjectRef}.supabase.co/auth/v1";
        options.Audience = "authenticated";
        // Cookie에서 토큰 추출
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                ctx.Token = ctx.Request.Cookies["auth-token"];
                return Task.CompletedTask;
            }
        };
    });

app.UseAuthentication();
app.UseAuthorization();
```

**Blazor Auto는 절대 Supabase에 직접 JWT 요청하지 않음** — 반드시 .NET API를 통해 인증.

## Secret 관리

```csharp
// NEVER: appsettings.json에 시크릿 직접 기입
{
  "Supabase": {
    "ServiceKey": "eyJhbGciOiJ..." // ❌ 절대 금지
  }
}

// CORRECT: 환경별 Secret 관리
// - Development: User Secrets (dotnet user-secrets set "Supabase:ServiceKey" "...")
// - Staging/Production: Azure Key Vault
```

Azure Key Vault 연동:

```csharp
// Program.cs
if (!builder.Environment.IsDevelopment())
{
    builder.Configuration.AddAzureKeyVault(
        new Uri($"https://{keyVaultName}.vault.azure.net/"),
        new DefaultAzureCredential()
    );
}
```

## CORS 엄격 설정

```csharp
// WRONG: Wildcard 허용
app.UseCors(policy => policy.AllowAnyOrigin()); // ❌

// CORRECT: 명시적 Origin만 허용
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        policy.WithOrigins(
                configuration["Cors:AllowedOrigins"]!.Split(','))
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
```

## HTTPS 강제

```csharp
// 개발 환경을 제외한 모든 환경에서 HTTPS 리다이렉트
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}
```

## 입력 검증 (FluentValidation + MediatR Pipeline)

```csharp
// ValidationBehavior: 모든 Command/Query 진입 전 자동 검증
// ValidationException은 아래 ExceptionHandler 미들웨어가 Problem Details로 변환
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var failures = _validators
            .Select(v => v.Validate(request))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Any())
            throw new ValidationException(failures);

        return await next();
    }
}
```

### ValidationException → Problem Details 변환 (필수)

`ValidationBehavior`가 throw한 `ValidationException`을 클라이언트가 받을 수 있는 **Problem Details (RFC 7807)** 형태로 변환해야 합니다.
`architecture-dotnet.md`의 Result 패턴 원칙에 따라 클라이언트는 항상 일관된 에러 포맷을 받아야 합니다:

```csharp
// Program.cs — ValidationException을 400 Problem Details로 변환
builder.Services.AddProblemDetails();

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        var (status, title, errors) = exception switch
        {
            ValidationException ve => (400, "Validation Failed",
                ve.Errors.GroupBy(e => e.PropertyName)
                         .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
            _ => (500, "Internal Server Error", (Dictionary<string, string[]>?)null)
        };

        context.Response.StatusCode = status;
        await Results.Problem(
            title: title,
            statusCode: status,
            extensions: errors is not null
                ? new Dictionary<string, object?> { ["errors"] = errors }
                : null
        ).ExecuteAsync(context);
    });
});

app.UseAuthentication();
app.UseAuthorization();
```

클라이언트가 받는 응답:
```json
{
  "title": "Validation Failed",
  "status": 400,
  "errors": {
    "CustomerId": ["고객 ID가 필요합니다."],
    "Items": ["주문 항목이 없습니다."]
  }
}
```

## 멀티테넌트 RBAC (회사/부서/개인 권한)

### 권한 계층 구조

```
Tenant (회사)                    ← 최상위 격리 단위
  └── Department (부서)
        └── User
              └── Role           ← 역할별 권한 집합
```

### 역할 정의

| Role | 권한 범위 |
|:-----|:---------|
| `system_admin` | 모든 테넌트 접근, 시스템 설정 변경 |
| `company_admin` | 자사 테넌트 전체, 부서·사용자 역할 조정 |
| `dept_manager` | 소속 부서 데이터, 부서원 권한 일부 조정 |
| `member` | 본인 데이터 + 부서 공유 데이터 (읽기) |

### DB 스키마

```sql
CREATE TABLE tenants (
    id          UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    name        TEXT NOT NULL,
    created_at  TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE departments (
    id          UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id   UUID NOT NULL REFERENCES tenants(id),
    name        TEXT NOT NULL
);

CREATE TABLE user_roles (
    user_id       UUID NOT NULL REFERENCES auth.users(id),
    tenant_id     UUID NOT NULL REFERENCES tenants(id),
    department_id UUID REFERENCES departments(id), -- NULL이면 회사 전체
    role          TEXT NOT NULL CHECK (role IN ('system_admin','company_admin','dept_manager','member')),
    PRIMARY KEY (user_id, tenant_id)
);
```

### RLS — tenant_id 기반 격리

```sql
-- 모든 업무 테이블에 tenant_id 컬럼 추가 후 RLS 적용
ALTER TABLE orders ENABLE ROW LEVEL SECURITY;

-- 백엔드 서비스 키는 BYPASS RLS — 앱 레벨에서 tenant 필터 적용
-- (직접 Supabase 접근 없으므로 auth.uid() 정책 불필요)
```

### .NET Authorization — MediatR 파이프라인

```csharp
// 권한 확인 Behavior
public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAuthorizedRequest
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var userId = _currentUser.Id;
        var tenantId = _currentUser.TenantId;
        var role = await _roleService.GetRoleAsync(userId, tenantId, ct);

        if (!request.RequiredRoles.Contains(role))
            return Result.Failure<TResponse>("권한이 없습니다.") as TResponse
                   ?? throw new ForbiddenException();

        return await next();
    }
}

// Command에 권한 명시
public record RestoreRecordCommand(...) : IRequest<Result>, IAuthorizedRequest
{
    public IReadOnlyList<string> RequiredRoles => ["system_admin", "company_admin"];
}
```

### API Controller — 역할 기반 접근 제어

```csharp
// 시스템 관리자 전용
[Authorize(Roles = "system_admin")]
[HttpGet("admin/tenants")]
public async Task<IActionResult> GetAllTenants() { ... }

// 회사 관리자 이상
[Authorize(Roles = "system_admin,company_admin")]
[HttpPost("roles")]
public async Task<IActionResult> AssignRole([FromBody] AssignRoleCommand cmd) { ... }

// 부서 관리자 이상
[Authorize(Roles = "system_admin,company_admin,dept_manager")]
[HttpGet("departments/{deptId}/members")]
public async Task<IActionResult> GetDeptMembers(Guid deptId) { ... }
```

### 현재 사용자 컨텍스트 서비스

```csharp
public interface ICurrentUserService
{
    Guid Id { get; }
    Guid TenantId { get; }
    Guid? DepartmentId { get; }
    string Role { get; }
    bool IsSystemAdmin { get; }
    bool IsCompanyAdmin { get; }
}
```

모든 Query/Command Handler에서 `ICurrentUserService`로 테넌트 필터 적용 — Raw SQL에 tenant_id 직접 삽입 금지.

## 보안 체크리스트 (커밋 전 필수)

- [ ] `appsettings.json`에 시크릿 없음 (API Key, Password, Token)
- [ ] 모든 API 엔드포인트에 `[Authorize]` 또는 `[AllowAnonymous]` 명시
- [ ] SQL은 EF Core 또는 파라미터화 쿼리만 사용 (Raw string SQL 금지)
- [ ] 에러 응답에 스택 트레이스 미포함 (Production 환경)
- [ ] Rate Limiting 설정 확인
- [ ] CORS Origins가 wildcard `*` 아님
- [ ] `ValidationException` → Problem Details 변환 미들웨어 등록 확인 (`UseExceptionHandler`)
- [ ] 모든 Query/Command Handler에서 `tenant_id` 필터 적용 확인 (테넌트 간 데이터 누출 방지)
- [ ] Restore/Delete 등 파괴적 작업에 `[Authorize(Roles = "system_admin,company_admin")]` 적용
- [ ] `ICurrentUserService`를 통해 현재 사용자 역할 주입 (하드코딩 금지)
