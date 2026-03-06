#!/usr/bin/env dotnet-script
// tailwind-setup.csx — Bootstrap 제거 + TailwindCSS v4 설정 (모드 2 하위 모듈)
#nullable enable

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

void SetupTailwind(string root, string name, string repoDir)
{
    Print("");
    Print("  Bootstrap 제거 + TailwindCSS v4 설정 중...", ConsoleColor.White);

    // Blazor Auto: 실제 서버 프로젝트는 src/{name}.Web/{name}.Web/ (중첩 구조)
    var webServerProjectDir = Path.Combine(root, "src", $"{name}.Web", $"{name}.Web");
    var wwwrootDir   = Path.Combine(webServerProjectDir, "wwwroot");
    var cssDir       = Path.Combine(wwwrootDir, "css");

    // Bootstrap 디렉토리 삭제
    var bootstrapDir = Path.Combine(cssDir, "bootstrap");
    if (Directory.Exists(bootstrapDir))
    {
        Directory.Delete(bootstrapDir, recursive: true);
        Print("  [OK] wwwroot/css/bootstrap/ 삭제", ConsoleColor.Green);
    }

    // app.css: Bootstrap import + .btn 클래스 제거
    Directory.CreateDirectory(cssDir);
    var appCssPath = Path.Combine(cssDir, "app.css");
    var cssContent = File.Exists(appCssPath) ? File.ReadAllText(appCssPath) : "";
    cssContent = Regex.Replace(cssContent, "@import\\s+[\"']bootstrap[^\"']*[\"'];?\\s*\\n?", "");
    cssContent = Regex.Replace(cssContent, @"\.btn[^{]*\{[^}]*\}\s*", "");
    File.WriteAllText(appCssPath, cssContent.TrimEnd() + "\n", Encoding.UTF8);
    Print("  [OK] app.css → Bootstrap 제거", ConsoleColor.Green);

    // tailwindcss_input.css: Tailwind v4 진입점
    var twInputPath = Path.Combine(cssDir, "tailwindcss_input.css");
    File.WriteAllText(twInputPath, "@import \"tailwindcss\";\n", Encoding.UTF8);
    Print("  [OK] tailwindcss_input.css (@import \"tailwindcss\")", ConsoleColor.Green);

    // App.razor: Bootstrap CDN 제거 + tailwindcss_output.css 링크 추가
    var appRazorPath = Path.Combine(webServerProjectDir, "Components", "App.razor");
    if (!File.Exists(appRazorPath))
        appRazorPath = Path.Combine(webServerProjectDir, "App.razor");
    if (File.Exists(appRazorPath))
    {
        var razor = File.ReadAllText(appRazorPath);
        razor = Regex.Replace(razor, @"<link[^>]*bootstrap[^>]*>\s*\n?", "",
            RegexOptions.IgnoreCase);
        if (!razor.Contains("tailwindcss_output.css"))
            razor = razor.Replace("</head>",
                """    <link rel="stylesheet" href="@Assets["tailwindcss_output.css"]" />""" + "\n</head>");
        File.WriteAllText(appRazorPath, razor, Encoding.UTF8);
        Print("  [OK] App.razor → Bootstrap 제거 + tailwindcss_output.css", ConsoleColor.Green);
    }

    // npm install
    Print("");
    Print("  TailwindCSS v4 npm 패키지 설치 중...", ConsoleColor.White);
    Print("  (참고: https://tailwindcss.com/docs/installation/tailwind-cli)", ConsoleColor.DarkGray);
    if (CommandExists("npm"))
    {
        Run("npm", "init -y", webServerProjectDir);
        Run("npm", "install --save-dev tailwindcss @tailwindcss/cli", webServerProjectDir);
        Print("  [OK] npm install tailwindcss @tailwindcss/cli", ConsoleColor.Green);
    }
    else
    {
        Print("  [참고] npm 없음 — aspire run 시 npx로 자동 다운로드됩니다", ConsoleColor.Yellow);
    }

    // tailwind-runner.mjs 복사
    Print("");
    Print("  tailwind-runner.mjs 생성 중...", ConsoleColor.White);
    var runnerTemplate = Path.Combine(repoDir, "templates", "tailwind-runner.mjs.template");
    File.WriteAllText(Path.Combine(webServerProjectDir, "tailwind-runner.mjs"),
        File.ReadAllText(runnerTemplate), Encoding.UTF8);
    Print("  [OK] tailwind-runner.mjs (watch 모드 자동 재시작 래퍼)", ConsoleColor.Green);

    // apphost.cs 생성
    Print("");
    Print("  apphost.cs 생성 중...", ConsoleColor.White);
    var resourcePrefix  = name.ToLower().Replace(".", "-");
    var apphostTemplate = Path.Combine(repoDir, "templates", "apphost.cs.template");
    File.WriteAllText(Path.Combine(root, "apphost.cs"),
        File.ReadAllText(apphostTemplate)
            .Replace("{{name}}", name)
            .Replace("{{resourcePrefix}}", resourcePrefix),
        Encoding.UTF8);
    Print("  [OK] apphost.cs (file-based Aspire — AddCSharpApp + HTTPS)", ConsoleColor.Green);

    Print("  [OK] Bootstrap 제거 + TailwindCSS v4 설정 완료", ConsoleColor.Green);
}
