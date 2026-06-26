#!/usr/bin/env python3
"""Generate AS-IS (DCGO) ↔ TO-BE (Headless) function-level mapping CSV."""

from __future__ import annotations

import csv
import re
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
# Physical AS-IS: repo-root DCGO/ (original Unity project; gitignored in workspace tools)
ASIS_ROOT = ROOT / "DCGO" / "Assets" / "Scripts" / "Script"
# Physical TO-BE: headless engine
TOBE_ROOT = ROOT / "src" / "HeadlessDCGO.Engine" / "Headless"
EXISTING_CSV = ROOT / "docs" / "headless_dependency_function_port_mapping.csv"
OUTPUT_CSV = ROOT / "docs" / "asis_tobe_function_mapping.csv"
OUTPUT_KO_CSV = ROOT / "docs" / "asis_tobe_function_mapping_ko.csv"

ASIS_PREFIX = "DCGO/Assets/Scripts/Script"
TOBE_PREFIX = "src/HeadlessDCGO.Engine/Headless"


def _m(cls, func, rel, cat, phase, status, notes):
    return {
        "tobe_class": cls,
        "tobe_function": func,
        "tobe_location": f"{TOBE_PREFIX}/{rel}",
        "category": cat,
        "phase": phase,
        "porting_method": "직접포팅" if status == "PORTED" else "계약고정_후속구현",
        "status": status,
        "notes": notes,
    }


# Match public/protected/internal methods (incl. IEnumerator, async, override)
METHOD_LINE = re.compile(
    r"^\s*(?:\[.*?\]\s*)*(?:public|protected|internal)\s+"
    r"(?:static\s+)?(?:async\s+)?(?:override\s+|virtual\s+|new\s+)?"
    r"(?:[\w<>,\[\]\.?]+\s+)+(?P<name>\w+)\s*\("
)
CLASS_LINE = re.compile(r"^\s*(?:public\s+)?(?:sealed\s+)?(?:partial\s+)?class\s+(\w+)")
SKIP_NAMES = frozenset({"if", "for", "while", "switch", "catch", "using"})

UI_SUFFIXES = ("Panel", "Button", "Object", "Tab", "Frame", "Editor", "Detail", "Notification")
UI_FILES = {
    "SelectCardPanel.cs", "DeckListPanel.cs", "DeckInfoPanel.cs", "CheckCardPanel.cs",
    "ShowPhaseObject.cs", "ShowPhaseNotificationObject.cs", "NextPhaseButton.cs",
    "DetailCard_DeckEditor.cs", "EditDeck.cs", "CreateNewDeckButton.cs",
    "CardDistributionTab.cs", "CardPrefab_CreateDeck.cs", "PermanentDetail.cs",
    "HandCard.cs", "FieldPermanentCard.cs", "BGMObject.cs", "BendText.cs",
    "DeckBuildingRule.cs", "FilterCardList.cs",
}

# (asis_file, asis_method) -> mapping fields
MANUAL: dict[tuple[str, str], dict] = {
    ("GManager.cs", "Init"): _m("DcgoMatch", "InitializeAsync", "Runtime/DcgoMatch.cs", "런타임/생명주기", "Phase 1", "PORTED", "매치 초기화"),
    ("TurnStateMachine.cs", "Init"): _m("InMemoryHeadlessTurnController", "Initialize", "Runtime/InMemoryHeadlessTurnController.cs", "런타임/턴", "Phase 2", "CONTRACT", "턴 순서 초기화"),
    ("TurnStateMachine.cs", "GameStateMachine"): _m("DcgoMatch", "StepAsync", "Runtime/DcgoMatch.cs", "런타임/생명주기", "Phase 1", "PORTED", "1-step 매치 진행"),
    ("TurnStateMachine.cs", "QueueMainPhaseAction"): _m("HeadlessActionQueue", "Enqueue", "Runtime/HeadlessActionQueue.cs", "액션/턴", "Phase 1", "PORTED", "legal action 큐잉"),
    ("TurnStateMachine.cs", "SetPlayCard"): _m("PlayCardAction", "Process", "Runtime/PlayCardAction.cs", "액션/플레이", "Phase 2", "PORTED", "카드 플레이 액션"),
    ("TurnStateMachine.cs", "SetAttackingPermaent"): _m("AttackPermanentAction", "Process", "Runtime/AttackPermanentAction.cs", "전투/공격", "Phase 2", "PORTED", "공격 선언"),
    ("TurnStateMachine.cs", "Surrender"): _m("HeadlessActionFactory", "SetTerminalResult", "Runtime/HeadlessActionFactory.cs", "런타임/승패", "Phase 2", "CONTRACT", "항복 terminal metadata"),
    ("TurnStateMachine.cs", "SetMainPhase"): _m("InMemoryHeadlessTurnController", "SetPhase", "Runtime/InMemoryHeadlessTurnController.cs", "런타임/턴", "Phase 2", "CONTRACT", "메인 페이즈 전환"),
    ("AutoProcessing.cs", "AutoProcessCheck"): _m("GameFlowProcessor", "AutoProcessAsync", "Runtime/GameFlowProcessor.cs", "효과/자동처리", "Phase 3.5", "PORTED", "G3.5-006 이벤트→트리거→스케줄러"),
    ("AutoProcessing.cs", "RuleProcess"): _m("GameFlowProcessor", "RuleProcess", "Runtime/GameFlowProcessor.cs", "효과/지속", "Phase 3.5", "CONTRACT", "DP cleanup placeholder"),
    ("AutoProcessing.cs", "TriggeredSkillProcess"): _m("AutoProcessingTriggerCollector", "CollectAndEnqueue", "Effects/AutoProcessingTriggerCollector.cs", "효과/트리거", "Phase 3.5", "PORTED", "게임 이벤트→트리거 수집"),
    ("AutoProcessing.cs", "ActivateEffectProcess"): _m("EffectScheduler", "ResolveAllAsync", "Effects/EffectScheduler.cs", "효과/스케줄러", "Phase 3.5", "PORTED", "G3.5-001 resolver 연결"),
    ("AttackProcess.cs", "ProcessNextState"): _m("AttackPipeline", "AdvanceAsync", "Runtime/AttackPipeline.cs", "전투/공격", "Phase 3.5", "PORTED", "G3.5-005 공격 파이프라인"),
    ("AttackProcess.cs", "BlockTiming"): _m("BlockTiming", "RequestBlockChoice", "Runtime/BlockTiming.cs", "전투/블록", "Phase 2", "PORTED", "블록 choice 생성"),
    ("AttackProcess.cs", "BattleProcess"): _m("BattleResolver", "ResolveAsync", "Runtime/BattleResolver.cs", "전투/DP", "Phase 2", "PORTED", "DP 전투 해결"),
    ("AttackProcess.cs", "SecurityCheck"): _m("SecurityResolver", "ResolveAsync", "Runtime/SecurityResolver.cs", "전투/시큐리티", "Phase 2", "PORTED", "시큐리티 체크"),
    ("Player.cs", "MaxMemoryCost"): _m("PlayerRuleAdapter", "MaxMemoryCost", "State/PlayerRuleAdapter.cs", "플레이어/메모리", "Phase 2", "PORTED", "메모리 비용 상한"),
    ("Player.cs", "ExpectedMemory"): _m("PlayerRuleAdapter", "ExpectedMemory", "State/PlayerRuleAdapter.cs", "플레이어/메모리", "Phase 2", "PORTED", "메모리 예상값"),
    ("Player.cs", "CanAddSecurity"): _m("PlayerRuleAdapter", "CanAddSecurity", "State/PlayerRuleAdapter.cs", "플레이어/시큐리티", "Phase 2", "PORTED", "시큐리티 추가 가능"),
    ("Player.cs", "CanReduceSecurity"): _m("PlayerRuleAdapter", "CanReduceSecurity", "State/PlayerRuleAdapter.cs", "플레이어/시큐리티", "Phase 2", "PORTED", "시큐리티 감소 가능"),
    ("Player.cs", "Draw"): _m("InMemoryZoneMover", "DrawAsync", "Services/InMemoryZoneMover.cs", "존/드로우", "Phase 2", "PORTED", "덱 드로우"),
    ("Player.cs", "IsLose"): _m("IHeadlessPlayerStatusController", "MarkLose/IsLose", "Runtime/IHeadlessPlayerStatusController.cs", "플레이어/승패", "Phase 3.5", "PORTED", "G3.5-008 lose 플래그"),
    ("ContinuousController.cs", "Recalculation"): _m("ContinuousEffectEvaluator", "Evaluate", "Effects/ContinuousEffectEvaluator.cs", "효과/지속", "Phase 3", "PORTED", "지속효과 재평가"),
    ("CardEffectFactory.cs", "Lookup"): _m("CardEffectFactoryBindingRegistry", "Lookup", "Effects/CardEffectFactoryBindingRegistry.cs", "효과/팩토리", "Phase 3.5", "PORTED", "G3.5-003 player/context"),
    ("SelectCardEffect.cs", "SetUp"): _m("ChoiceRequest", "constructor", "Choices/ChoiceRequest.cs", "선택/Choice", "Phase 1", "PORTED", "선택 요청 모델"),
    ("SelectCardEffect.cs", "Activate"): _m("IChoiceProvider", "ChooseAsync", "Choices/IChoiceProvider.cs", "선택/Choice", "Phase 1", "PORTED", "UI→deterministic choice"),
    ("GameRandom.cs", "Shuffle"): _m("GameRandomSource", "Shuffle", "Services/GameRandomSource.cs", "랜덤", "Phase 1", "PORTED", "시드 기반 셔플"),
    ("GameRandom.cs", "IsSucceedProbability"): _m("GameRandomSource", "NextDouble", "Services/GameRandomSource.cs", "랜덤", "Phase 1", "PORTED", "확률 판정"),
    ("CardObjectController.cs", "MovePermanent"): _m("InMemoryZoneMover", "MoveAsync", "Services/InMemoryZoneMover.cs", "존/이동", "Phase 2", "PORTED", "존 간 카드 이동"),
    ("AutoProcessing.cs", "EndTurnCheck"): _m("GameFlowProcessor", "EndTurnCheck", "Runtime/GameFlowProcessor.cs", "런타임/승패", "Phase 3.5", "PORTED", "G3.5-008 PlayerRuleAdapter terminal"),
    ("AutoProcessing.cs", "PutStackedSkill"): _m("EffectResolutionQueue", "Enqueue", "Effects/EffectResolutionQueue.cs", "효과/스케줄러", "Phase 3", "PORTED", "스킬 큐 적재"),
    ("AutoProcessing.cs", "EndTurnProcess"): _m("HeadlessEndTurnCleanupFlow", "ApplyAsync", "Runtime/HeadlessEndTurnCleanupFlow.cs", "런타임/턴", "Phase 2", "PORTED", "턴 종료 정리"),
    ("AttackProcess.cs", "EndAttack"): _m("AttackPipeline", "AdvanceEndAttack", "Runtime/AttackPipeline.cs", "전투/공격", "Phase 3.5", "PORTED", "EndAttackTriggerHook 수집"),
    ("TurnStateMachine.cs", "ProcessNextState"): _m("GameFlowProcessor", "RunToStableAsync", "Runtime/GameFlowProcessor.cs", "런타임/공통루프", "Phase 3.5", "PORTED", "G3.5-004 공통 처리 루프"),
    ("CardObjectController.cs", "RemoveFromAllArea"): _m("ZoneState", "Remove", "State/ZoneState.cs", "존/이동", "Phase 2", "PORTED", "존에서 카드 제거"),
}


@dataclass
class MethodInfo:
    class_name: str
    method_name: str
    line: int
    signature: str


def extract_methods(file_path: Path) -> list[MethodInfo]:
    content = file_path.read_text(encoding="utf-8", errors="replace")
    class_name = file_path.stem
    for match in CLASS_LINE.finditer(content):
        class_name = match.group(1)
        break

    methods: list[MethodInfo] = []
    for i, line in enumerate(content.splitlines(), start=1):
        match = METHOD_LINE.match(line)
        if not match:
            continue
        name = match.group("name")
        if name in SKIP_NAMES:
            continue
        methods.append(MethodInfo(class_name, name, i, line.strip()))
    return methods


def is_ui_file(name: str) -> bool:
    if name in UI_FILES:
        return True
    return any(name.endswith(s + ".cs") for s in UI_SUFFIXES)


def infer_category(file_name: str) -> str:
    rules = [
        ("TurnStateMachine", "런타임/턴"), ("AttackProcess", "전투/공격"),
        ("AutoProcessing", "효과/자동처리"), ("Player", "플레이어"),
        ("ContinuousController", "효과/지속"), ("CardEffectFactory", "효과/팩토리"),
        ("CardController", "카드/효과"), ("CardObjectController", "카드/존"),
        ("GManager", "런타임/생명주기"), ("GameContext", "상태/컨텍스트"),
        ("GameRandom", "랜덤"), ("Select", "선택/Choice"), ("Deck", "덱/데이터"),
        ("Permanent", "카드/상태"), ("SkillInfo", "효과/스킬"),
    ]
    for key, cat in rules:
        if key in file_name:
            return cat
    if is_ui_file(file_name):
        return "UI/클라이언트"
    return "기타"


def load_existing() -> tuple[dict[tuple[str, str], dict], dict[str, dict]]:
    by_file_func: dict[tuple[str, str], dict] = {}
    by_func: dict[str, dict] = {}
    if not EXISTING_CSV.exists():
        return by_file_func, by_func
    with EXISTING_CSV.open(encoding="utf-8-sig", newline="") as f:
        for row in csv.DictReader(f):
            raw_func = row.get("existing_dependency_function_name", "").strip()
            func = raw_func.split("(")[0].strip()
            if "." in func:
                func = func.split(".")[-1]
            loc = row.get("existing_location", "")
            file_hint = ""
            for part in loc.replace('"', "").split(","):
                part = part.strip()
                if part.endswith(".cs"):
                    file_hint = Path(part).name
                    break
                if ".cs" in part:
                    file_hint = part.split("/")[-1]
                    break
            entry = {
                "tobe_function": row.get("current_porting_function_name", ""),
                "tobe_location": row.get("current_location", ""),
                "status": row.get("status", "UNMAPPED"),
                "notes": row.get("notes", ""),
                "category": row.get("category", "기타"),
            }
            if file_hint:
                by_file_func[(file_hint, func)] = entry
            if func and func not in by_func:
                by_func[func] = entry
    return by_file_func, by_func


def resolve_mapping(file_name: str, method_name: str, existing: tuple[dict, dict]) -> dict:
    by_file_func, by_func = existing
    key = (file_name, method_name)
    if key in MANUAL:
        return MANUAL[key]
    if key in by_file_func:
        return _from_existing(by_file_func[key])
    # Generic dependency mappings (e.g. StartCoroutine, WaitForSeconds) by method name
    if method_name in by_func:
        ex = by_func[method_name]
        if ex["status"] in ("PORTED", "CONTRACT", "BRIDGE", "EXCLUDED"):
            return _from_existing(ex)
    if is_ui_file(file_name):
        return {
            "tobe_class": "", "tobe_function": "", "tobe_location": "",
            "category": "UI/클라이언트", "phase": "N/A", "porting_method": "제외",
            "status": "EXCLUDED", "notes": "UI/클라이언트 전용; Headless 제외",
        }
    return {
        "tobe_class": "", "tobe_function": "", "tobe_location": "",
        "category": infer_category(file_name), "phase": "Phase 4+",
        "porting_method": "미정", "status": "UNMAPPED",
        "notes": "함수 단위 TO-BE 매핑 미정",
    }


def _from_existing(ex: dict) -> dict:
    tobe_func = ex["tobe_function"]
    return {
        "tobe_class": tobe_func.split(".")[0] if "." in tobe_func else tobe_func.split("(")[0],
        "tobe_function": tobe_func,
        "tobe_location": ex["tobe_location"],
        "category": ex.get("category", "기타"),
        "phase": "Phase 1",
        "porting_method": "역할기반_재설계" if ex["status"] == "PORTED" else ex["status"],
        "status": ex["status"],
        "notes": ex["notes"],
    }


def main() -> None:
    if not ASIS_ROOT.exists():
        raise SystemExit(f"AS-IS root not found: {ASIS_ROOT}")

    existing = load_existing()
    rows: list[dict] = []
    seq = 0

    for file_path in sorted(ASIS_ROOT.glob("*.cs")):
        file_name = file_path.name
        asis_loc = f"{ASIS_PREFIX}/{file_name}"
        for method in extract_methods(file_path):
            seq += 1
            m = resolve_mapping(file_name, method.method_name, existing)
            rows.append({
                "mapping_id": f"AFM-{seq:05d}",
                "phase": m["phase"],
                "category": m["category"],
                "asis_class": method.class_name,
                "asis_function": method.method_name,
                "asis_signature": method.signature,
                "asis_file": file_name,
                "asis_location": asis_loc,
                "asis_line": method.line,
                "tobe_class": m["tobe_class"],
                "tobe_function": m["tobe_function"],
                "tobe_location": m["tobe_location"],
                "porting_method": m["porting_method"],
                "status": m["status"],
                "notes": m["notes"],
            })

    # TO-BE-only integration entries (no single AS-IS function)
    tobe_only = [
        ("GameFlowProcessor", "RunToStableAsync", "TurnStateMachine.ProcessNextState 공통루프", "Phase 3.5", "PORTED", "G3.5-004 공통 루프 골격"),
        ("GameFlowProcessor", "EndTurnCheck", "AutoProcessing player.IsLose 판정", "Phase 3.5", "PORTED", "G3.5-008 terminal outcome"),
        ("TerminalEvaluator", "Evaluate", "AutoProcessing lose flag consolidation", "Phase 3.5", "PORTED", "PlayerRuleAdapter bridge"),
        ("ContinuousRestrictionGate", "EvaluateAttack", "ContinuousController restriction→legal action", "Phase 3.5", "PORTED", "G3.5-007"),
        ("MatchStateMutationSink", "ApplyMutations", "CardController effect mutations", "Phase 3.5", "PORTED", "G3.5-002"),
        ("ITerminalOutcomeSink", "SetTerminalOutcome", "TurnStateMachine.EndGame winner", "Phase 3.5", "PORTED", "G3.5-008 MatchResult"),
        ("HeadlessRlEnvironment", "StepAsync", "GManager game loop for RL", "Phase 5", "PORTED", "RL 환경 API"),
    ]
    for cls, func, asis_equiv, phase, status, notes in tobe_only:
        seq += 1
        rows.append({
            "mapping_id": f"AFM-{seq:05d}",
            "phase": phase,
            "category": "TO-BE 전용/통합",
            "asis_class": "(분산/없음)",
            "asis_function": asis_equiv,
            "asis_signature": "",
            "asis_file": "",
            "asis_location": "",
            "asis_line": "",
            "tobe_class": cls,
            "tobe_function": func,
            "tobe_location": f"{TOBE_PREFIX}/...",
            "porting_method": "신규_통합",
            "status": status,
            "notes": notes,
        })

    fields_en = [
        "mapping_id", "phase", "category",
        "asis_class", "asis_function", "asis_signature",
        "asis_file", "asis_location", "asis_line",
        "tobe_class", "tobe_function", "tobe_location",
        "porting_method", "status", "notes",
    ]
    fields_ko = [
        "mapping_id", "phase", "category",
        "asis_class", "asis_function", "asis_signature",
        "asis_file", "asis_location", "asis_line",
        "tobe_class", "tobe_function", "tobe_location",
        "porting_method", "status", "notes",
    ]
    ko_header = {
        "mapping_id": "mapping_id",
        "phase": "phase",
        "category": "category",
        "asis_class": "asis_class",
        "asis_function": "asis_function",
        "asis_signature": "asis_signature",
        "asis_file": "asis_file",
        "asis_location": "asis_location",
        "asis_line": "asis_line",
        "tobe_class": "tobe_class",
        "tobe_function": "tobe_function",
        "tobe_location": "tobe_location",
        "porting_method": "porting_method",
        "status": "status",
        "notes": "notes",
    }
    ko_header = {
        "mapping_id": "매핑ID",
        "phase": "단계",
        "category": "분류",
        "asis_class": "ASIS_클래스",
        "asis_function": "ASIS_함수",
        "asis_signature": "ASIS_시그니처",
        "asis_file": "ASIS_파일",
        "asis_location": "ASIS_위치",
        "asis_line": "ASIS_행",
        "tobe_class": "TOBE_클래스",
        "tobe_function": "TOBE_함수",
        "tobe_location": "TOBE_위치",
        "porting_method": "포팅_방식",
        "status": "상태",
        "notes": "비고",
    }

    for path, fields, header in (
        (OUTPUT_CSV, fields_en, {f: f for f in fields_en}),
        (OUTPUT_KO_CSV, fields_ko, ko_header),
    ):
        with path.open("w", encoding="utf-8-sig", newline="") as f:
            w = csv.DictWriter(f, fieldnames=fields)
            w.writerow(header)
            for row in rows:
                w.writerow({k: row.get(k, "") for k in fields})

    counts: dict[str, int] = {}
    for r in rows:
        counts[r["status"]] = counts.get(r["status"], 0) + 1
    print(f"Wrote {len(rows)} rows")
    print(f"  EN: {OUTPUT_CSV}")
    print(f"  KO: {OUTPUT_KO_CSV}")
    print("Status:", counts)


if __name__ == "__main__":
    main()
