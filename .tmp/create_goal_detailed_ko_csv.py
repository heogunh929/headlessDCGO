import csv
from pathlib import Path

SOURCE = Path("docs/headless_complete_goal_breakdown_ko.csv")
TARGET = Path("docs/headless_complete_goal_breakdown_detailed_ko.csv")

AREA_DETAIL = {
    "설계": "설계 문서가 서로 모순되지 않는지 확인하고, 후속 구현자가 읽을 기준 문서를 확정한다.",
    "테스트": "테스트 정책, 테스트 매트릭스, 결과 문서 규칙을 구현 작업의 완료 기준으로 고정한다.",
    "단계 완료 게이트": "선행 Goal 결과 문서를 확인하고 다음 단계로 넘어갈 수 있는지 판정한다.",
    "런타임": "Headless 엔진의 외부 호출 계약을 고정한다. 매치 생성, 초기화, 스텝, 액션 적용, 결과 조회가 Unity 없이 설명되어야 한다.",
    "상태/존": "GameObject/Transform 대신 순수 데이터로 카드 위치, 카드 상태, 플레이어 상태, 존 순서를 표현한다.",
    "Unity 전역 접근 대체": "GManager.instance, ContinuousController.instance, GetComponent 계열 접근을 EngineContext와 명시적 서비스 접근으로 바꿀 기준을 만든다.",
    "코루틴 대체": "IEnumerator, StartCoroutine, WaitForSeconds, WaitWhile로 표현되던 게임 진행을 deterministic task/step/pending state로 대체한다.",
    "선택 처리 대체": "SelectCardEffect 계열 UI 선택을 ChoiceRequest/ChoiceResult로 표현하고 정책/스크립트가 선택을 반환할 수 있게 한다.",
    "효과 처리": "AutoProcessing, Effects, MultipleSkills가 붙을 effect queue, context, scheduler, timing contract를 만든다.",
    "세션/네트워크 대체": "Photon room/player/RPC/ownership 의미를 local player id, action queue, event stream으로 대체한다.",
    "데이터 로딩": "Resources, ScriptableObject, prefab 없이 카드 데이터, 덱, banlist를 파일/JSON/domain record로 로드한다.",
    "진단/결정성": "seed, random, trace, log, dependency scan을 통해 같은 입력이 같은 결과를 내도록 검증한다.",
    "턴/페이즈 흐름": "TurnStateMachine의 setup, draw, unsuspend, breeding, main, end, memory pass 흐름을 Headless state와 연결한다.",
    "게임 컨텍스트": "AS-IS GameContext 접근을 MatchState/PlayerState 기반 read/write view로 연결한다.",
    "플레이어 상태": "Player가 들고 있던 zone ownership, memory, security, deck loss 관련 판단을 state service 기준으로 옮긴다.",
    "카드 컨트롤러": "CardController/CardObjectController의 카드 id, 이동, 공개, suspend, source 조작을 IZoneMover와 CardInstanceState로 옮긴다.",
    "메인 페이즈 액션": "MainPhaseAction 하위 play, digivolve, option, attack, pass 흐름을 LegalAction과 ActionProcessor로 연결한다.",
    "자동 효과 처리": "AutoProcessing의 트리거 수집, 강제/선택 효과 순서, delayed/security hook을 EffectScheduler와 연결한다.",
    "공격/배틀 처리": "AttackProcess의 공격 선언, 타겟, 블록, DP 비교, 삭제, 시큐리티 체크, end-attack 트리거를 데이터 상태로 처리한다.",
    "효과 계약": "ICardEffect, SkillInfo, effect metadata가 Unity component 없이 실행될 typed contract를 만든다.",
    "효과 컨텍스트": "Hashtable 기반 effect payload를 source, owner, trigger, targets, flags가 명시된 typed EffectContext로 옮긴다.",
    "조건 처리": "CanUseEffects와 trigger condition helper를 현재 MatchState와 EffectContext 기준으로 평가하게 한다.",
    "요구사항 처리": "레벨, DP, 비용, 이름, 색, 특성 등 요구사항 판단을 재사용 가능한 helper로 정리한다.",
    "비용 처리": "play cost, digivolution cost, cost reduction, memory movement의 계산 기준을 분리한다.",
    "대상/존 조회": "대상 후보, 존 검색, 공개/비공개 정보 접근 기준을 서비스로 정리한다.",
    "키워드 효과": "키워드별 공통 동작을 카드별 구현 전에 재사용 가능한 base effect로 정리한다.",
    "수치/비용 변경": "DP, cost, security attack 등 modifier 효과를 continuous/recalculation 기준으로 처리한다.",
    "제한/불가 효과": "cannot attack/block/delete/return/suspend 계열 restriction을 RuleQuery/EffectQuery로 노출한다.",
    "대체 효과": "prevent, redirect, immune 같은 replacement 판단을 실제 mutation 전에 적용할 수 있게 한다.",
    "상시 효과": "현재 상태에 따라 계속 재평가되는 continuous effect evaluator를 만든다.",
    "효과 팩토리": "card id, trigger, keyword, permanent effect를 effect resolver와 연결하는 registry/factory를 만든다.",
    "효과 선택": "효과 해석 중 필요한 선택을 ChoiceRequest로 생성하고 결과를 EffectContext에 반영한다.",
    "타이밍/우선순위": "turn player order, mandatory/optional order, trigger timing window를 명시적으로 정렬한다.",
    "턴 제한 플래그": "once per turn, once per timing 같은 flag를 turn cleanup과 연결한다.",
    "진화원/부여/시큐리티 효과": "inherited, granted, security effect의 source와 활성 조건을 분리한다.",
    "카드풀": "대상 카드 데이터가 로드되고 effect binding coverage와 missing behavior report가 생성되게 한다.",
    "트리거 효과": "on play, on digivolve, when attacking, deletion, security 등 trigger별 카드 효과를 연결한다.",
    "디지볼브 관련": "digivolution requirement, cost, source 조작, 특수 진화 조건을 룰/효과 서비스로 옮긴다.",
    "진화원 조작": "source add, trash, reveal, attach/detach를 ZoneState와 CardInstanceState 기준으로 처리한다.",
    "카드별 효과": "세트/디렉터리 단위로 card-specific effect를 구현하고 대표 테스트를 남긴다.",
    "효과 순서": "여러 효과가 동시에 발생할 때 강제/선택/턴플레이어 순서가 고정되는지 검증한다.",
    "데이터-동작 연결": "card text/data와 구현 behavior의 연결 누락을 audit하고 보고한다.",
    "커버리지": "카드풀 구현률, 효과 binding률, 제외 사유가 수치로 확인되게 한다.",
    "관측값": "AI/RL이 볼 수 있는 state를 hidden information 규칙에 맞춰 observation으로 변환한다.",
    "액션 인코딩": "LegalAction을 stable action id와 action mask로 변환하고 역변환한다.",
    "보상": "terminal result와 optional shaping config를 reward로 변환한다.",
    "RL 환경": "reset/step API가 authoritative match를 감싸고 observation/reward/terminal을 반환한다.",
    "정책 실행": "scripted/self-play policy가 legal action과 choice를 사용해 episode를 실행한다.",
    "배치 실행": "여러 episode와 parallel simulation이 seed별 deterministic 결과를 만드는지 확인한다.",
    "학습 데이터셋": "transition, observation, action, reward, metadata를 JSONL 등 학습용 포맷으로 export한다.",
    "시나리오": "테스트 시나리오를 데이터로 정의하고 runner가 같은 흐름을 재현하게 한다.",
    "리플레이": "action/choice/seed/trace를 저장하고 재실행해 같은 fingerprint가 나오는지 검증한다.",
    "AS-IS 비교": "Unity AS-IS curated scenario와 Headless 결과를 비교하는 harness와 결과 문서를 만든다.",
    "회귀 테스트": "setup, phase, memory, movement, combat, security, effect 범주가 regression suite에 포함되게 한다.",
    "성능 측정": "학습용 batch 실행 규모에서 episode throughput과 병목을 문서화한다.",
}

PHASE_GUARD = {
    "Phase 0": "설계와 문서 검증만 수행한다. C# 구현과 원본 소스 수정은 하지 않는다.",
    "Phase 1": "Unity 대체 기반만 구현한다. Phase 1 완료 전에는 Assets 룰/카드 효과 포팅을 하지 않는다.",
    "Phase 2": "Phase 1에서 확정된 API를 사용해 AS-IS 핵심 흐름만 포팅한다. 카드별 효과 구현으로 확장하지 않는다.",
    "Phase 3": "공통 룰/효과 인프라만 포팅한다. 개별 카드 효과 배치 작업은 Phase 4로 남긴다.",
    "Phase 4": "카드 효과와 카드풀 구현만 다룬다. Phase 1~3 기반 API를 재설계하지 않는다.",
    "Phase 5": "완성된 Headless 실행기를 AI/RL에 연결한다. 게임 룰을 새로 구현하지 않는다.",
    "Phase 6": "검증, parity, regression, benchmark를 수행한다. 새 기능 구현은 실패 원인 수정 범위로 제한한다.",
}

HEADERS = [
    "Goal ID",
    "단계",
    "영역",
    "목표",
    "상세 목표 설명",
    "해야 할 작업",
    "하지 말아야 할 작업",
    "참조/확인 대상",
    "산출물",
    "단위테스트 상세",
    "결과 문서",
    "결과 문서 필수 내용",
    "선행 Goal",
    "완료 기준",
    "완료 체크리스트",
    "우선순위",
]


def phase_key(phase: str) -> str:
    for key in PHASE_GUARD:
        if phase.startswith(key):
            return key
    return ""


def make_reference(row: dict) -> str:
    refs = [
        "docs/headless_complete_goal_breakdown_ko.csv",
        "docs/headless_goal_spec_index.csv",
        row["상세 지시서"],
        "docs/headless_goal_execution_prompt.md",
    ]
    if row["단계"].startswith("Phase 1"):
        refs.append("src/HeadlessDCGO.Engine/Headless")
    if row["단계"].startswith(("Phase 2", "Phase 3", "Phase 4")):
        refs.append("DCGO/Assets 원본 파일은 읽기 전용 참조")
    if row["결과 문서"]:
        refs.append(row["결과 문서"])
    return " | ".join(refs)


def make_detail(row: dict) -> str:
    area_detail = AREA_DETAIL.get(row["영역"], "이 Goal의 목표와 작업 범위에 적힌 내용을 기준으로 산출물을 완성한다.")
    return (
        f"{row['목표']} Goal은 '{row['작업 범위']}'를 완성하기 위한 작업이다. "
        f"{area_detail} "
        f"산출물은 '{row['산출물']}'이며, 완료 기준은 '{row['완료 기준']}'이다."
    )


def make_do(row: dict) -> str:
    return (
        f"1. 선행 Goal '{row['선행 Goal']}' 완료 여부를 확인한다. "
        f"2. 상세 지시서 '{row['상세 지시서']}'를 읽는다. "
        f"3. 작업 범위 '{row['작업 범위']}'에 해당하는 파일과 API만 확인한다. "
        f"4. 필요한 AS-IS 원본은 읽기 전용으로 분석한다. "
        f"5. 산출물 '{row['산출물']}'을 작성하거나 갱신한다. "
        f"6. 단위테스트 '{row['단위테스트 범위']}'를 작성한다. "
        f"7. 테스트를 실행하고 실패가 있으면 같은 Goal 범위 안에서 수정한다. "
        f"8. 결과 문서 '{row['결과 문서']}'를 작성한다."
    )


def make_dont(row: dict) -> str:
    guard = PHASE_GUARD.get(phase_key(row["단계"]), "Goal 범위 밖 작업을 하지 않는다.")
    return (
        f"{guard} "
        "원본 DCGO/Assets 파일은 수정하지 않는다. "
        "다른 Goal을 함께 처리하지 않는다. "
        "단위테스트 없이 완료라고 말하지 않는다. "
        "결과 문서 없이 완료라고 말하지 않는다. "
        "완성 기준을 충족하지 않는 자리표시 구현을 완료로 간주하지 않는다."
    )


def make_test_detail(row: dict) -> str:
    return (
        f"테스트는 '{row['단위테스트 범위']}'를 직접 검증해야 한다. "
        "정상 케이스, 실패/예외 케이스, 결정성이 필요한 경우 동일 입력 반복 케이스를 포함한다. "
        "테스트 파일명과 테스트 명령을 결과 문서에 기록한다. "
        "Goal 범위 밖 동작을 검증하기 위해 새 구현을 끌어오지 않는다."
    )


def make_result_requirements() -> str:
    return (
        "실행 일시, 수정/생성 파일, 참조한 원본 파일, 테스트 명령, 전체/통과/실패/스킵 수, "
        "실패 상세, 테스트하지 못한 항목, 미해결 리스크, 완료 판정을 포함한다."
    )


def make_checklist(row: dict) -> str:
    return (
        f"선행 Goal 확인; 상세 지시서 확인; 작업 범위 준수; 원본 소스 미수정; "
        f"산출물 작성; 단위테스트 작성; 단위테스트 실행; 실패 0개; "
        f"결과 문서 작성; 완료 기준 '{row['완료 기준']}' 충족"
    )


def main() -> None:
    with SOURCE.open("r", encoding="utf-8-sig", newline="") as source:
        rows = list(csv.DictReader(source))

    output = []
    for row in rows:
        output.append({
            "Goal ID": row["Goal ID"],
            "단계": row["단계"],
            "영역": row["영역"],
            "목표": row["목표"],
            "상세 목표 설명": make_detail(row),
            "해야 할 작업": make_do(row),
            "하지 말아야 할 작업": make_dont(row),
            "참조/확인 대상": make_reference(row),
            "산출물": row["산출물"],
            "단위테스트 상세": make_test_detail(row),
            "결과 문서": row["결과 문서"],
            "결과 문서 필수 내용": make_result_requirements(),
            "선행 Goal": row["선행 Goal"],
            "완료 기준": row["완료 기준"],
            "완료 체크리스트": make_checklist(row),
            "우선순위": row["우선순위"],
        })

    with TARGET.open("w", encoding="utf-8-sig", newline="") as target:
        writer = csv.DictWriter(target, fieldnames=HEADERS)
        writer.writeheader()
        writer.writerows(output)

    print(f"wrote {TARGET} rows={len(output)}")


if __name__ == "__main__":
    main()
