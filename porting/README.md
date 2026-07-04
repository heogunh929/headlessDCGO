# porting/ — 카드 포팅 파이프라인 (엔진과 분리된 도구 일체)

DCGO 원본 카드 효과를 헤드리스 엔진으로 1:1 포팅하기 위한 **도구·데이터·문서 전부**를 여기 모았다.
엔진 런타임 기능을 쓰지 않는 별도 관심사라 한 폴더로 격리한다.

## 레이아웃

| 경로 | 내용 |
|---|---|
| `docs/` | 설계·참조 문서 — `IR-PIPELINE-DESIGN.md`(v3 정본), `PORTING-RECIPE.md`, `PRIMITIVE-CATALOG.md`, `EXPRESSION-MAP.md` |
| `tools/CardIr.Extract/` | (v3 stage 2) Roslyn Source IR 추출기 |
| `scripts/` | `port-batch.sh`(v2 배치 드라이버), `make-card-brief.py`, `generate-primitive-catalog.py` |
| `data/` | `ir-src/`(Source IR), `ir/`(Canonical IR), 이후 `symbols.json`·`ledger/`·`cardpool.json` |
| `briefs/` | (v2) 카드별 브리프 |
| `stop/` | STOP 집계 (`<SET>.<COLOR>.md`) |
| `binding-test/` | `CardEffect.Binding.Auto.Tests` — 바인딩 게이트(강모델 소유) |

## 여기 없는 것 (제약상 잔류)

- **`.opencode/`** — 루트. opencode CLI가 실행 디렉터리 기준으로 설정을 찾고, 포터가 루트의
  `DCGO/`를 읽고 `src/.../CardEffect/`에 써야 하므로 루트에서 돌아야 한다. 포터 에이전트/커맨드/
  스킬/플러그인은 전부 `.opencode/` 아래.
- **카드 미러 `src/HeadlessDCGO.Engine/.../CardEffect/<SET>/<COLOR>/`** — 포팅 "산출물"이지만
  엔진에 컴파일되어 로드되는 실제 효과 클래스라 엔진 소스에 남는다. v3에서는 코드젠 생성물이 된다.

## 실행

```bash
# v2 배치 포팅(현행): 스켈레톤 스텁만 대상, 카드당 독립 세션
porting/scripts/port-batch.sh BT1 Blue

# 바인딩 게이트 (run-tests가 tests/ + porting/ 를 탐색)
bash scripts/run-tests.sh CardEffect.Binding.Auto

# v3 Source IR 추출 (stage 2)
dotnet run --project porting/tools/CardIr.Extract -- DCGO/Assets/Scripts/CardEffect/BT1/Blue/BT1_031.cs
```

파이프라인 전체 설계와 단계별 명세는 `docs/IR-PIPELINE-DESIGN.md` 참조.
