using Azure.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;

namespace Daeha.CleanArch.Api.Extensions;

/// <summary>API 레이어 DI 등록 확장 메서드 모음</summary>
public static class ApiServiceExtensions
{
    /// <summary>
    /// Azure Key Vault 연동 설정. Production/Staging 환경에서만 활성화.
    /// appsettings에 <c>Azure:KeyVaultUri</c> 설정 필요.
    /// </summary>
    public static WebApplicationBuilder AddAzureKeyVault(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment())
        {
            var keyVaultUri = builder.Configuration["Azure:KeyVaultUri"];
            if (!string.IsNullOrEmpty(keyVaultUri))
            {
                builder.Configuration.AddAzureKeyVault(
                    new Uri(keyVaultUri),
                    new DefaultAzureCredential());
            }
        }
        return builder;
    }

    /// <summary>
    /// Serilog 설정. Console + ApplicationInsights + n8n Webhook (URL 설정 시에만 활성화).
    /// </summary>
    /// <remarks>
    /// n8n Webhook URL은 <c>Serilog:N8nWebhookUrl</c> 설정 키로 지정.
    /// 미설정 시 Http Sink 자체가 등록되지 않으므로 크래시 없음.
    /// </remarks>
    public static WebApplicationBuilder AddSerilogForge(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((ctx, services, lc) =>
        {
            lc.ReadFrom.Configuration(ctx.Configuration)
              .WriteTo.Console();

            // n8n Webhook — URL이 설정된 환경에서만 활성화
            var webhookUrl = ctx.Configuration["Serilog:N8nWebhookUrl"];
            if (!string.IsNullOrEmpty(webhookUrl))
            {
                lc.WriteTo.Http(
                    requestUri: webhookUrl,
                    queueLimitBytes: null,
                    restrictedToMinimumLevel: LogEventLevel.Error);
            }
        });
        return builder;
    }

    /// <summary>
    /// JWT Bearer 인증 설정 (HttpOnly Cookie에서 토큰 추출).
    /// Supabase JWT 검증을 위해 <c>Jwt:Authority</c>, <c>Jwt:Audience</c> 설정 필요.
    /// </summary>
    /// <remarks>
    /// 기본값: Authority = https://{supabase-ref}.supabase.co/auth/v1, Audience = authenticated
    /// </remarks>
    public static IServiceCollection AddJwtBearerFromCookie(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Jwt:Authority"];
                options.Audience = configuration["Jwt:Audience"] ?? "authenticated";
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                };
                // HttpOnly Cookie에서 JWT 추출
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        ctx.Token = ctx.Request.Cookies["auth-token"];
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    /// <summary>
    /// Azure Blob + Key Vault 기반 Data Protection 설정.
    /// Scale-out 시 인스턴스 간 암호화 키 공유. 서버 1대여도 동일하게 적용.
    /// <c>Azure:DataProtectionBlobUri</c>, <c>Azure:DataProtectionKeyId</c> 설정 필요.
    /// </summary>
    public static IServiceCollection AddDataProtectionAzure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            // 개발 환경: 기본 Data Protection (로컬 키)
            services.AddDataProtection();
            return services;
        }

        var blobUri = configuration["Azure:DataProtectionBlobUri"]
            ?? throw new InvalidOperationException("Azure:DataProtectionBlobUri 설정이 필요합니다.");
        var keyId = configuration["Azure:DataProtectionKeyId"]
            ?? throw new InvalidOperationException("Azure:DataProtectionKeyId 설정이 필요합니다.");

        services.AddDataProtection()
            .PersistKeysToAzureBlobStorage(new Uri(blobUri))
            .ProtectKeysWithAzureKeyVault(new Uri(keyId), new DefaultAzureCredential());

        return services;
    }

    /// <summary>
    /// ValidationException → 400 Problem Details 변환 미들웨어 등록.
    /// FluentValidation의 ValidationException을 RFC 7807 형식으로 변환.
    /// </summary>
    public static IApplicationBuilder UseValidationExceptionHandler(this WebApplication app)
    {
        app.UseExceptionHandler(exceptionHandlerApp =>
        {
            exceptionHandlerApp.Run(async context =>
            {
                var exceptionFeature = context.Features
                    .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                var exception = exceptionFeature?.Error;

                switch (exception)
                {
                    case ValidationException ve:
                    {
                        var errors = ve.Errors
                            .GroupBy(e => e.PropertyName)
                            .ToDictionary(
                                g => g.Key,
                                g => g.Select(e => e.ErrorMessage).ToArray());

                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await Results.Problem(
                            title: "Validation Failed",
                            statusCode: StatusCodes.Status400BadRequest,
                            extensions: new Dictionary<string, object?> { ["errors"] = errors }
                        ).ExecuteAsync(context);
                        break;
                    }
                    case UnauthorizedAccessException:
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await Results.Problem(
                            title: "Forbidden",
                            statusCode: StatusCodes.Status403Forbidden
                        ).ExecuteAsync(context);
                        break;
                    default:
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        await Results.Problem(
                            title: "Internal Server Error",
                            statusCode: StatusCodes.Status500InternalServerError
                        ).ExecuteAsync(context);
                        break;
                }
            });
        });

        return app;
    }
}
