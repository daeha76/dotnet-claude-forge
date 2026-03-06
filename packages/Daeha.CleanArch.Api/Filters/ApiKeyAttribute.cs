using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Daeha.CleanArch.Api.Filters;

/// <summary>
/// n8n Webhook 전용 API Key 인증 필터.
/// JWT 인증 없이 X-Api-Key 헤더로 인증. n8n → .NET API Webhook 엔드포인트에만 사용.
/// </summary>
/// <remarks>
/// 설정 키: <c>N8n:WebhookSecret</c> (개발: User Secrets, 운영: Azure Key Vault)
/// </remarks>
/// <example>
/// <code>
/// [HttpPost("g2b-notices")]
/// [ApiKey]
/// public async Task&lt;IActionResult&gt; ReceiveG2bNotices([FromBody] G2bNoticePayload[] notices)
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ApiKeyAttribute : Attribute, IAsyncActionFilter
{
    private const string ApiKeyHeader = "X-Api-Key";

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeader, out var providedKey))
        {
            context.Result = new UnauthorizedObjectResult($"{ApiKeyHeader} 헤더가 필수입니다.");
            return;
        }

        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expectedKey = config["N8n:WebhookSecret"];

        if (string.IsNullOrEmpty(expectedKey) || providedKey != expectedKey)
        {
            context.Result = new UnauthorizedObjectResult("유효하지 않은 API Key입니다.");
            return;
        }

        await next();
    }
}
