#!/usr/bin/env dotnet-script
// nuget-installer.csx — NuGet 패키지 설치 (모드 2 하위 모듈)
#nullable enable

using System;
using System.IO;

void InstallNuGetPackages(string root, string name)
{
    Print("");
    Print("  NuGet 패키지 설치 중...", ConsoleColor.White);

    // 패키지 정의 테이블: (프로젝트경로, 패키지명[])
    var targets = new (string ProjDir, string[] Packages)[]
    {
        (
            Path.Combine(root, "src", $"{name}.Domain"),
            new[]
            {
                // DDD 기반 클래스 (Result, BaseEntity, AggregateRoot, ValueObject 등)
                "Daeha.CleanArch.Domain",
            }
        ),
        (
            Path.Combine(root, "src", $"{name}.Application"),
            new[]
            {
                // CQRS Behaviors, ICurrentUserService, IUnitOfWork, DI 확장
                // (FluentValidation + MediatR 전이적 포함)
                "Daeha.CleanArch.Application",
                "FluentValidation",
                "FluentValidation.DependencyInjectionExtensions",
                "MediatR",
            }
        ),
        (
            Path.Combine(root, "src", $"{name}.Infrastructure"),
            new[]
            {
                // BaseDbContext, CurrentUserService, AddCleanArchInfrastructure<T>
                // (EFCore.NamingConventions, Npgsql 전이적 포함)
                "Daeha.CleanArch.Infrastructure",
                "Supabase",
                "Microsoft.EntityFrameworkCore.Design",
                "Microsoft.EntityFrameworkCore.Tools",
            }
        ),
        (
            Path.Combine(root, "src", $"{name}.Api"),
            new[]
            {
                // AddAzureKeyVault, AddSerilogForge, AddJwtBearerFromCookie,
                // AddDataProtectionAzure, UseValidationExceptionHandler, ApiKeyAttribute
                "Daeha.CleanArch.Api",
                "Microsoft.AspNetCore.OpenApi",
                "Swashbuckle.AspNetCore",
            }
        ),
        (
            Path.Combine(root, "tests", $"{name}.Domain.Tests"),
            new[] { "NSubstitute", "FluentAssertions" }
        ),
        (
            Path.Combine(root, "tests", $"{name}.Application.Tests"),
            new[] { "NSubstitute", "FluentAssertions" }
        ),
    };

    foreach (var (projDir, packages) in targets)
    {
        var label = Path.GetFileName(projDir);
        foreach (var pkg in packages)
        {
            Run("dotnet", $"add package {pkg}", projDir);
            Print($"  [OK] {label} ← {pkg}", ConsoleColor.DarkGray);
        }
    }

    Print("  [OK] NuGet 패키지 설치 완료", ConsoleColor.Green);
}
