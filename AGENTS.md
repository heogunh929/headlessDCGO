# headlessDCGO — 에이전트 규칙 (opencode)

Digimon Card Game(원본 Unity `DCGO/`)을 헤드리스 C# 엔진(`src/HeadlessDCGO.Engine/`)으로 포팅하는 저장소. 현재 단계 = **카드 per-card 포팅**. 엔진과 카드-facing 프리미티브(팩토리)는 **선행개발 완료**.

## 절대 규칙
- **`DCGO/` 는 읽기 전용** — AS-IS 1:1 대조용 원본. 절대 수정/커밋하지 않는다.
- **커밋 금지 경로**: `DCGO/`, `.dotnet/`, `**/bin/`, `**/obj/`.
- **커밋은 사용자가 직접** 한다. 에이전트는 커밋/푸시하지 않는다.
- **AS-IS 1:1 미러**: 카드-facing 코드는 원본 파일/심볼을 그대로 미러. 게임 룰 불변. 로직 변경/단순화/추측 금지.
- **프리미티브 개발 금지**: 카드가 부르는 `CardEffectFactory.<이름>(...)` 은 이미 전부 존재. 없으면 만들지 말고 **STOP**(강모델 몫).

## 카드 포팅
- 진입 커맨드: `/port-card <카드ID>` (예: `/port-card ST1_06`).
- 절차·템플릿·예시: `docs/porting/PORTING-RECIPE.md`
- 사용 가능한 프리미티브 전수(시그니처): `docs/porting/PRIMITIVE-CATALOG.md`
- 미러 파일: `src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/<SET>/<COLOR>/<ID>.cs`
- 테스트: `tests/CardEffect.<SET>.<COLOR>.Tests/` (카드마다 새 프로젝트 만들지 않고 그룹에 sub-test 추가).

## 검증 (매 카드 후)
```bash
bash scripts/run-tests.sh          # SUMMARY: PASS=N FAIL=0
```
- 규칙 불변 확인(선택): `dotnet run --project tools/RuleAudit`

## 구조 요약
- 카드-facing 팩토리: `src/.../CardEffectCommons/CardPortingFramework.cs`(원본 이름 미러) + `CardEffectFactory/`.
- 엔진 배관(수정 자제): `src/.../Headless/`.
- 포팅된 카드 예시: `src/.../CardEffect/ST1/Red/ST1_06.cs`.
