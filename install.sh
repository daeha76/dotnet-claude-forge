#!/bin/bash
# install.sh — Claude Forge 설치 스크립트 (macOS / Linux / WSL)
#
# 이 파일은 install.csx 의 얇은 래퍼입니다.
# 모든 실제 설치 로직은 install.csx 에 있습니다.
#
# 사용법:
#   ./install.sh                  # Claude Forge 설치
#   ./install.sh MyApp            # .NET 프로젝트 생성 (claude-forge 상위 폴더에 생성)
#   ./install.sh MyApp ~/projects # .NET 프로젝트 생성 (지정 위치에 생성)

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
APP_NAME="${1:-}"
PARENT_DIR="${2:-}"

# dotnet SDK 확인
if ! command -v dotnet > /dev/null 2>&1; then
    echo "[오류] .NET SDK가 설치되어 있지 않습니다."
    echo "       설치: https://dot.net/download"
    exit 1
fi

# dotnet-script 전역 도구 확인 및 설치
if ! dotnet tool list -g 2>/dev/null | grep -q "dotnet-script"; then
    echo "dotnet-script 전역 도구를 설치합니다..."
    dotnet tool install -g dotnet-script
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

# 인수를 환경변수로 전달 (dotnet 옵션과 충돌 방지)
export FORGE_APP_NAME="$APP_NAME"
export FORGE_PARENT_DIR="$PARENT_DIR"

dotnet script "$SCRIPT_DIR/install.csx"
