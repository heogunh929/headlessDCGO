import csv
from pathlib import Path

SRC = Path("docs/headless_complete_goal_breakdown.csv")
DST = Path("docs/headless_complete_goal_breakdown_ko.csv")

PHASE_MAP = {
    "Phase 0": "Phase 0 - 설계/테스트 정책 확정",
    "Phase 1": "Phase 1 - Unity 대체 기반 구현",
    "Phase 2": "Phase 2 - AS-IS 핵심 흐름 포팅",
    "Phase 3": "Phase 3 - 공통 룰/효과 인프라 포팅",
    "Phase 4": "Phase 4 - 개별 카드 효과와 카드풀 포팅",
    "Phase 5": "Phase 5 - AI/RL 어댑터",
    "Phase 6": "Phase 6 - Parity/Regression 검증",
}

AREA_MAP = {
    "Design": "설계",
    "Testing": "테스트",
    "Gate": "단계 완료 게이트",
    "Runtime": "런타임",
    "State": "상태/존",
    "Bridge": "Unity 전역 접근 대체",
    "Coroutines": "코루틴 대체",
    "Choices": "선택 처리 대체",
    "Effects": "효과 처리",
    "Session": "세션/네트워크 대체",
    "DataLoading": "데이터 로딩",
    "Diagnostics": "진단/결정성",
    "TurnStateMachine": "턴/페이즈 흐름",
    "GameContext": "게임 컨텍스트",
    "Player": "플레이어 상태",
    "CardController": "카드 컨트롤러",
    "MainPhaseAction": "메인 페이즈 액션",
    "AutoProcessing": "자동 효과 처리",
    "AttackProcess": "공격/배틀 처리",
    "EffectContract": "효과 계약",
    "EffectContext": "효과 컨텍스트",
    "Conditions": "조건 처리",
    "Requirements": "요구사항 처리",
    "Costs": "비용 처리",
    "Targeting": "대상/존 조회",
    "Keywords": "키워드 효과",
    "Modifiers": "수치/비용 변경",
    "Restrictions": "제한/불가 효과",
    "Replacement": "대체 효과",
    "Continuous": "상시 효과",
    "Factory": "효과 팩토리",
    "Selection": "효과 선택",
    "Timing": "타이밍/우선순위",
    "Flags": "턴 제한 플래그",
    "Inherited": "진화원/부여/시큐리티 효과",
    "CardPool": "카드풀",
    "Triggers": "트리거 효과",
    "Digivolution": "디지볼브 관련",
    "Sources": "진화원 조작",
    "CardSpecific": "카드별 효과",
    "Ordering": "효과 순서",
    "DataBinding": "데이터-동작 연결",
    "Coverage": "커버리지",
    "RL": "AI/RL",
    "Observation": "관측값",
    "Actions": "액션 인코딩",
    "Rewards": "보상",
    "Environment": "RL 환경",
    "Policies": "정책 실행",
    "Batch": "배치 실행",
    "Dataset": "학습 데이터셋",
    "Verification": "검증",
    "Scenario": "시나리오",
    "Replay": "리플레이",
    "Parity": "AS-IS 비교",
    "Regression": "회귀 테스트",
    "Performance": "성능 측정",
}

PRIORITY_MAP = {
    "HIGH": "높음",
    "MEDIUM": "중간",
    "LOW": "낮음",
    "NONE": "없음",
}

REPLACEMENTS = [
    ("architecture design", "아키텍처 설계 문서"),
    ("modules csv", "모듈 CSV"),
    ("dependency csv", "의존성 CSV"),
    ("porting sequence", "포팅 순서 문서"),
    ("unit test plan", "단위테스트 계획서"),
    ("unit test matrix", "단위테스트 매트릭스"),
    ("phase0 validation result", "Phase 0 검증 결과 문서"),
    ("runtime model files", "런타임 모델 파일"),
    ("DcgoMatch lifecycle API", "DcgoMatch 생명주기 API"),
    ("HeadlessAction ActionProcessResult IllegalAction model", "HeadlessAction/ActionProcessResult/IllegalAction 모델"),
    ("ObservationSnapshot LegalAction ActionMask contract", "ObservationSnapshot/LegalAction/ActionMask 계약"),
    ("Runtime test summary", "런타임 테스트 요약"),
    ("HeadlessPlayerId HeadlessEntityId registry", "HeadlessPlayerId/HeadlessEntityId 등록 구조"),
    ("MatchState PlayerState", "MatchState/PlayerState"),
    ("ZoneState ZoneId Visibility model", "ZoneState/ZoneId/Visibility 모델"),
    ("CardInstanceState", "카드 인스턴스 상태"),
    ("IZoneMover ZoneMoveRequest CardMoved event", "IZoneMover/ZoneMoveRequest/CardMoved 이벤트"),
    ("Snapshot Fingerprint service", "스냅샷/핑거프린트 서비스"),
    ("ContinuousContext MatchConfig mapping", "ContinuousContext/MatchConfig 매핑"),
    ("UnityNullObjectPolicy", "Unity 전용 객체 제외 정책"),
    ("IEngineTask EngineTaskStatus", "IEngineTask/EngineTaskStatus"),
    ("ChoiceRequest ChoiceCandidate ChoiceType ChoiceZone", "ChoiceRequest/ChoiceCandidate/ChoiceType/ChoiceZone"),
    ("ChoiceResult ChoiceOption", "ChoiceResult/ChoiceOption"),
    ("EffectRequest EffectContext EffectResult", "EffectRequest/EffectContext/EffectResult"),
    ("EffectResolutionQueue PendingEffect", "EffectResolutionQueue/PendingEffect"),
    ("TimingWindowResolver interface", "TimingWindowResolver 인터페이스"),
    ("EffectRegistry interface", "EffectRegistry 인터페이스"),
    ("IRandomSource GameRandomSource", "IRandomSource/GameRandomSource"),
    ("EngineTrace TraceEvent", "EngineTrace/TraceEvent"),
    ("ILogSink NullLogSink InMemoryLogSink", "ILogSink/NullLogSink/InMemoryLogSink"),
    ("phase2 result document", "Phase 2 결과 문서"),
    ("phase3 result document", "Phase 3 결과 문서"),
    ("phase4 result document", "Phase 4 결과 문서"),
    ("phase5 result document", "Phase 5 결과 문서"),
    ("final completion report", "최종 완료 보고서"),
    ("first player setup", "선후공 초기 세팅"),
    ("phase transition", "페이즈 전이"),
    ("memory pass", "메모리 패스"),
    ("state read write", "상태 읽기/쓰기"),
    ("hidden information view", "비공개 정보 시야"),
    ("zone owner", "존 소유자"),
    ("card instance binding", "카드 인스턴스 연결"),
    ("move event", "이동 이벤트"),
    ("source stack", "진화원 스택"),
    ("play card legal apply", "카드 플레이 합법성/적용"),
    ("digivolve legal apply", "디지볼브 합법성/적용"),
    ("option use", "옵션 사용"),
    ("trigger collection", "트리거 수집"),
    ("mandatory order", "강제 효과 순서"),
    ("optional choice", "선택 효과 선택"),
    ("attack declaration", "공격 선언"),
    ("block choice", "블록 선택"),
    ("battle DP deletion", "배틀 DP 비교/삭제"),
    ("security check", "시큐리티 체크"),
    ("keyword", "키워드"),
    ("trigger", "트리거"),
    ("modifier", "수치/비용 변경"),
    ("restriction", "제한"),
    ("replacement", "대체 효과"),
    ("continuous", "상시 효과"),
    ("factory", "팩토리"),
    ("coverage", "커버리지"),
    ("parity", "AS-IS 비교"),
    ("regression", "회귀 테스트"),
    ("benchmark", "성능 측정"),
    ("validation", "검증"),
    ("contract", "계약"),
    ("schema", "스키마"),
    ("mapping", "매핑"),
    ("adapter", "어댑터"),
    ("helper", "헬퍼"),
    ("helpers", "헬퍼"),
    ("event", "이벤트"),
    ("events", "이벤트"),
    ("model", "모델"),
    ("files", "파일"),
    ("flow", "흐름"),
    ("result", "결과"),
    ("results", "결과"),
    ("test", "테스트"),
    ("tests", "테스트"),
]

HEADERS = [
    "Goal ID",
    "단계",
    "영역",
    "목표",
    "작업 범위",
    "산출물",
    "단위테스트 범위",
    "상세 지시서",
    "결과 문서",
    "선행 Goal",
    "완료 기준",
    "우선순위",
]


def ko_text(value: str) -> str:
    if value is None:
        return ""
    result = str(value)
    for old, new in REPLACEMENTS:
        result = result.replace(old, new)
    return result


def safe_name(goal_id: str, goal: str) -> str:
    keep = []
    for ch in goal.lower():
        if ch.isalnum():
            keep.append(ch)
        elif ch in (" ", "-", "_"):
            keep.append("_")
    slug = "".join(keep).strip("_")
    while "__" in slug:
        slug = slug.replace("__", "_")
    if not slug:
        slug = "goal"
    return f"{goal_id}_{slug}.md"


def main() -> None:
    with SRC.open("r", encoding="utf-8-sig", newline="") as source:
        rows = list(csv.DictReader(source))

    output_rows = []
    for row in rows:
        output_rows.append({
            "Goal ID": row["goal_id"],
            "단계": PHASE_MAP.get(row["phase"], row["phase"]),
            "영역": AREA_MAP.get(row["area"], row["area"]),
            "목표": row["goal"],
            "작업 범위": ko_text(row["scope"]),
            "산출물": ko_text(row["deliverables"]),
            "단위테스트 범위": ko_text(row["unit_test_scope"]),
            "상세 지시서": f"docs/goal-specs/{safe_name(row['goal_id'], row['goal'])}",
            "결과 문서": row["result_document"],
            "선행 Goal": "없음" if row["blocked_until"] in ("", "None") else row["blocked_until"],
            "완료 기준": ko_text(row["completion_gate"]),
            "우선순위": PRIORITY_MAP.get(row["priority"], row["priority"]),
        })

    with DST.open("w", encoding="utf-8-sig", newline="") as target:
        writer = csv.DictWriter(target, fieldnames=HEADERS)
        writer.writeheader()
        writer.writerows(output_rows)

    print(f"wrote {DST} rows={len(output_rows)}")


if __name__ == "__main__":
    main()
