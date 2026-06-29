# CI 확인 절차 (G8-007)

- 작성일: 2026-06-30
- 목적: 최신 `main` HEAD의 GitHub Actions 상태를 확인하는 절차. (헤드리스 에이전트 환경엔 `gh` CLI가 없고 브라우저 UI 접근이 막혀 있어, 사용자/CI에서 확인.)

## 1. CI가 검사하는 것 (한계)
`.github/workflows/ci.yml`은 **컴파일 게이트 only**:
- 엔진(`HeadlessDCGO.Engine`) Release 빌드 + 전 테스트 프로젝트 빌드.
- **테스트는 실행하지 않음** — 다수 테스트가 git-ignored `DCGO/` 원본을 읽는데 CI엔 없기 때문.
- 따라서 **CI green = "Release 컴파일됨"**일 뿐. **실 회귀 게이트는 로컬 `bash scripts/run-tests.sh`**.

## 2. 최신 HEAD 상태 확인 방법

### (a) 브라우저 UI
https://github.com/heogunh929/headlessDCGO/actions → 최신 커밋의 워크플로 run 확인.

### (b) gh CLI (설치 시)
```bash
# 설치(예): winget install GitHub.cli  /  apt install gh  /  brew install gh
gh auth login
gh run list --branch main --limit 5
gh run view --log            # 최신 run 상세
```

### (c) REST API (gh 없이)
```bash
curl -s https://api.github.com/repos/heogunh929/headlessDCGO/actions/runs?branch=main\&per_page=1 \
  | grep -E '"head_sha"|"status"|"conclusion"'
```
`conclusion: success` 면 컴파일 게이트 통과.

## 3. 권장
- push 후 (a) 또는 (b)로 한 번 확인.
- **진짜 검증은 로컬 전체 스위트**(`bash scripts/run-tests.sh`, 결과는 docs/test-results/ 보관 — G8-008).
- (선택) CI를 실제 테스트 게이트로 만들려면 `DCGO/` 비의존 테스트 서브셋만 CI에서 실행하도록 `ci.yml` 확장(별도 작업).
