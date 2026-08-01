#!/usr/bin/env bash
# DCGO 아레나 상주 서비스 기동 — 운영 토큰은 ~/.dcgo/ops.env(600)에서만 읽는다
# (보안 정리 2026-08-01: 커맨드라인·히스토리에 토큰을 남기지 않는다).
#
#   ./scripts/dcgo-serve.sh            # runner + opsd 기동
#   ./scripts/dcgo-serve.sh stop       # 정지
#
# 포트: 8790 runner(로컬) · 8791 공개(참가자, 외부 포워딩 대상) · 8792 관리(LAN 전용)
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${DCGO_ENV_FILE:-$HOME/.dcgo/ops.env}"
LOG_DIR="${DCGO_LOG_DIR:-$HOME/.dcgo/logs}"
PY="$REPO/rl/.venv/bin/python"

if [ "${1:-start}" = "stop" ]; then
  pkill -f "opsd.runner" || true
  pkill -f "opsd.server" || true
  echo "정지됨"
  exit 0
fi

[ -f "$ENV_FILE" ] || { echo "토큰 파일 없음: $ENV_FILE"; exit 1; }
# shellcheck disable=SC1090
set -a; . "$ENV_FILE"; set +a
[ -n "${DCGO_OPS_TOKEN:-}" ] || { echo "DCGO_OPS_TOKEN 미설정($ENV_FILE)"; exit 1; }

mkdir -p "$LOG_DIR"
cd "$REPO/rl"
nohup "$PY" -m opsd.runner --port 8790 >"$LOG_DIR/runner.log" 2>&1 &
sleep 2
nohup "$PY" -m opsd.server >"$LOG_DIR/opsd.log" 2>&1 &
sleep 3
echo "기동됨 — 공개 http://$(hostname -I | awk '{print $1}'):8791  ·  관리 http://$(hostname -I | awk '{print $1}'):8792"
echo "로그: $LOG_DIR"
