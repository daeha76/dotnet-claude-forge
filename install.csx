#!/usr/bin/env dotnet-script
// install.csx — Claude Forge 설치 및 .NET 프로젝트 초기화 (크로스플랫폼)
//
// 사용법:
//   dotnet script install.csx                         # Claude Forge 설치
//   dotnet script install.csx -- --AppName MyApp      # .NET 프로젝트 생성
//   dotnet script install.csx -- --AppName MyApp --ParentDir D:\projects
//
// 도구 설치 (최초 1회):
//   dotnet tool install -g dotnet-script

#nullable enable
#r "nuget: System.Text.Json, 8.0.0"
#load "utils.csx"
#load "modules/forge-installer.csx"
#load "modules/nuget-installer.csx"
#load "modules/tailwind-setup.csx"
#load "modules/claude-forge-setup.csx"
#load "modules/project-scaffolder.csx"

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

// ──────────────────────────────────────────────
// 배너
// ──────────────────────────────────────────────
Print("");
Print("   ╔═╗┬  ┌─┐┬ ┬┌┬┐┌─┐  ╔═╗┌─┐┬─┐┌─┐┌─┐", ConsoleColor.Cyan);
Print("   ║  │  ├─┤│ │ ││├┤   ╠╣ │ │├┬┘│ ┬├┤ ", ConsoleColor.Cyan);
Print("   ╚═╝┴─┘┴ ┴└─┘─┴┘└─┘  ╚  └─┘┴└─└─┘└─┘", ConsoleColor.Cyan);
Print("                              for .NET", ConsoleColor.Blue);
Print("");
Print("   Production-grade Claude Code Framework for .NET", ConsoleColor.White);
Print("   github.com/daeha76/claude-forge",   ConsoleColor.DarkGray);
Print("");

// ──────────────────────────────────────────────
// 스크립트 위치 기준 경로 계산
// ──────────────────────────────────────────────
string repoDir = Environment.GetCommandLineArgs()
    .Where(a => a.EndsWith(".csx", StringComparison.OrdinalIgnoreCase) && File.Exists(a))
    .Select(a => Path.GetDirectoryName(Path.GetFullPath(a))!)
    .FirstOrDefault()
    ?? Directory.GetCurrentDirectory();

// ──────────────────────────────────────────────
// 인수 파싱
// 우선순위: 환경변수 (래퍼) > 직접 Args (직접 실행 시)
// ──────────────────────────────────────────────

// 1순위: 환경변수 (래퍼 install.ps1 / install.sh이 설정)
string appName   = Environment.GetEnvironmentVariable("FORGE_APP_NAME")   ?? "";
string parentDir = Environment.GetEnvironmentVariable("FORGE_PARENT_DIR") ?? "";

// 2순위: 직접 주어진 Args
if (string.IsNullOrEmpty(appName)   && Args.Count > 0) appName   = Args[0];
if (string.IsNullOrEmpty(parentDir) && Args.Count > 1) parentDir = Args[1];

// 부모 경로 기본값: claude-forge 상위 폴더
if (string.IsNullOrEmpty(parentDir))
    parentDir = Path.GetDirectoryName(repoDir) ?? Directory.GetCurrentDirectory();

string claudeDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

// ──────────────────────────────────────────────
// 플랫폼 감지
// ──────────────────────────────────────────────
bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
bool isMacOS   = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
bool isLinux   = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
bool isWsl     = isLinux && File.Exists("/proc/sys/fs/binfmt_misc/WSLInterop");
string platform = isWindows ? "windows" : isMacOS ? "macos" : isWsl ? "wsl" : "linux";

// WSL에서 Windows 파일시스템 경로면 심볼릭 링크 불가
bool useSymlinks = !isWindows && !(isWsl && repoDir.StartsWith("/mnt/"));

Print($"   Platform: {platform} | Symlinks: {(useSymlinks ? "yes" : "no (copy mode)")}", ConsoleColor.DarkGray);
Print("");

// ──────────────────────────────────────────────
// 분기: 모드 2 — .NET 프로젝트 초기화
// ──────────────────────────────────────────────
if (!string.IsNullOrEmpty(appName))
{
    var targetPath = Path.Combine(parentDir, appName);
    Print("  [모드 2] .NET 프로젝트 초기화", ConsoleColor.Cyan);
    Print($"   앱 이름 : {appName}", ConsoleColor.White);
    Print($"   생성 위치: {targetPath}", ConsoleColor.White);
    Print("");
    InitDotNetProject(appName, parentDir);
    return;
}

// ──────────────────────────────────────────────
// 모드 1 — Claude Forge 설치
// ──────────────────────────────────────────────
Print("  [모드 1] Claude Forge 설치", ConsoleColor.Cyan);
Print("");

CheckDependencies();
InitSubmodules();
BackupExistingConfig();
LinkOrCopyFiles();
ApplyCcChipsOverlay();

if (VerifyInstallation())
{
    WriteForgeMetadata();
    if (!isWindows) SetupShellAliases();
    InstallMcpServers();
    InstallExternalSkills();
    InstallWorkTracker();

    Print("");
    Print("  ╔══════════════════════════════════════════════════════╗", ConsoleColor.Green);
    Print("  ║           Claude Forge for .NET 설치 완료!           ║", ConsoleColor.Green);
    Print("  ╠══════════════════════════════════════════════════════╣", ConsoleColor.Green);
    Print("  ║  11 agents · 55 commands · 12 rules · 18 skills     ║", ConsoleColor.Green);
    Print("  ╚══════════════════════════════════════════════════════╝", ConsoleColor.Green);
    Print("");
    Print("  처음이신가요? 이것만 하세요:", ConsoleColor.Cyan);
    Print("    1. 새 터미널 열고 'claude' 실행");
    Print("    2. /guide 입력 — 3분 인터랙티브 가이드");
    Print("");
    Print("  .NET 앱 프로젝트를 새로 만들려면:", ConsoleColor.DarkGray);
    Print("    dotnet script install.csx -- --AppName <앱이름>", ConsoleColor.DarkGray);
    Print("");
    Print("  ★ github.com/daeha76/claude-forge", ConsoleColor.Yellow);
}
else
{
    Print("\n  [오류] 설치 중 문제가 발생했습니다. 위 메시지를 확인하세요.", ConsoleColor.Red);
    Environment.Exit(1);
}

// 함수들은 각 모듈 파일 참조:
//   utils.csx              → Print, Run, RunCapture, DeletePath, CopyDirectory
//   forge-installer.csx    → CheckDependencies ... InstallWorkTracker  (모드 1)
//   project-scaffolder.csx → InitDotNetProject  (모드 2 오케스트레이터)
//   nuget-installer.csx    → InstallNuGetPackages
//   tailwind-setup.csx     → SetupTailwind
//   claude-forge-setup.csx → SetupClaudeForge
