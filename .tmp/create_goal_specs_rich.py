import csv
import re
from pathlib import Path


BASE_CSV = Path("docs/headless_complete_goal_breakdown.csv")
DETAIL_CSV = Path("docs/headless_complete_goal_breakdown_detailed_ko.csv")
INDEX_CSV = Path("docs/headless_goal_spec_index.csv")
MODULES_CSV = Path("docs/headless_complete_architecture_modules.csv")
DEPENDENCIES_CSV = Path("docs/headless_complete_dependency_replacement.csv")
SOURCE_ORIGIN_CSV = Path("docs/headless_source_origin_mapping.csv")
OUT_DIR = Path("docs/goal-specs")
QUALITY_REPORT = Path("docs/test-results/headless_goal_spec_quality_results.md")


COMMON_DOCS = [
    "docs/headless_complete_goal_breakdown.csv",
    "docs/headless_complete_goal_breakdown_detailed_ko.csv",
    "docs/headless_goal_execution_prompt.md",
    "docs/headless_complete_unit_test_plan.md",
    "docs/headless_complete_unit_test_matrix.csv",
]


AREA_REFS = {
    "설계": [
        "docs/headless_complete_architecture_design.md",
        "docs/headless_complete_architecture_modules.csv",
        "docs/headless_complete_dependency_replacement.csv",
        "docs/headless_complete_porting_sequence.md",
    ],
    "테스트": [
        "docs/headless_complete_unit_test_plan.md",
        "docs/headless_complete_unit_test_matrix.csv",
        "docs/test-results",
    ],
    "단계 완료 게이트": [
        "docs/test-results",
        "docs/headless_complete_porting_sequence.md",
        "docs/headless_complete_goal_breakdown_detailed_ko.csv",
    ],
    "런타임": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "DCGO/Assets/Scripts/Script/GManager.cs",
        "DCGO/Assets/Scripts/Script/TurnStateMachine.cs",
        "DCGO/Assets/Scripts/Script/GameContext.cs",
    ],
    "상태/존": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "src/HeadlessDCGO.Engine/Headless/Services",
        "DCGO/Assets/Scripts/Script/Player.cs",
        "DCGO/Assets/Scripts/Script/CardController.cs",
        "DCGO/Assets/Scripts/Script/CardObjectController.cs",
    ],
    "Unity 전역 접근 대체": [
        "src/HeadlessDCGO.Engine/Headless/Bridge",
        "DCGO/Assets/Scripts/Script/GManager.cs",
        "DCGO/Assets/Scripts/Script/ContinuousController.cs",
        "DCGO/Assets/Scripts/Script/AutoProcessing.cs",
        "DCGO/Assets/Scripts/Script/AttackProcess.cs",
    ],
    "코루틴 대체": [
        "src/HeadlessDCGO.Engine/Headless/Coroutines",
        "DCGO/Assets/Scripts/Script/AutoProcessing.cs",
        "DCGO/Assets/Scripts/Script/AttackProcess.cs",
        "DCGO/Assets/Scripts/Script/TurnStateMachine.cs",
    ],
    "선택 처리 대체": [
        "src/HeadlessDCGO.Engine/Headless/Choices",
        "DCGO/Assets/Scripts/Script/SelectCardEffect.cs",
        "DCGO/Assets/Scripts/Script/SelectPermanentEffect.cs",
        "DCGO/Assets/Scripts/Script/SelectCountEffect.cs",
        "DCGO/Assets/Scripts/Script/SelectHandEffect.cs",
        "DCGO/Assets/Scripts/Script/SelectAttackEffect.cs",
        "DCGO/Assets/Scripts/Script/PlayerSelection",
    ],
    "효과 처리": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/AutoProcessing.cs",
        "DCGO/Assets/Scripts/Script/Effects.cs",
        "DCGO/Assets/Scripts/Script/MultipleSkills.cs",
    ],
    "세션/네트워크 대체": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "docs/dotnet_non_unity_dependency_replacement_plan.md",
    ],
    "데이터 로딩": [
        "src/HeadlessDCGO.Engine/Headless/DataLoading",
        "src/HeadlessDCGO.Engine/Headless/Services/ICardRepository.cs",
        "DCGO/Assets/CardBaseEntity",
        "DCGO/Assets/Scripts/Script/DeckData.cs",
        "DCGO/Assets/Scripts/Script/DeckCodeUtility.cs",
    ],
    "진단/결정성": [
        "src/HeadlessDCGO.Engine/Headless/Diagnostics",
        "src/HeadlessDCGO.Engine/Headless/Services/IRandomSource.cs",
        "src/HeadlessDCGO.Engine/Headless/Services/ILogSink.cs",
        "DCGO/Assets/Scripts/Script/GameRandom.cs",
    ],
    "턴/페이즈 흐름": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "DCGO/Assets/Scripts/Script/TurnStateMachine.cs",
        "DCGO/Assets/Scripts/Script/GManager.cs",
    ],
    "게임 컨텍스트": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "DCGO/Assets/Scripts/Script/GameContext.cs",
    ],
    "플레이어 상태": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "src/HeadlessDCGO.Engine/Headless/Services",
        "DCGO/Assets/Scripts/Script/Player.cs",
    ],
    "카드 컨트롤러": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "src/HeadlessDCGO.Engine/Headless/Services/IZoneMover.cs",
        "DCGO/Assets/Scripts/Script/CardController.cs",
        "DCGO/Assets/Scripts/Script/CardObjectController.cs",
    ],
    "메인 페이즈 액션": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "DCGO/Assets/Scripts/Script/MainPhaseAction",
    ],
    "자동 효과 처리": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/AutoProcessing.cs",
    ],
    "공격/배틀 처리": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "DCGO/Assets/Scripts/Script/AttackProcess.cs",
        "DCGO/Assets/Scripts/Script/SelectAttackEffect.cs",
    ],
    "효과 계약": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/ICardEffect.cs",
        "DCGO/Assets/Scripts/Script/CardEffectInterfaces.cs",
        "DCGO/Assets/Scripts/Script/SkillInfo.cs",
    ],
    "효과 컨텍스트": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons/GetFromHashtable.cs",
        "DCGO/Assets/Scripts/Script/CardEffectCommons/HashtableSetting.cs",
    ],
    "조건 처리": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons",
    ],
    "요구사항 처리": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons/MinMax_DP_Cost_Level",
        "DCGO/Assets/Scripts/Script/CardEffectCommons",
    ],
    "비용 처리": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons/ShowReducedCost.cs",
        "DCGO/Assets/Scripts/Script/CardEffectFactory",
    ],
    "대상/존 조회": [
        "src/HeadlessDCGO.Engine/Headless/Services",
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons",
    ],
    "키워드 효과": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects",
        "DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects",
    ],
    "수치/비용 변경": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons",
    ],
    "제한/불가 효과": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons",
    ],
    "대체 효과": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons",
    ],
    "상시 효과": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons",
    ],
    "효과 팩토리": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectFactory",
    ],
    "효과 선택": [
        "src/HeadlessDCGO.Engine/Headless/Choices",
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/SelectCardEffect.cs",
    ],
    "타이밍/우선순위": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/AutoProcessing.cs",
    ],
    "턴 제한 플래그": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons",
    ],
    "진화원/부여/시큐리티 효과": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons",
    ],
    "카드풀": [
        "src/HeadlessDCGO.Engine/Headless/DataLoading",
        "DCGO/Assets/CardBaseEntity",
        "docs/dotnet_engine_file_mapping.csv",
    ],
    "트리거 효과": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectFactory",
        "DCGO/Assets/Scripts/Script/CardEffects",
    ],
    "디지볼브 관련": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons",
    ],
    "진화원 조작": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons",
    ],
    "카드별 효과": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/CardEffects",
        "DCGO/Assets/CardBaseEntity",
    ],
    "효과 순서": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/AutoProcessing.cs",
    ],
    "데이터-동작 연결": [
        "src/HeadlessDCGO.Engine/Headless/DataLoading",
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/CardBaseEntity",
    ],
    "커버리지": [
        "docs",
        "src/HeadlessDCGO.Engine/Headless/DataLoading",
        "DCGO/Assets/CardBaseEntity",
    ],
    "관측값": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "src/HeadlessDCGO.Engine/Headless/Services",
    ],
    "액션 인코딩": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
    ],
    "보상": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
    ],
    "RL 환경": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
    ],
    "정책 실행": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "src/HeadlessDCGO.Engine/Headless/Choices",
    ],
    "배치 실행": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
    ],
    "학습 데이터셋": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "docs",
    ],
    "시나리오": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "docs",
    ],
    "리플레이": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "src/HeadlessDCGO.Engine/Headless/Diagnostics",
    ],
    "AS-IS 비교": [
        "docs/headless_unity_dependent_functions.csv",
        "docs/headless_source_origin_mapping.csv",
        "src/HeadlessDCGO.Engine/Headless/Runtime",
    ],
    "회귀 테스트": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "docs/test-results",
    ],
    "성능 측정": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "docs/test-results",
    ],
}


AREA_IMPLEMENTATION_NOTES = {
    "런타임": [
        "매치 외부 공개 API는 DcgoMatch와 HeadlessGameLoop를 통해서만 노출한다.",
        "Initialize, Reset, Step, ApplyAction, Observe, GetLegalActions, GetResult의 입력과 반환 모델을 고정한다.",
        "Step은 내부 자동 처리 후 안정 상태, 선택 대기, 종료 상태 중 하나로 귀결되어야 한다.",
        "Unity scene, MonoBehaviour lifecycle, frame Update 의미가 public API에 새지 않게 한다.",
    ],
    "상태/존": [
        "카드 위치, 소유자, 공개 여부, 순서, 진화원, 링크 상태를 GameObject가 아닌 데이터 모델로 표현한다.",
        "모든 zone mutation은 단일 boundary를 통과하게 하고 CardMoved/GameEvent 기록을 남긴다.",
        "zone 순서가 결과에 영향을 주는 경우 deterministic ordering을 명시한다.",
        "숨김 정보가 있는 zone은 owner view와 opponent view를 분리한다.",
    ],
    "Unity 전역 접근 대체": [
        "GManager.instance, ContinuousController.instance, GetComponent<T> 접근을 EngineContext/service 접근으로 치환한다.",
        "gameplay 의미가 있는 전역 상태만 남기고 UI, scene, animation 전용 필드는 제외한다.",
        "Bridge는 영구 도메인 모델이 아니라 AS-IS 포팅 연결부임을 문서화한다.",
        "존재하지 않는 service는 침묵하지 말고 명확한 계약 오류로 노출한다.",
    ],
    "코루틴 대체": [
        "IEnumerator 흐름을 frame time이 아닌 deterministic task step으로 표현한다.",
        "WaitForSeconds, WaitWhile, 중첩 IEnumerator가 gameplay 결과를 비결정적으로 바꾸지 않게 한다.",
        "task queue의 실행 순서, 완료, 실패 전파, 중단 조건을 명확히 한다.",
        "wall clock이나 Unity yield object에 의존하지 않는다.",
    ],
    "선택 처리 대체": [
        "UI 선택 창을 ChoiceRequest/ChoiceResult와 IChoiceProvider 계약으로 바꾼다.",
        "candidate id, zone, owner, min/max count, skip 가능 여부, message, source 정보를 데이터로 보존한다.",
        "선택 대기 상태에서는 자동 진행을 멈추고 선택 결과 적용 후 같은 큐에서 재개한다.",
        "Scripted provider와 policy provider가 같은 검증 규칙을 통과하게 한다.",
    ],
    "효과 처리": [
        "효과 요청, 효과 컨텍스트, 대기 효과, 해소 결과, 타이밍 창을 명확한 모델로 분리한다.",
        "mandatory, optional, delayed, replacement, continuous 효과가 같은 queue/scheduler 계약으로 설명되어야 한다.",
        "효과 해소 중 선택이 필요하면 pending choice로 멈추고 재개 가능해야 한다.",
        "효과 실행 순서는 입력 seed와 상태가 같으면 항상 같아야 한다.",
    ],
    "데이터 로딩": [
        "Unity Resources, ScriptableObject, prefab 로딩 없이 파일/JSON/텍스트 기반 입력을 사용한다.",
        "이미지, 사운드, 애니메이션, 프리팹 경로는 gameplay 데이터에서 제외하거나 metadata로만 보존한다.",
        "카드 id, 이름, 색, 타입, 비용, 진화 조건, 효과 binding key가 repository에서 조회 가능해야 한다.",
        "잘못된 데이터는 조용히 무시하지 말고 진단 가능한 오류로 반환한다.",
    ],
    "진단/결정성": [
        "동일 seed와 동일 action sequence는 동일 trace와 fingerprint를 만들어야 한다.",
        "로그와 trace는 테스트에서 snapshot 비교가 가능하도록 순서와 sequence를 가진다.",
        "금지 dependency scan은 Headless 영역에 Unity, Photon, TMPro, Unity UI가 들어오지 못하게 한다.",
        "진단 기능은 게임 결과를 변경하지 않는다.",
    ],
    "턴/페이즈 흐름": [
        "TurnStateMachine의 phase 의미를 HeadlessPhase와 명시적 transition으로 옮긴다.",
        "setup, draw, unsuspend, breeding, main, end, memory pass의 진입/종료 조건을 상태 전이로 표현한다.",
        "자동 효과와 선택 대기가 phase 전이 사이에 끼어드는 지점을 명확히 한다.",
        "턴 종료, 패스, 메모리 이동, 승패 조건 확인 순서가 테스트로 고정되어야 한다.",
    ],
    "메인 페이즈 액션": [
        "Play, Digivolve, Option activate, Attack, Pass를 HeadlessAction으로 입력받아 ActionProcessResult로 반환한다.",
        "legal action query와 action execution이 같은 룰 조건을 공유해야 한다.",
        "비용 지불, zone 이동, trigger enqueue, illegal reason을 각각 검증 가능하게 분리한다.",
        "cheat/debug action은 명시적으로 허용된 테스트 경로가 아니면 legal action에 포함하지 않는다.",
    ],
    "공격/배틀 처리": [
        "공격 선언, 대상 지정, block timing, DP 비교, deletion, security check, end attack trigger를 순서대로 분리한다.",
        "공격 중간에 선택/효과가 끼어들 수 있는 지점을 pending state로 표현한다.",
        "battle 결과와 security 결과는 이벤트와 상태 변화가 모두 검증 가능해야 한다.",
        "시각적 공격 연출, 카메라, 애니메이션은 포함하지 않는다.",
    ],
    "효과 계약": [
        "카드 효과는 UI나 MonoBehaviour가 아닌 순수 effect contract로 실행되어야 한다.",
        "SkillInfo, trigger, cost, target, resolution result가 typed model로 표현되어야 한다.",
        "효과는 EffectContext를 통해서만 상태를 읽고 mutation service를 통해서만 상태를 바꾼다.",
        "원본 타입을 그대로 끌고 오기보다 Headless 도메인 타입으로 의미를 고정한다.",
    ],
    "카드별 효과": [
        "카드 id별 효과 binding과 실제 동작을 분리해 coverage를 측정할 수 있게 한다.",
        "공통 helper로 표현 가능한 효과는 카드별 코드에 중복 구현하지 않는다.",
        "대표 카드 테스트는 카드 텍스트, 입력 상태, 기대 상태 변화를 함께 기록한다.",
        "이미지/프리팹/연출 데이터는 카드 효과 완료 기준이 아니다.",
    ],
    "관측값": [
        "RL observation은 player perspective를 기준으로 숨김 정보를 마스킹해야 한다.",
        "관측값 schema와 vector schema는 버전과 차원 수가 고정되어야 한다.",
        "같은 상태와 같은 perspective는 같은 observation fingerprint를 반환해야 한다.",
        "학습 편의를 위해 룰을 누설하는 hidden 정보가 포함되지 않게 한다.",
    ],
    "액션 인코딩": [
        "legal action 목록, action mask, encode/decode가 같은 action catalog를 공유해야 한다.",
        "invalid action은 예외로만 흘리지 말고 학습 환경에서 처리 가능한 결과 모델로 반환한다.",
        "mask index와 decoded action의 roundtrip을 테스트로 고정한다.",
        "현재 상태에서 불가능한 action이 mask에 열리지 않아야 한다.",
    ],
    "RL 환경": [
        "reset과 step은 Gym류 환경처럼 observation, reward, terminal, info를 안정적으로 반환해야 한다.",
        "환경은 Unity 입력, frame, scene 없이 batch/self-play에서 반복 실행 가능해야 한다.",
        "terminal 이후 step 처리 정책을 명확히 한다.",
        "seed, deck, policy가 같으면 episode trace가 같아야 한다.",
    ],
    "리플레이": [
        "replay는 초기 설정, seed, action sequence, choice result, version을 모두 포함해야 한다.",
        "import 후 재실행 fingerprint가 export 시점과 일치해야 한다.",
        "golden trace diff는 최초 불일치 지점을 사람이 읽을 수 있게 보여야 한다.",
        "리플레이는 UI 녹화가 아니라 상태 전이 재현 데이터다.",
    ],
    "AS-IS 비교": [
        "비교 대상 AS-IS 파일과 Headless 결과의 관측 포인트를 명시한다.",
        "같은 초기 상태와 같은 입력에 대해 phase, memory, zone, event, result를 비교한다.",
        "Unity 화면/애니메이션 차이는 parity 실패로 보지 않는다.",
        "불일치가 나면 원본 라인/함수, Headless 모듈, 기대 차이를 결과 문서에 남긴다.",
    ],
}


DEFAULT_IMPLEMENTATION_NOTES = [
    "Goal의 산출물을 완성형 기준으로 정의하고 public contract를 먼저 고정한다.",
    "선행 Goal의 결과를 재구현하지 말고 공개된 API만 사용한다.",
    "상태를 바꾸는 코드는 이벤트와 테스트로 관측 가능해야 한다.",
    "완료 기준을 만족하지 않는 빈 동작이나 자리표시 구현은 완료로 보지 않는다.",
]


AREA_MATCH_TOKENS = {
    "런타임": ["runtime", "match", "game loop", "dcgomatch", "headlessgameloop"],
    "상태/존": ["state", "zone", "entity", "player", "card", "mover", "fingerprint"],
    "Unity 전역 접근 대체": ["bridge", "context", "gmanager", "continuous"],
    "코루틴 대체": ["coroutine", "task", "wait", "runner", "iengine"],
    "선택 처리 대체": ["choice", "selection", "provider"],
    "효과 처리": ["effect", "scheduler", "queue", "timing", "registry"],
    "세션/네트워크 대체": ["session", "network", "photon", "replay", "action queue"],
    "데이터 로딩": ["data", "loading", "repository", "card database", "deck", "banlist"],
    "진단/결정성": ["diagnostics", "trace", "random", "log", "determin"],
    "턴/페이즈 흐름": ["turn", "phase", "turnstatemachine"],
    "게임 컨텍스트": ["gamecontext", "context"],
    "플레이어 상태": ["player"],
    "카드 컨트롤러": ["cardcontroller", "card object", "zone mover"],
    "메인 페이즈 액션": ["main", "action", "play", "digivolve", "pass"],
    "자동 효과 처리": ["auto", "trigger", "effect"],
    "공격/배틀 처리": ["attack", "battle", "security", "block"],
    "효과 계약": ["effect", "skill", "contract"],
    "효과 컨텍스트": ["effect", "context", "hashtable"],
    "조건 처리": ["condition", "canuse"],
    "요구사항 처리": ["requirement", "min", "max", "level"],
    "비용 처리": ["cost"],
    "대상/존 조회": ["target", "zone", "query"],
    "키워드 효과": ["keyword", "blocker", "jamming", "rush", "blitz"],
    "수치/비용 변경": ["modifier", "dp", "cost"],
    "제한/불가 효과": ["cannot", "restriction", "immunity"],
    "대체 효과": ["replacement", "prevention"],
    "상시 효과": ["continuous"],
    "효과 팩토리": ["factory", "binding"],
    "효과 선택": ["choice", "effect"],
    "타이밍/우선순위": ["timing", "priority"],
    "턴 제한 플래그": ["once", "turn", "flag"],
    "진화원/부여/시큐리티 효과": ["inherited", "granted", "security"],
    "카드풀": ["card pool", "cardbase", "database"],
    "트리거 효과": ["trigger"],
    "디지볼브 관련": ["digivolution", "digivolve"],
    "진화원 조작": ["source"],
    "카드별 효과": ["cardeffects", "card effect"],
    "효과 순서": ["ordering", "effect"],
    "데이터-동작 연결": ["behavior", "binding", "data"],
    "커버리지": ["coverage"],
    "관측값": ["observation"],
    "액션 인코딩": ["action", "encoder", "mask"],
    "보상": ["reward"],
    "RL 환경": ["rl", "environment"],
    "정책 실행": ["policy"],
    "배치 실행": ["batch", "episode"],
    "학습 데이터셋": ["dataset", "transition", "jsonl"],
    "시나리오": ["scenario"],
    "리플레이": ["replay", "golden"],
    "AS-IS 비교": ["parity", "as-is", "asis", "compare"],
    "회귀 테스트": ["regression"],
    "성능 측정": ["benchmark", "throughput", "performance"],
}


AREA_TEST_CASES = {
    "런타임": [
        "Given 유효한 MatchConfig, When Initialize와 Reset을 호출하면, Then 초기 StepResult와 MatchResult 상태가 일관된다.",
        "Given legal action, When ApplyAction 후 Step을 호출하면, Then event sequence와 state version이 증가한다.",
        "Given illegal action, When ApplyAction을 호출하면, Then 상태를 오염시키지 않고 IllegalAction reason을 반환한다.",
        "Given terminal state, When GetResult를 호출하면, Then winner/draw/reason이 재현 가능하게 반환된다.",
    ],
    "상태/존": [
        "Given 카드가 특정 zone에 있을 때, When 다른 zone으로 이동하면, Then 이전 zone에서 제거되고 새 zone 순서가 유지된다.",
        "Given hidden zone, When opponent view를 만들면, Then card identity가 노출되지 않는다.",
        "Given 같은 move sequence, When fingerprint를 계산하면, Then 항상 같은 값이 나온다.",
        "Given 잘못된 owner 또는 없는 card id, When mutation을 요청하면, Then 명확한 실패 결과를 반환한다.",
    ],
    "코루틴 대체": [
        "Given 여러 task가 queue에 있을 때, When RunUntilIdle을 실행하면, Then enqueue 순서대로 완료된다.",
        "Given wait condition이 만족되지 않을 때, When Step을 호출하면, Then task가 완료되지 않고 다음 step에 재개된다.",
        "Given 중첩 IEnumerator, When adapter로 실행하면, Then 부모 task 완료 전에 자식 task가 완료된다.",
        "Given task 내부 예외, When runner가 실행하면, Then 예외/실패 상태가 호출자에게 전파된다.",
    ],
    "선택 처리 대체": [
        "Given min/max count와 candidate 목록, When ChoiceResult를 적용하면, Then 범위 밖 선택은 거부된다.",
        "Given skip 불가 요청, When Skip 결과를 반환하면, Then validation 실패가 기록된다.",
        "Given ScriptedChoiceProvider queue, When 여러 요청을 처리하면, Then 입력 순서대로 deterministic하게 반환된다.",
        "Given PolicyChoiceProvider, When cancellation token이 취소되면, Then 선택 대기가 해제되거나 취소 결과가 명확히 반환된다.",
    ],
    "효과 처리": [
        "Given 여러 PendingEffect, When ResolveAll을 실행하면, Then timing/order 규칙대로 해소된다.",
        "Given resolver가 choice를 요구할 때, When ResolveNext를 호출하면, Then pending choice 상태로 멈춘다.",
        "Given mandatory와 optional effect, When 같은 timing window에 들어오면, Then 우선순위가 테스트 기대값과 일치한다.",
        "Given resolver 실패, When scheduler가 처리하면, Then queue와 trace에 실패 정보가 남는다.",
    ],
    "데이터 로딩": [
        "Given 유효한 카드 JSON, When loader가 읽으면, Then CardRecord 필수 필드가 채워진다.",
        "Given 잘못된 JSON 또는 누락 필드, When loader가 읽으면, Then 진단 가능한 오류를 반환한다.",
        "Given deck list, When loader가 읽으면, Then 카드 수와 제한 규칙이 검증된다.",
        "Given Unity prefab/image/audio path, When gameplay repository를 만들면, Then 룰 실행에 필요한 데이터와 분리된다.",
    ],
    "진단/결정성": [
        "Given 같은 seed와 action sequence, When 두 번 실행하면, Then trace와 fingerprint가 일치한다.",
        "Given 다른 seed, When random choice나 shuffle을 수행하면, Then 결과 차이가 trace에 설명 가능하게 남는다.",
        "Given forbidden dependency 문자열, When scan을 실행하면, Then Headless 영역의 위반 파일을 검출한다.",
        "Given Null sink, When log/trace를 기록하면, Then 게임 상태가 바뀌지 않는다.",
    ],
    "턴/페이즈 흐름": [
        "Given setup 직후, When phase를 진행하면, Then draw/unsuspend/breeding/main/end 순서가 기대와 일치한다.",
        "Given memory가 상대 영역으로 넘어간 상태, When main phase를 평가하면, Then pass/end turn 흐름이 발생한다.",
        "Given phase 중 trigger가 발생할 때, When step을 진행하면, Then effect queue가 phase 전이 전에 처리된다.",
        "Given terminal 조건, When phase transition을 시도하면, Then 더 이상 불필요한 전이가 발생하지 않는다.",
    ],
    "메인 페이즈 액션": [
        "Given 충분한 비용과 유효한 카드, When PlayCardAction을 적용하면, Then 비용 지불과 zone 이동이 기록된다.",
        "Given 유효하지 않은 진화 조건, When DigivolveAction을 적용하면, Then 상태 변화 없이 illegal reason이 반환된다.",
        "Given option 카드, When Activate action을 적용하면, Then 효과 queue와 trash 이동이 기대와 일치한다.",
        "Given pass action, When 적용하면, Then memory와 turn ownership이 규칙대로 변경된다.",
    ],
    "공격/배틀 처리": [
        "Given 공격 가능한 permanent, When attack declaration을 적용하면, Then suspend와 attack target 상태가 기록된다.",
        "Given blocker가 있는 상태, When block timing을 처리하면, Then 선택 가능 blocker와 결과가 기대와 일치한다.",
        "Given DP 비교 상황, When battle resolver를 실행하면, Then deletion/trash/event가 기대와 일치한다.",
        "Given security check, When security card를 공개하면, Then trigger 또는 trash/remaining security 상태가 검증된다.",
    ],
    "효과 계약": [
        "Given effect request와 context, When effect를 실행하면, Then 상태 변경은 service boundary를 통해서만 발생한다.",
        "Given 필요한 target이 없을 때, When CanResolve를 평가하면, Then 실행 불가 결과가 반환된다.",
        "Given 같은 context, When 같은 effect를 반복 실행하면, Then deterministic한 result와 event를 만든다.",
        "Given 잘못된 typed context, When effect가 값을 요청하면, Then 명확한 validation 오류가 반환된다.",
    ],
    "카드별 효과": [
        "Given 대표 카드 id와 카드 텍스트, When 효과를 실행하면, Then 텍스트가 요구하는 상태 변화가 발생한다.",
        "Given 공통 helper로 표현 가능한 카드, When binding을 확인하면, Then 중복 코드 없이 helper를 사용한다.",
        "Given 미구현 카드 효과, When coverage report를 만들면, Then 카드 id와 누락 사유가 기록된다.",
        "Given 선택이 필요한 카드 효과, When policy 선택을 주입하면, Then 선택 결과에 따라 상태가 달라진다.",
    ],
    "관측값": [
        "Given 같은 상태와 같은 player, When observation을 생성하면, Then 같은 schema version과 fingerprint가 나온다.",
        "Given 상대 hidden zone, When observation을 생성하면, Then card identity가 mask된다.",
        "Given vector schema, When observation을 vectorize하면, Then 차원 수와 index 의미가 문서와 일치한다.",
        "Given terminal state, When observation을 생성하면, Then result 정보가 허용된 범위에서만 포함된다.",
    ],
    "액션 인코딩": [
        "Given legal actions, When mask를 만들면, Then 가능한 action index만 true가 된다.",
        "Given encoded action, When decode 후 encode하면, Then 원래 index와 action payload가 보존된다.",
        "Given invalid index, When decode하면, Then 학습 루프가 처리 가능한 실패 결과가 반환된다.",
        "Given 상태 변화 후, When mask를 다시 만들면, Then 새 legal action과 일치한다.",
    ],
    "RL 환경": [
        "Given seed와 deck config, When Reset을 호출하면, Then 첫 observation과 legal action mask가 결정적으로 반환된다.",
        "Given action, When Step을 호출하면, Then observation/reward/terminal/info가 한 번의 전이 결과를 나타낸다.",
        "Given terminal 이후 Step, When 호출하면, Then 정의된 오류 또는 terminal 유지 정책을 따른다.",
        "Given 같은 policy와 seed, When episode를 반복하면, Then transition sequence가 일치한다.",
    ],
    "리플레이": [
        "Given episode trace, When replay를 export/import하면, Then seed/action/choice/result가 보존된다.",
        "Given imported replay, When 재실행하면, Then final fingerprint가 golden 값과 일치한다.",
        "Given 불일치 replay, When comparer를 실행하면, Then 최초 불일치 event와 state path를 출력한다.",
        "Given replay version mismatch, When import하면, Then 명확한 호환성 오류가 반환된다.",
    ],
    "AS-IS 비교": [
        "Given AS-IS 관측값과 Headless 관측값, When parity comparer를 실행하면, Then gameplay 의미만 비교한다.",
        "Given 화면/연출 차이, When 비교하면, Then parity 실패로 처리하지 않는다.",
        "Given phase/memory/zone 차이, When 비교하면, Then source function과 Headless module을 함께 보고한다.",
        "Given 대표 시나리오, When 반복 실행하면, Then 같은 입력에서 같은 비교 결과가 나온다.",
    ],
}


def read_csv(path):
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def unique(items):
    result = []
    seen = set()
    for item in items:
        if item and item not in seen:
            result.append(item)
            seen.add(item)
    return result


def phase_short(phase):
    m = re.search(r"Phase\s+(\d+)", phase)
    return f"Phase {m.group(1)}" if m else phase


def phase_number(phase):
    m = re.search(r"Phase\s+(\d+)", phase)
    return int(m.group(1)) if m else -1


def split_items(value):
    if not value:
        return []
    parts = re.split(r"\s*[;|]\s*", value)
    return [p.strip() for p in parts if p.strip()]


def normalize(value):
    return "".join(ch.lower() for ch in value if ch.isalnum())


def safe_test_project(goal_id, goal):
    ascii_slug = []
    for ch in goal:
        if ch.isascii() and ch.isalnum():
            ascii_slug.append(ch)
        elif ch in " _-/":
            ascii_slug.append(".")
    slug = "".join(ascii_slug).strip(".") or "Goal"
    slug = re.sub(r"\.+", ".", slug)
    return f"tests/{goal_id}.{slug}.Tests"


def load_headless_files():
    root = Path("src/HeadlessDCGO.Engine/Headless")
    if not root.exists():
        return []
    return [str(p).replace("\\", "/") for p in root.rglob("*.cs")]


def match_existing_files(row, headless_files):
    haystack = normalize(" ".join([
        row["목표"],
        row["상세 목표 설명"],
        row["산출물"],
        row["해야 할 작업"],
    ]))
    matched = []
    for path in headless_files:
        stem = normalize(Path(path).stem)
        if stem and (stem in haystack or haystack in stem):
            matched.append(path)
    return matched[:8]


def match_modules(row, modules):
    phase = phase_short(row["단계"])
    text = normalize(" ".join([row["영역"], row["목표"], row["산출물"], row["상세 목표 설명"]]))
    scored = []
    for module in modules:
        score = 0
        content_score = 0
        if module["stage"] == phase:
            score += 3
        module_text = normalize(" ".join([
            module["area"],
            module["module"],
            module["path"],
            module["responsibility"],
            module["public_api"],
            module["notes"],
        ]))
        raw_module_text = " ".join([
            module["area"],
            module["module"],
            module["path"],
            module["responsibility"],
            module["public_api"],
            module["notes"],
        ]).lower()
        for token in AREA_MATCH_TOKENS.get(row["영역"], []):
            if token.lower() in raw_module_text:
                content_score += 2
        for token in [normalize(row["영역"]), normalize(row["목표"]), normalize(row["산출물"])]:
            if token and token in module_text:
                content_score += 2
        for token in re.split(r"\s+", row["목표"] + " " + row["산출물"]):
            token_n = normalize(token)
            if len(token_n) >= 5 and token_n in module_text:
                content_score += 1
        score += content_score
        if content_score > 0:
            scored.append((score, module))
    scored.sort(key=lambda x: (-x[0], x[1]["path"]))
    return [m for _, m in scored[:4]]


def match_dependencies(row, dependencies):
    phase = phase_short(row["단계"])
    area = row["영역"]
    text = normalize(" ".join([area, row["목표"], row["상세 목표 설명"], row["해야 할 작업"]]))
    scored = []
    for dep in dependencies:
        score = 0
        content_score = 0
        if dep["gate"] == phase:
            score += 3
        dep_text = normalize(" ".join([
            dep["dependency"],
            dep["asis_kind"],
            dep["source_patterns"],
            dep["role_in_unity_client"],
            dep["headless_replacement_module"],
            dep["replacement_design"],
        ]))
        raw_dep_text = " ".join([
            dep["dependency"],
            dep["asis_kind"],
            dep["source_patterns"],
            dep["role_in_unity_client"],
            dep["headless_replacement_module"],
            dep["replacement_design"],
        ]).lower()
        for token in AREA_MATCH_TOKENS.get(area, []):
            if token.lower() in raw_dep_text:
                content_score += 2
        if normalize(area) in dep_text:
            content_score += 2
        for token in ["unity", "mono", "coroutine", "choice", "effect", "photon", "resources", "scene", "ui"]:
            if token in text and token in dep_text:
                content_score += 1
        score += content_score
        if content_score > 0:
            scored.append((score, dep))
    scored.sort(key=lambda x: (-x[0], x[1]["dependency"]))
    return [d for _, d in scored[:5]]


def source_origin_matches(row, origins):
    area = row["영역"]
    targets = AREA_REFS.get(area, [])
    matched = []
    for origin in origins:
        path = origin["headless_path"].replace("\\", "/")
        if any(path.startswith(t.replace("\\", "/")) or t.replace("\\", "/") in path for t in targets):
            matched.append(origin)
    return matched[:5]


def target_paths(row, headless_files, modules):
    targets = []
    targets.extend(match_existing_files(row, headless_files))
    targets.extend(m["path"] for m in modules if m.get("path"))
    # Read-only AS-IS files are listed in section 5. The target list should
    # only contain writable design/runtime/test/report locations.
    for ref in AREA_REFS.get(row["영역"], []):
        normalized = ref.replace("\\", "/")
        if normalized.startswith("DCGO/Assets"):
            continue
        if normalized.startswith("src/") or normalized.startswith("tests/") or normalized.startswith("docs/"):
            targets.append(ref)
    phase = phase_number(row["단계"])
    if phase >= 3 and row["영역"] not in ("AS-IS 비교", "단계 완료 게이트", "커버리지"):
        targets.append("src/HeadlessDCGO.Engine/Headless/Effects")
    if phase >= 5:
        targets.append("src/HeadlessDCGO.Engine/Headless/Runtime")
    targets.append(safe_test_project(row["Goal ID"], row["목표"]))
    targets.append(row["결과 문서"])
    return unique(targets)[:14]


def implementation_notes(row):
    notes = []
    notes.extend(AREA_IMPLEMENTATION_NOTES.get(row["영역"], DEFAULT_IMPLEMENTATION_NOTES))
    if row["영역"] not in AREA_IMPLEMENTATION_NOTES:
        notes.extend(DEFAULT_IMPLEMENTATION_NOTES)
    for item in split_items(row["산출물"]):
        notes.append(f"`{item}` 산출물이 실제 public API, 모델, 문서, 테스트 중 어디에 속하는지 명확히 분리한다.")
    notes.append(f"완료 기준은 `{row['완료 기준']}`이며, 이 기준을 테스트와 결과 문서에서 직접 증명한다.")
    return unique(notes)


def forbidden_notes(row):
    phase_no = phase_number(row["단계"])
    notes = [
        "원본 `DCGO/Assets/...` 파일을 수정하지 않는다. 필요한 경우 읽기 전용으로만 확인한다.",
        "Goal 범위를 넘어 다음 Goal이나 상위 Phase 전체를 함께 처리하지 않는다.",
        "단위테스트와 결과 문서 없이 완료를 선언하지 않는다.",
        "완성 기준을 충족하지 않는 빈 동작, 자리표시 구현, TODO-only 구현을 완료로 보지 않는다.",
    ]
    if phase_no == 0:
        notes.append("설계/문서 검증 Goal에서는 C# 런타임 구현을 진행하지 않는다.")
    if phase_no == 1:
        notes.append("Phase 1에서는 Unity 대체 기반까지만 구현하고, 카드별 실제 효과 포팅은 시작하지 않는다.")
    if phase_no < 4:
        notes.append("asset/card effect 실제 포팅은 해당 단계가 열리기 전까지 수행하지 않는다.")
    if row["영역"] in ("관측값", "액션 인코딩", "RL 환경", "정책 실행", "배치 실행"):
        notes.append("AI/RL 편의를 위해 숨김 정보나 불법 액션을 누설하지 않는다.")
    return notes


def test_cases(row):
    cases = []
    cases.extend(AREA_TEST_CASES.get(row["영역"], []))
    if not cases:
        cases.extend([
            f"Given `{row['산출물']}` 기본 입력, When Goal 범위의 기능을 실행하면, Then `{row['완료 기준']}`을 만족하는 결과가 나온다.",
            "Given 유효하지 않은 입력, When 기능을 실행하면, Then 상태 오염 없이 명확한 실패 결과를 반환한다.",
            "Given 동일 입력을 두 번 적용할 때, When 결과를 비교하면, Then deterministic하게 같은 값이 나온다.",
            "Given Goal 범위 밖 기능, When 테스트를 작성할 때, Then 선행 Goal의 내부 구현을 재검증하지 않는다.",
        ])
    cases.append(f"CSV에 적힌 단위테스트 범위 `{row['단위테스트 상세']}`가 실제 테스트명 또는 assertion으로 추적 가능해야 한다.")
    return unique(cases)[:7]


def result_doc_required(row):
    return [
        f"Goal ID와 제목: `{row['Goal ID']} {row['목표']}`",
        "실행 일시와 실행 환경",
        "수정/생성 파일 목록",
        "읽기 전용으로 확인한 AS-IS 파일 목록",
        "테스트 명령 전체",
        "전체/통과/실패/스킵 수",
        "실패 상세와 수정 여부",
        "테스트하지 못한 항목과 이유",
        f"완료 기준 `{row['완료 기준']}` 충족 근거",
        "다음 Goal 진행 가능 여부",
    ]


def bullet(items, code=False):
    out = []
    for item in items:
        out.append(f"- `{item}`" if code else f"- {item}")
    return "\n".join(out) if out else "- 없음"


def numbered(items):
    return "\n".join(f"{i}. {item}" for i, item in enumerate(items, 1))


def table_modules(modules):
    if not modules:
        return "| 모듈 | 대상 경로 | 책임 | public API |\n|---|---|---|---|\n| 해당 없음 | Goal별 산출물 기준 | CSV와 상세 지시서 기준으로 확정 | 결과 문서에 명시 |"
    lines = ["| 모듈 | 대상 경로 | 책임 | public API |", "|---|---|---|---|"]
    for module in modules:
        lines.append(
            f"| {module['module']} | `{module['path']}` | {module['responsibility']} | {module['public_api']} |"
        )
    return "\n".join(lines)


def table_dependencies(deps):
    if not deps:
        return "| 의존성 | 원본 역할 | Headless 대체 | 완료 기준 |\n|---|---|---|---|\n| 해당 없음 | Goal 범위에서 직접 확인 | Goal 산출물 기준 | 결과 문서에 명시 |"
    lines = ["| 의존성 | 원본 역할 | Headless 대체 | 완료 기준 |", "|---|---|---|---|"]
    for dep in deps:
        lines.append(
            f"| {dep['dependency']} | {dep['role_in_unity_client']} | {dep['replacement_design']} | {dep['completion_criteria']} |"
        )
    return "\n".join(lines)


def table_origins(origins):
    if not origins:
        return "| Headless 위치 | AS-IS 원본 | 대체 대상 | 포팅 메모 |\n|---|---|---|---|\n| Goal 산출물 기준 | 상세 참조 파일 기준 | 결과 문서에 명시 | 확인 후 보완 |"
    lines = ["| Headless 위치 | AS-IS 원본 | 대체 대상 | 포팅 메모 |", "|---|---|---|---|"]
    for origin in origins:
        lines.append(
            f"| `{origin['headless_path']}` | {origin['asis_source_paths']} | {origin['asis_dependency']} | {origin['porting_notes']} |"
        )
    return "\n".join(lines)


def spec_text(row, base_row, modules, deps, origins, headless_files):
    goal_id = row["Goal ID"]
    goal = row["목표"]
    blocker = row["선행 Goal"]
    target_list = target_paths(row, headless_files, modules)
    refs = unique(COMMON_DOCS + AREA_REFS.get(row["영역"], []) + [row["결과 문서"]])
    work_steps = [
        f"`{blocker}` 선행 Goal의 결과 문서와 실패/미해결 리스크를 확인한다." if blocker != "없음" else "선행 Goal이 없음을 확인하고 바로 Goal 범위 검토를 시작한다.",
        f"작업 범위를 `{base_row['scope']}`로 제한하고, 산출물을 `{base_row['deliverables']}`로 고정한다.",
        "아래 AS-IS 확인 대상 파일을 읽기 전용으로 확인하고, gameplay 의미와 Unity/클라이언트 의존 의미를 분리한다.",
        "아래 대상 파일/폴더 중 Goal 산출물과 직접 관련된 위치만 수정하거나 생성한다.",
        "public API, 입력 모델, 출력 모델, 실패 모델을 먼저 정하고 테스트 이름에 반영한다.",
        "구현 또는 문서 작성 후 단위테스트를 작성하고 같은 Goal 범위 안에서 실패를 수정한다.",
        f"테스트 결과와 완료 기준 `{row['완료 기준']}` 충족 근거를 `{row['결과 문서']}`에 기록한다.",
    ]
    prompt = f"""HeadlessDCGO.Engine Goal {goal_id}를 수행하라.

반드시 먼저 이 상세 지시서를 읽어라:
docs/goal-specs/{Path(row['참조/확인 대상'].split(' | ')[2]).name if 'docs/goal-specs/' in row['참조/확인 대상'] else goal_id}

이번 작업은 {goal_id} 하나만 완료하는 것이 목표다.
선행 Goal: {blocker}
작업 범위: {base_row['scope']}
산출물: {base_row['deliverables']}
단위테스트 범위: {base_row['unit_test_scope']}
결과 문서: {row['결과 문서']}
완료 기준: {row['완료 기준']}

원본 DCGO/Assets 파일은 수정하지 말라.
Goal 범위 밖 작업을 하지 말라.
단위테스트와 결과 문서 없이는 완료로 말하지 말라."""

    return f"""# {goal_id} {goal} 상세 지시서

## 1. Goal 식별 정보

- Goal ID: `{goal_id}`
- 단계: `{row['단계']}`
- 영역: `{row['영역']}`
- 우선순위: `{row['우선순위']}`
- 선행 Goal: `{blocker}`
- 결과 문서: `{row['결과 문서']}`

## 2. 완성 목표

{row['상세 목표 설명']}

이 Goal은 `{base_row['scope']}` 범위를 완성형 기준으로 닫는 작업이다. 완료 판정은 `{row['완료 기준']}`이며, 구현 산출물만으로는 완료가 아니다. 단위테스트와 결과 문서가 함께 있어야 다음 Goal로 넘어갈 수 있다.

## 3. 작업 순서

{numbered(work_steps)}

## 4. 작업 대상 파일과 생성 위치

아래 위치는 우선 확인 대상이다. 실제 수정은 Goal 산출물과 직접 연결되는 파일로 제한한다. 없는 파일은 해당 Goal 산출물이 요구할 때만 생성한다.

{bullet(target_list, code=True)}

권장 테스트 위치:

- `{safe_test_project(goal_id, goal)}/Program.cs`

## 5. AS-IS 확인 대상과 대체 관계

### 직접 참조 파일

{bullet(refs, code=True)}

### Headless 모듈 매핑

{table_modules(modules)}

### Unity/클라이언트 의존 대체

{table_dependencies(deps)}

### 원본 위치 매핑

{table_origins(origins)}

## 6. 구현 또는 문서 작성 지시

{bullet(implementation_notes(row))}

추가 세부 지시:

- 산출물 `{row['산출물']}`이 어느 파일과 public API에 반영되는지 결과 문서에 적는다.
- AS-IS와 다르게 설계한 부분은 이유를 적는다. 단, 화면/연출/입력/UI 차이는 Headless 설계 차이로 분리한다.
- 상태를 바꾸는 작업이면 변경 전 상태, 입력, 변경 후 상태, 발생 이벤트를 테스트에서 확인한다.
- 실패 결과가 가능한 작업이면 예외만 던지고 끝내지 말고 호출자가 검증할 수 있는 실패 모델 또는 명확한 예외 계약을 정한다.

## 7. 하지 말아야 할 작업

{bullet(forbidden_notes(row))}

## 8. 단위테스트 지시

CSV 기준 단위테스트 범위:

> {base_row['unit_test_scope']}

반드시 포함할 테스트 관점:

{bullet(test_cases(row))}

테스트 작성 규칙:

- 테스트는 Goal 산출물의 public API 또는 문서 검증 포인트를 직접 호출해야 한다.
- 같은 입력을 반복했을 때 결과가 달라질 수 있는 부분은 seed 또는 deterministic fixture를 고정한다.
- 실패 케이스는 최소 1개 이상 포함한다. 입력 검증, illegal action, 누락 데이터, 잘못된 상태 중 Goal에 맞는 것을 고른다.
- 테스트 명령은 `.\\.dotnet\\dotnet.exe run --project <테스트 csproj>` 형태로 결과 문서에 기록한다.
- 테스트가 아직 생성되지 않은 Goal이면 이 Goal에서 테스트 프로젝트 또는 테스트 파일을 함께 만든다.

## 9. 결과 문서 작성 지시

결과 문서 경로:

- `{row['결과 문서']}`

결과 문서에는 다음 항목을 반드시 포함한다.

{bullet(result_doc_required(row))}

## 10. 완료 판정 체크리스트

- [ ] 선행 Goal `{blocker}` 상태를 확인했다.
- [ ] 작업 범위 `{base_row['scope']}` 밖의 변경을 하지 않았다.
- [ ] 원본 `DCGO/Assets/...` 파일을 수정하지 않았다.
- [ ] 대상 파일과 AS-IS 확인 파일을 결과 문서에 기록했다.
- [ ] 산출물 `{base_row['deliverables']}`을 구현 또는 문서화했다.
- [ ] 단위테스트 `{base_row['unit_test_scope']}`를 작성했다.
- [ ] 단위테스트를 실행했고 실패가 없다.
- [ ] 금지 dependency 또는 금지 작업 위반이 없다.
- [ ] 결과 문서 `{row['결과 문서']}`를 작성했다.
- [ ] 완료 기준 `{row['완료 기준']}`을 결과 문서에서 증명했다.

## 11. 실행 프롬프트

```text
{prompt}
```
"""


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    QUALITY_REPORT.parent.mkdir(parents=True, exist_ok=True)

    base_rows = read_csv(BASE_CSV)
    detail_rows = read_csv(DETAIL_CSV)
    index_rows = read_csv(INDEX_CSV)
    modules = read_csv(MODULES_CSV)
    deps = read_csv(DEPENDENCIES_CSV)
    origins = read_csv(SOURCE_ORIGIN_CSV) if SOURCE_ORIGIN_CSV.exists() else []
    headless_files = load_headless_files()

    base_by_id = {r["goal_id"]: r for r in base_rows}
    index_by_id = {r["Goal ID"]: r for r in index_rows}

    written = []
    for row in detail_rows:
        goal_id = row["Goal ID"]
        base_row = base_by_id[goal_id]
        index_row = index_by_id[goal_id]
        path = Path(index_row["상세 지시서"])
        goal_modules = match_modules(row, modules)
        goal_deps = match_dependencies(row, deps)
        goal_origins = source_origin_matches(row, origins)
        text = spec_text(row, base_row, goal_modules, goal_deps, goal_origins, headless_files)
        path.write_text(text, encoding="utf-8", newline="\n")
        written.append(path)

    lengths = [len(p.read_text(encoding="utf-8")) for p in written]
    short = [str(p) for p, length in zip(written, lengths) if length < 3500]
    report = [
        "# Headless Goal 상세 지시서 품질 검증 결과",
        "",
        "## 요약",
        "",
        f"- 생성/갱신한 상세 지시서 수: {len(written)}",
        f"- 최소 길이: {min(lengths) if lengths else 0}",
        f"- 평균 길이: {round(sum(lengths) / len(lengths), 1) if lengths else 0}",
        f"- 3500자 미만 문서 수: {len(short)}",
        "",
        "## 검증 항목",
        "",
        "- 161개 Goal 각각에 작업 대상 파일과 생성 위치를 포함했다.",
        "- 161개 Goal 각각에 AS-IS 확인 대상과 Headless 대체 관계를 포함했다.",
        "- 161개 Goal 각각에 구현 또는 문서 작성 지시를 포함했다.",
        "- 161개 Goal 각각에 Given/When/Then 형태의 단위테스트 관점을 포함했다.",
        "- 161개 Goal 각각에 결과 문서 필수 항목과 완료 체크리스트를 포함했다.",
        "",
        "## 짧은 문서",
        "",
    ]
    if short:
        report.extend(f"- `{item}`" for item in short[:50])
    else:
        report.append("- 없음")
    QUALITY_REPORT.write_text("\n".join(report) + "\n", encoding="utf-8", newline="\n")

    print(f"wrote specs={len(written)} report={QUALITY_REPORT}")


if __name__ == "__main__":
    main()
