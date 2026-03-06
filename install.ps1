<#
.SYNOPSIS
    Claude Forge 설치 스크립트 (Windows)

    이 파일은 install.csx 의 얇은 래퍼입니다.
    모든 실제 설치 로직은 install.csx 에 있습니다.

.PARAMETER AppName
    생성할 .NET 앱 이름. 지정 시 새 프로젝트 폴더를 만들고 git init 합니다.
    예: .\install.ps1 MyApp
    예: .\install.ps1 -AppName MyApp

.PARAMETER ParentDir
    프로젝트 폴더가 생성될 상위 디렉토리. 기본값: claude-forge 의 상위 폴더
    예: .\install.ps1 MyApp -ParentDir "D:\projects"
#>
param(
    [Parameter(Position=0)]
    [string]$AppName   = "",
    [string]$ParentDir = ""
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# .NET SDK 확인
if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-Host "[오류] .NET SDK가 설치되어 있지 않습니다." -ForegroundColor Red
    Write-Host "       설치: winget install Microsoft.DotNet.SDK.10" -ForegroundColor Yellow
    exit 1
}

# dotnet-script 전역 도구 확인 및 설치
$toolList = dotnet tool list -g 2>$null
if (-not ($toolList -match "dotnet-script")) {
    Write-Host "dotnet-script 전역 도구를 설치합니다..." -ForegroundColor Cyan
    dotnet tool install -g dotnet-script
}

# 인수를 환경변수로 전달 (dotnet 옵션과 충돌 방지)
$env:FORGE_APP_NAME   = $AppName
$env:FORGE_PARENT_DIR = $ParentDir

try {
    & dotnet script "$ScriptDir\install.csx"
    $exitCode = $LASTEXITCODE
}
finally {
    # 환경변수 정리
    Remove-Item Env:\FORGE_APP_NAME   -ErrorAction SilentlyContinue
    Remove-Item Env:\FORGE_PARENT_DIR -ErrorAction SilentlyContinue
}

exit $exitCode
