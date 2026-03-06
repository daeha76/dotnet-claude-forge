#!/usr/bin/env dotnet-script
// project-scaffolder.csx — .NET Clean Architecture 프로젝트 초기화 오케스트레이터 (모드 2)
//
// 의존 모듈 (install.csx에서 #load):
//   modules/nuget-installer.csx
//   modules/tailwind-setup.csx
//   modules/claude-forge-setup.csx
#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;

void InitDotNetProject(string name, string baseDir)
{
    Print($"  [모드 2] .NET 프로젝트 초기화: {name}", ConsoleColor.Cyan);
    Print("");

    // ── 사전 확인 ────────────────────────────────────────────────────────
    if (!CommandExists("dotnet"))
    {
        Print("  [오류] .NET SDK 미설치", ConsoleColor.Red);
        Print("         설치: https://dot.net/download", ConsoleColor.Yellow);
        Environment.Exit(1);
    }
    Print($"  [OK] .NET SDK: {RunCapture("dotnet", "--version").Trim()}", ConsoleColor.Green);

    if (!CommandExists("git"))
    {
        Print("  [오류] git 미설치", ConsoleColor.Red);
        Environment.Exit(1);
    }

    // ── 프로젝트 루트 ─────────────────────────────────────────────────────
    var root = Path.Combine(baseDir, name);
    if (Directory.Exists(root))
    {
        Print($"  [경고] 폴더가 이미 존재합니다: {root}", ConsoleColor.Yellow);
        Console.Write("  계속할까요? (y/n): ");
        if (Console.ReadLine()?.Trim().ToLower() != "y") return;
    }
    else
    {
        Directory.CreateDirectory(root);
        Print($"  [OK] 폴더 생성: {root}", ConsoleColor.Green);
    }

    // ── 솔루션 + 프로젝트 생성 ────────────────────────────────────────────
    Print("");
    Print("  .NET Clean Architecture 솔루션 생성 중...", ConsoleColor.White);
    CreateSolutionAndProjects(root, name);

    // ── NuGet 패키지 ──────────────────────────────────────────────────────
    InstallNuGetPackages(root, name);

    // ── TailwindCSS + apphost.cs ──────────────────────────────────────────
    SetupTailwind(root, name, repoDir);

    // ── API Program.cs ────────────────────────────────────────────────────
    SetupApiProgram(root, name);

    // ── 글로벌 파일들 ─────────────────────────────────────────────────────
    CreateGlobalFiles(root, name);

    // ── Claude Forge 기능 복사 ────────────────────────────────────────────
    SetupClaudeForge(root, name, repoDir);

    // ── git init ──────────────────────────────────────────────────────────
    Print("");
    Print("  git 초기화 중...", ConsoleColor.White);
    Run("git", "init", root);
    Run("git", "add .", root);
    Run("git", $"commit -m \"chore: initial .NET Clean Architecture scaffold for {name}\"", root);
    Print("  [OK] git init + 첫 커밋 완료", ConsoleColor.Green);

    // ── 완료 메시지 ───────────────────────────────────────────────────────
    Print("");
    Print("  ╔══════════════════════════════════════════════════╗", ConsoleColor.Green);
    Print($"  ║   {name} 프로젝트 생성 완료!", ConsoleColor.Green);
    Print("  ╠══════════════════════════════════════════════════╣", ConsoleColor.Green);
    Print("  ║  5 src · 2 tests · apphost.cs · Claude Forge 포함 ║", ConsoleColor.Green);
    Print("  ╚══════════════════════════════════════════════════╝", ConsoleColor.Green);
    Print("");
    Print("  다음 단계:", ConsoleColor.Cyan);
    Print($"    cd \"{root}\"");
    Print("    aspire run             # Aspire 실행");
    Print("    dotnet build           # 빌드 확인");
    Print("    claude                 # Claude Code + Forge 사용 가능");
    Print("");
    Print("  ★ TailwindCSS v4 설정 완료 — aspire run 시 watch 모드 자동 시작", ConsoleColor.Yellow);
    Print("    Tailwind CLI 문서: https://tailwindcss.com/docs/installation/tailwind-cli", ConsoleColor.DarkGray);
    Print("  ★ Swagger UI: Aspire 대시보드에서 api 서비스 URL 확인 후 /swagger 접속", ConsoleColor.Yellow);
    Print("");
    Print($"  프로젝트 위치: {root}", ConsoleColor.Yellow);
    Run("antigravity", root);
}

// ── 솔루션 + 프로젝트 생성 (내부 헬퍼) ─────────────────────────────────────
void CreateSolutionAndProjects(string root, string name)
{
    Run("dotnet", $"new sln --format slnx -n {name} --output .", root);
    Print($"  [OK] {name}.slnx", ConsoleColor.Green);

    var srcProjects = new (string Template, string Extra, string ProjName, string Dir)[]
    {
        ("classlib", "",                               $"{name}.Domain",         Path.Combine("src", $"{name}.Domain")),
        ("classlib", "",                               $"{name}.Application",    Path.Combine("src", $"{name}.Application")),
        ("classlib", "",                               $"{name}.Infrastructure", Path.Combine("src", $"{name}.Infrastructure")),
        ("webapi",   "",                               $"{name}.Api",            Path.Combine("src", $"{name}.Api")),
        ("blazor",   "--interactivity Auto --empty",   $"{name}.Web",            Path.Combine("src", $"{name}.Web")),
    };
    var testProjects = new (string Template, string Extra, string ProjName, string Dir)[]
    {
        ("xunit", "", $"{name}.Domain.Tests",      Path.Combine("tests", $"{name}.Domain.Tests")),
        ("xunit", "", $"{name}.Application.Tests", Path.Combine("tests", $"{name}.Application.Tests")),
    };

    foreach (var (tmpl, extra, proj, dir) in srcProjects.Concat(testProjects))
    {
        var fullDir = Path.Combine(root, dir);
        Directory.CreateDirectory(fullDir);
        Run("dotnet", $"new {tmpl} -n {proj} --output . --force {extra}".Trim(), fullDir);

        if (tmpl == "blazor")
        {
            // Blazor Auto 자동 생성 .sln/.slnx 삭제 (루트 솔루션과 충돌 방지)
            foreach (var f in Directory.GetFiles(fullDir, "*.sln",  SearchOption.AllDirectories)) File.Delete(f);
            foreach (var f in Directory.GetFiles(fullDir, "*.slnx", SearchOption.AllDirectories)) File.Delete(f);
            Run("dotnet", $"sln add {dir}/{proj}", root);
            Run("dotnet", $"sln add {dir}/{proj}.Client", root);
        }
        else
        {
            Run("dotnet", $"sln add {dir}", root);
        }

        var class1 = Path.Combine(fullDir, "Class1.cs");
        if (File.Exists(class1)) File.Delete(class1);

        Print($"  [OK] {proj}", ConsoleColor.Green);
    }

    // Clean Architecture 참조 설정
    Print("");
    Print("  프로젝트 참조 연결 중...", ConsoleColor.White);
    var refs = new (string From, string To)[]
    {
        ($"src/{name}.Application",            $"src/{name}.Domain"),
        ($"src/{name}.Infrastructure",         $"src/{name}.Application"),
        ($"src/{name}.Api",                    $"src/{name}.Infrastructure"),
        ($"src/{name}.Api",                    $"src/{name}.Application"),
        ($"src/{name}.Web/{name}.Web",         $"src/{name}.Application"),
        ($"tests/{name}.Domain.Tests",         $"src/{name}.Domain"),
        ($"tests/{name}.Application.Tests",    $"src/{name}.Application"),
    };
    foreach (var (from, to) in refs)
        Run("dotnet", $"add {from} reference {to}", root);
    Print("  [OK] 참조 연결 완료", ConsoleColor.Green);
}

// ── API Program.cs 설정 (내부 헬퍼) ─────────────────────────────────────────
void SetupApiProgram(string root, string name)
{
    Print("");
    Print("  API Program.cs 구성 중...", ConsoleColor.White);

    var apiDir        = Path.Combine(root, "src", $"{name}.Api");
    var apiProgramPath = Path.Combine(apiDir, "Program.cs");
    var template      = Path.Combine(repoDir, "templates", "api-program.cs.template");
    File.WriteAllText(apiProgramPath,
        File.ReadAllText(template).Replace("{{name}}", name),
        Encoding.UTF8);
    Print("  [OK] API/Program.cs (Swagger + Health Checks + CORS + Problem Details)", ConsoleColor.Green);

    // XML 문서 생성 활성화 (Swagger XML 주석 지원)
    var apiCsprojPath = Path.Combine(apiDir, $"{name}.Api.csproj");
    if (File.Exists(apiCsprojPath))
    {
        var csproj = File.ReadAllText(apiCsprojPath);
        if (!csproj.Contains("GenerateDocumentationFile"))
        {
            csproj = csproj.Replace("</PropertyGroup>",
                "    <GenerateDocumentationFile>true</GenerateDocumentationFile>\n" +
                "    <NoWarn>$(NoWarn);1591</NoWarn>\n" +
                "  </PropertyGroup>");
            File.WriteAllText(apiCsprojPath, csproj, Encoding.UTF8);
            Print("  [OK] API.csproj → XML 문서 생성 활성화", ConsoleColor.Green);
        }
    }
}

// ── 글로벌 파일 생성 (내부 헬퍼) ────────────────────────────────────────────
void CreateGlobalFiles(string root, string name)
{
    // .gitignore
    Run("dotnet", "new gitignore --output . --force", root);
    var gitignorePath = Path.Combine(root, ".gitignore");
    if (File.Exists(gitignorePath))
    {
        File.AppendAllText(gitignorePath, """


# Tailwind CSS generated output (빌드 산출물 — Aspire watch 모드 출력)
**/wwwroot/css/tailwindcss_output.css

# Node.js (Tailwind CSS CLI)
**/node_modules/
**/package-lock.json
""", Encoding.UTF8);
    }
    Print("  [OK] .gitignore", ConsoleColor.Green);

    // global.json
    var dotnetVer  = RunCapture("dotnet", "--version").Trim();
    var globalJson = $"{{\n  \"sdk\": {{\n    \"version\": \"{dotnetVer}\",\n    \"rollForward\": \"latestMinor\"\n  }}\n}}";
    File.WriteAllText(Path.Combine(root, "global.json"), globalJson, Encoding.UTF8);
    Print($"  [OK] global.json (SDK {dotnetVer})", ConsoleColor.Green);

    // Directory.Build.props
    File.WriteAllText(Path.Combine(root, "Directory.Build.props"), """
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
""", Encoding.UTF8);
    Print("  [OK] Directory.Build.props", ConsoleColor.Green);

    // README.md
    File.WriteAllText(Path.Combine(root, "README.md"), $"""
# {name}

> .NET 10 + Blazor Auto + Supabase Clean Architecture

## 기술 스택
- **언어**: C# / .NET 10
- **프론트엔드**: Blazor Auto (SSR + WASM)
- **백엔드**: ASP.NET Core Web API
- **오케스트레이션**: .NET Aspire (file-based apphost.cs)
- **DB**: Supabase (PostgreSQL)
- **스타일**: Tailwind CSS v4

## 프로젝트 구조

```
apphost.cs                 # Aspire 오케스트레이터 (file-based)
src/
  {name}.Domain/           # 엔터티, 도메인 이벤트, 값 객체
  {name}.Application/      # 유스케이스, 인터페이스, DTO
  {name}.Infrastructure/   # DB, 외부 서비스 구현체
  {name}.Api/              # ASP.NET Core Web API
  {name}.Web/              # Blazor Auto 프론트엔드
tests/
  {name}.Domain.Tests/
  {name}.Application.Tests/
```

## 빠른 시작

```bash
aspire run          # Aspire 실행 (TailwindCSS watch 포함)
dotnet build
dotnet test
```
""", Encoding.UTF8);
    Print("  [OK] README.md", ConsoleColor.Green);
}
