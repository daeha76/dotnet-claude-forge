#!/usr/bin/env dotnet-script
// forge-installer.csx — Claude Forge 설치 (모드 1) 함수 모음
// install.csx에서 #load 되어 실행됨
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

// ── 의존성 확인 ──────────────────────────────────────────────────────────────
void CheckDependencies()
{
    Print("필수 도구 확인 중...", ConsoleColor.White);
    var missing = new List<string>();

    if (!CommandExists("node")) missing.Add("node");
    if (!CommandExists("git"))  missing.Add("git");

    if (!CommandExists("dotnet"))
    {
        Print("  [참고] .NET SDK 미설치 — .NET 프로젝트 기능 사용 시 필요", ConsoleColor.Yellow);
        Print("         설치: https://dot.net/download", ConsoleColor.Yellow);
        Print("");
    }
    else
    {
        var ver = RunCapture("dotnet", "--version");
        Print($"  [OK] .NET SDK: {ver.Trim()}", ConsoleColor.Green);
    }

    if (missing.Count > 0)
    {
        Print($"  [오류] 미설치 도구: {string.Join(", ", missing)}", ConsoleColor.Red);
        if (isMacOS)         Print("         brew install "    + string.Join(" ", missing));
        else if (!isWindows) Print("         sudo apt install " + string.Join(" ", missing));
        else                 Print("         winget install "  + string.Join(" ", missing));
        Environment.Exit(1);
    }

    Print("  [OK] 모든 필수 도구 확인 완료", ConsoleColor.Green);
}

// ── git 서브모듈 초기화 ───────────────────────────────────────────────────────
void InitSubmodules()
{
    Print("");
    Print("git 서브모듈 초기화 중...", ConsoleColor.White);
    int code = Run("git", "submodule update --init --recursive", repoDir);
    if (code == 0) Print("  [OK] cc-chips 서브모듈 초기화 완료", ConsoleColor.Green);
    else           Print("  [참고] 서브모듈 초기화 건너뜀 (이미 초기화됨)", ConsoleColor.Yellow);
}

// ── 기존 ~/.claude 백업 ───────────────────────────────────────────────────────
void BackupExistingConfig()
{
    if (!Directory.Exists(claudeDir)) return;

    Print("");
    Print("  기존 ~/.claude 폴더 발견", ConsoleColor.Yellow);
    Console.Write("  백업할까요? (y/n): ");
    var reply = Console.ReadLine()?.Trim().ToLower();
    if (reply == "y")
    {
        var backup = $"{claudeDir}.backup.{DateTime.Now:yyyyMMdd_HHmmss}";
        Directory.Move(claudeDir, backup);
        Print($"  [OK] 백업: {backup}", ConsoleColor.Green);
    }
    else
    {
        Print("  백업 건너뜀", ConsoleColor.Yellow);
    }
}

// ── 심볼릭 링크 또는 파일 복사 ───────────────────────────────────────────────
void LinkOrCopyFiles()
{
    Print("");
    Print(useSymlinks ? "심볼릭 링크 생성 중..." : "파일 복사 중...", ConsoleColor.White);

    Directory.CreateDirectory(claudeDir);

    var dirs = new[] { "agents", "rules", "commands", "scripts", "skills", "hooks", "cc-chips", "cc-chips-custom" };
    foreach (var dir in dirs)
    {
        var src  = Path.Combine(repoDir, dir);
        var dest = Path.Combine(claudeDir, dir);
        if (!Directory.Exists(src)) continue;

        if (Directory.Exists(dest) || File.Exists(dest)) DeletePath(dest);

        if (useSymlinks) Directory.CreateSymbolicLink(dest, src);
        else             CopyDirectory(src, dest);

        Print($"  [OK] {dir}/", ConsoleColor.DarkGray);
    }

    var files = new[] { "settings.json", "hooks.json" };
    foreach (var file in files)
    {
        var src  = Path.Combine(repoDir, file);
        var dest = Path.Combine(claudeDir, file);
        if (!File.Exists(src)) continue;

        if (File.Exists(dest)) File.Delete(dest);

        if (useSymlinks) File.CreateSymbolicLink(dest, src);
        else             File.Copy(src, dest);

        Print($"  [OK] {file}", ConsoleColor.DarkGray);
    }
}

// ── CC-Chips 커스텀 오버레이 ─────────────────────────────────────────────────
void ApplyCcChipsOverlay()
{
    var customDir = Path.Combine(repoDir, "cc-chips-custom");
    var target    = Path.Combine(claudeDir, "cc-chips");
    if (!Directory.Exists(customDir) || !Directory.Exists(target)) return;

    Print("");
    Print("CC CHIPS 커스텀 오버레이 적용 중...", ConsoleColor.White);

    var engineSrc = Path.Combine(customDir, "engine.sh");
    if (File.Exists(engineSrc))
    {
        File.Copy(engineSrc, Path.Combine(target, "engine.sh"), overwrite: true);
        Print("  [OK] engine.sh", ConsoleColor.Green);
    }

    var themesSrc = Path.Combine(customDir, "themes");
    var themesDst = Path.Combine(target, "themes");
    if (Directory.Exists(themesSrc) && Directory.Exists(themesDst))
    {
        foreach (var f in Directory.GetFiles(themesSrc, "*.sh"))
            File.Copy(f, Path.Combine(themesDst, Path.GetFileName(f)), overwrite: true);
        Print("  [OK] themes/", ConsoleColor.Green);
    }

    Print("  [OK] 오버레이 적용 완료", ConsoleColor.Green);
}

// ── 설치 검증 ────────────────────────────────────────────────────────────────
bool VerifyInstallation()
{
    Print("");
    Print("설치 확인 중...", ConsoleColor.White);
    int errors = 0;
    var items  = new[] { "agents", "rules", "commands", "skills", "cc-chips", "settings.json" };
    foreach (var item in items)
    {
        var path = Path.Combine(claudeDir, item);
        if (Directory.Exists(path) || File.Exists(path))
            Print($"  [OK] {item}", ConsoleColor.Green);
        else
        {
            Print($"  [실패] {item} — 없음", ConsoleColor.Red);
            errors++;
        }
    }
    return errors == 0;
}

// ── 메타데이터 기록 ───────────────────────────────────────────────────────────
void WriteForgeMetadata()
{
    Print("");
    Print("메타데이터 기록 중...", ConsoleColor.White);

    var metaPath      = Path.Combine(claudeDir, ".forge-meta.json");
    string installMode = useSymlinks ? "symlink" : "copy";
    string now         = DateTime.UtcNow.ToString("o");
    string installedAt = now;

    // 기존 installed_at 보존
    if (File.Exists(metaPath))
    {
        try
        {
            var prev = JsonDocument.Parse(File.ReadAllText(metaPath));
            if (prev.RootElement.TryGetProperty("installed_at", out var ia))
                installedAt = ia.GetString() ?? now;
        }
        catch { }
    }

    string gitCommit = RunCapture("git", "rev-parse --short HEAD", repoDir).Trim();
    string remoteUrl = RunCapture("git", "remote get-url origin",  repoDir).Trim();

    var meta = new
    {
        repo_path    = repoDir,
        install_mode = installMode,
        installed_at = installedAt,
        updated_at   = now,
        git_commit   = gitCommit,
        remote_url   = remoteUrl,
        platform,
    };

    File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
    Print("  [OK] .forge-meta.json", ConsoleColor.Green);
}

// ── 셸 별칭 설정 ─────────────────────────────────────────────────────────────
void SetupShellAliases()
{
    Print("");
    Print("셸 별칭 설정 중...", ConsoleColor.White);

    string? rcFile = null;
    string home    = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    if      (File.Exists(Path.Combine(home, ".zshrc")))  rcFile = Path.Combine(home, ".zshrc");
    else if (File.Exists(Path.Combine(home, ".bashrc"))) rcFile = Path.Combine(home, ".bashrc");

    if (rcFile is null) { Print("  [참고] .zshrc / .bashrc 없음 — 건너뜀", ConsoleColor.Yellow); return; }

    var content = File.ReadAllText(rcFile);
    if (content.Contains("# Claude Code aliases"))
    {
        Print($"  [OK] 별칭 이미 존재 ({Path.GetFileName(rcFile)})", ConsoleColor.Green);
        return;
    }

    File.AppendAllText(rcFile, "\n# Claude Code aliases\nalias cc='claude'\nalias ccr='claude --resume'\n");
    Print($"  [OK] 별칭 추가 → {Path.GetFileName(rcFile)} (cc, ccr)", ConsoleColor.Green);
}

// ── MCP 서버 설치 ─────────────────────────────────────────────────────────────
void InstallMcpServers()
{
    Print("");
    Print("MCP 서버 설치...", ConsoleColor.White);

    if (!CommandExists("claude"))
    {
        Print("  [참고] Claude CLI 없음 — 건너뜀", ConsoleColor.Yellow);
        Print("         설치: https://claude.ai/download", ConsoleColor.Yellow);
        return;
    }

    Console.Write("  권장 MCP 서버를 설치할까요? (y/n): ");
    if (Console.ReadLine()?.Trim().ToLower() != "y") { Print("  건너뜀", ConsoleColor.DarkGray); return; }

    var core = new (string Name, string Cmd)[]
    {
        ("context7",            "claude mcp add context7 -- npx -y @upstash/context7-mcp"),
        ("playwright",          "claude mcp add playwright -- npx @playwright/mcp@latest"),
        ("memory",              "claude mcp add memory -- npx -y @modelcontextprotocol/server-memory"),
        ("sequential-thinking", "claude mcp add sequential-thinking -- npx -y @modelcontextprotocol/server-sequential-thinking"),
    };

    foreach (var (name, cmd) in core)
    {
        Print($"  {name}...");
        var parts = cmd.Split(' ', 2);
        int code  = Run(parts[0], parts[1]);
        if (code == 0) Print($"  [OK] {name}", ConsoleColor.Green);
        else           Print($"  [참고] {name} — 이미 설치 또는 실패", ConsoleColor.Yellow);
    }

    // 선택적 서버
    Print("");
    var optional = new (string Name, string Cmd)[]
    {
        ("github",   "claude mcp add github -- npx -y @modelcontextprotocol/server-github"),
        ("supabase", "claude mcp add supabase -- npx -y @supabase/mcp-server-supabase@latest"),
    };
    foreach (var (name, cmd) in optional)
    {
        Console.Write($"  {name} 설치? (y/n): ");
        if (Console.ReadLine()?.Trim().ToLower() != "y") continue;
        var parts = cmd.Split(' ', 2);
        int code  = Run(parts[0], parts[1]);
        if (code == 0) Print($"  [OK] {name}", ConsoleColor.Green);
        else           Print($"  [참고] {name} — 실패", ConsoleColor.Yellow);
    }

    Print("  [OK] MCP 서버 설치 완료", ConsoleColor.Green);
}

// ── 외부 스킬 설치 ───────────────────────────────────────────────────────────
void InstallExternalSkills()
{
    if (!CommandExists("npx")) return;

    Print("");
    Console.Write("  외부 스킬 설치? (Superpowers, Humanizer 등) (y/n): ");
    if (Console.ReadLine()?.Trim().ToLower() != "y") { Print("  건너뜀", ConsoleColor.DarkGray); return; }

    var skills = new (string Name, string Args)[]
    {
        ("superpowers",   "skills add obra/superpowers -y -g"),
        ("humanizer",     "skills add blader/humanizer -y -g"),
        ("ui-ux-pro-max", "skills add nextlevelbuilder/ui-ux-pro-max-skill -y -g"),
    };

    foreach (var (name, args) in skills)
    {
        Print($"  {name}...");
        int code = Run("npx", $"-y {args}");
        if (code == 0) Print($"  [OK] {name}", ConsoleColor.Green);
        else           Print($"  [참고] {name} — 실패", ConsoleColor.Yellow);
    }
}

// ── Work Tracker 설치 ────────────────────────────────────────────────────────
void InstallWorkTracker()
{
    var wtScript = Path.Combine(repoDir, "setup", "work-tracker-install.sh");
    if (!File.Exists(wtScript) || isWindows) return;

    Print("");
    Console.Write("  Work Tracker 설치? (Claude Code 사용량 → Supabase) (y/n): ");
    if (Console.ReadLine()?.Trim().ToLower() != "y") return;

    Run("bash", wtScript);
}
