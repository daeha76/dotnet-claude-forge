using Daeha.CleanArch.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Daeha.CleanArch.Application.Extensions;

/// <summary>Application 레이어 DI 등록 확장 메서드</summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Application 레이어 서비스 등록.
    /// MediatR 파이프라인 순서: Logging → Validation → Authorization → Transaction → Handler
    /// </summary>
    /// <param name="services">DI 컨테이너</param>
    /// <param name="applicationAssembly">Command/Query/Validator가 정의된 어셈블리</param>
    public static IServiceCollection AddCleanArchApplication(
        this IServiceCollection services,
        System.Reflection.Assembly applicationAssembly)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);

            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });

        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        return services;
    }
}
