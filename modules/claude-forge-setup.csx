#!/usr/bin/env dotnet-script
// claude-forge-setup.csx — .claude/ 복사 + CLAUDE.md + nuget.config 생성 (모드 2 하위 모듈)
#nullable enable

using System;
using System.IO;
using System.Text;

void SetupClaudeForge(string root, string name, string repoDir)
{
    Print("");
    Print("  Claude Forge 기능 복사 중...", ConsoleColor.White);

    var projectClaudeDir = Path.Combine(root, ".claude");
    Directory.CreateDirectory(projectClaudeDir);

    // 디렉토리 복사
    var forgeDirs = new[] { "agents", "rules", "commands", "skills", "hooks" };
    foreach (var d in forgeDirs)
    {
        var src = Path.Combine(repoDir, d);
        var dst = Path.Combine(projectClaudeDir, d);
        if (!Directory.Exists(src)) continue;
        if (Directory.Exists(dst)) Directory.Delete(dst, recursive: true);
        CopyDirectory(src, dst);
        Print($"  [OK] .claude/{d}/", ConsoleColor.DarkGray);
    }

    // 설정 파일 복사
    foreach (var f in new[] { "settings.json", "hooks.json" })
    {
        var src = Path.Combine(repoDir, f);
        var dst = Path.Combine(projectClaudeDir, f);
        if (!File.Exists(src)) continue;
        File.Copy(src, dst, overwrite: true);
        Print($"  [OK] .claude/{f}", ConsoleColor.DarkGray);
    }

    // nuget.config — GitHub Packages 피드 (Daeha.CleanArch.* 패키지)
    // 템플릿 파일이 있으면 복사, 없으면 직접 생성
    var nugetTemplate = Path.Combine(repoDir, "setup", "nuget.config.template");
    var nugetDst = Path.Combine(root, "nuget.config");
    if (File.Exists(nugetTemplate))
    {
        File.Copy(nugetTemplate, nugetDst, overwrite: true);
    }
    else
    {
        File.WriteAllText(nugetDst, """
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <!-- Daeha.CleanArch.* 패키지: GitHub Packages
         GITHUB_TOKEN 환경 변수 또는 dotnet user-secrets로 인증 설정 필요.
         설정 방법: https://docs.github.com/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry -->
    <add key="github-daeha" value="https://nuget.pkg.github.com/daeha/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github-daeha>
      <add key="Username" value="daeha" />
      <add key="ClearTextPassword" value="%GITHUB_TOKEN%" />
    </github-daeha>
  </packageSourceCredentials>
</configuration>
""", Encoding.UTF8);
    }
    Print("  [OK] nuget.config (GitHub Packages 피드)", ConsoleColor.Green);

    // CLAUDE.md
    File.WriteAllText(Path.Combine(root, "CLAUDE.md"), $"""
# {name}

.NET 10 + Blazor Auto + Supabase Clean Architecture 프로젝트

## 기술 스택
- **언어**: C# / .NET 10
- **프론트엔드**: Blazor Auto
- **백엔드**: .NET API (Clean Architecture)
- **공통 인프라**: Daeha.CleanArch.* NuGet 패키지
- **DB**: Supabase (PostgreSQL)
- **스타일**: Tailwind CSS v4

## 빌드 & 검증 명령어

```bash
aspire run              # Aspire 실행 (TailwindCSS watch 포함)
dotnet build            # 빌드
dotnet test             # 테스트
dotnet format --verify-no-changes
```

## 핵심 디렉토리

```
apphost.cs                 # Aspire 오케스트레이터 (file-based)
src/{name}.Domain/         # 엔터티, 도메인 (Daeha.CleanArch.Domain 상속)
src/{name}.Application/    # 유스케이스 (Daeha.CleanArch.Application 사용)
src/{name}.Infrastructure/ # DB, 외부 서비스 (BaseDbContext<T> 상속)
src/{name}.Api/            # Web API (AddAzureKeyVault 등 확장 메서드 사용)
src/{name}.Web/            # Blazor Auto
tests/                     # 단위·통합 테스트
.claude/                   # Claude Forge 기능 (agents, rules, commands, skills)
```

## Daeha.CleanArch 패키지 구성

| 패키지 | 제공 기능 |
|--------|----------|
| `Daeha.CleanArch.Domain` | Result, BaseEntity, AggregateRoot, ValueObject, DomainEvent |
| `Daeha.CleanArch.Application` | LoggingBehavior, ValidationBehavior, AuthorizationBehavior, ICurrentUserService |
| `Daeha.CleanArch.Infrastructure` | BaseDbContext<T>, CurrentUserService, AddCleanArchInfrastructure<T>() |
| `Daeha.CleanArch.Api` | AddAzureKeyVault, AddJwtBearerFromCookie, AddDataProtectionAzure, ApiKeyAttribute |

## Infrastructure 설정 패턴

```csharp
// AppDbContext.cs — BaseDbContext 상속으로 보일러플레이트 제거
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUser)
    : BaseDbContext<AppDbContext>(options, currentUser)
{{
    public DbSet<MyEntity> MyEntities => Set<MyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {{
        base.OnModelCreating(modelBuilder); // 글로벌 필터(테넌트/소프트삭제) 적용
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }}
}}
```
""", Encoding.UTF8);

    Print("  [OK] CLAUDE.md", ConsoleColor.Green);
    Print("  [OK] Claude Forge 기능 복사 완료", ConsoleColor.Green);
}
