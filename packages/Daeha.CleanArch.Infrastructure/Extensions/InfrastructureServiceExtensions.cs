using Daeha.CleanArch.Application.Common.Interfaces;
using Daeha.CleanArch.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Daeha.CleanArch.Infrastructure.Extensions;

/// <summary>Infrastructure 레이어 DI 등록 확장 메서드</summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Infrastructure 레이어 서비스 등록.
    /// AppDbContext 등록, IUnitOfWork 바인딩, ICurrentUserService 등록을 수행.
    /// </summary>
    /// <typeparam name="TContext">프로젝트의 AppDbContext 타입</typeparam>
    /// <param name="services">DI 컨테이너</param>
    /// <param name="configuration">appsettings 설정</param>
    /// <param name="connectionStringName">Connection String 이름 (기본: DefaultConnection)</param>
    public static IServiceCollection AddCleanArchInfrastructure<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "DefaultConnection")
        where TContext : DbContext
    {
        var connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{connectionStringName}'이(가) 설정되지 않았습니다.");

        services.AddDbContext<TContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        // IUnitOfWork → TContext (같은 Scoped 인스턴스)
        services.AddScoped<IUnitOfWork>(sp => (IUnitOfWork)sp.GetRequiredService<TContext>());

        // ICurrentUserService → HttpContext JWT 파싱
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}
