#!/usr/bin/env dotnet-script
// utils.csx — Claude Forge 공통 유틸리티 함수
#nullable enable

using System;
using System.Diagnostics;
using System.IO;

// ──────────────────────────────────────────────
// 출력
// ──────────────────────────────────────────────
static void Print(string msg, ConsoleColor? color = null)
{
    if (color.HasValue) Console.ForegroundColor = color.Value;
    Console.WriteLine(msg);
    Console.ResetColor();
}

// ──────────────────────────────────────────────
// 도구 존재 여부 확인
// ──────────────────────────────────────────────
static bool CommandExists(string cmd)
{
    try
    {
        var psi = new ProcessStartInfo(cmd, "--version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        return true;
    }
    catch { return false; }
}

// ──────────────────────────────────────────────
// 프로세스 실행 (exit code 반환)
// ──────────────────────────────────────────────
static int Run(string cmd, string args, string? workDir = null)
{
    try
    {
        var psi = new ProcessStartInfo(cmd, args) { UseShellExecute = false };
        if (workDir is not null) psi.WorkingDirectory = workDir;
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        return p.ExitCode;
    }
    catch { return -1; }
}

// ──────────────────────────────────────────────
// 프로세스 실행 (stdout 캡처)
// ──────────────────────────────────────────────
static string RunCapture(string cmd, string args, string? workDir = null)
{
    try
    {
        var psi = new ProcessStartInfo(cmd, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        if (workDir is not null) psi.WorkingDirectory = workDir;
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return output;
    }
    catch { return ""; }
}

// ──────────────────────────────────────────────
// 파일 또는 디렉토리 삭제
// ──────────────────────────────────────────────
static void DeletePath(string path)
{
    if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    else if (File.Exists(path)) File.Delete(path);
}

// ──────────────────────────────────────────────
// 디렉토리 재귀 복사
// ──────────────────────────────────────────────
static void CopyDirectory(string src, string dest)
{
    Directory.CreateDirectory(dest);
    foreach (var file in Directory.GetFiles(src))
        File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
    foreach (var dir in Directory.GetDirectories(src))
        CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
}
