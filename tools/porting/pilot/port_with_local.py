"""카드 포팅 로컬 하네스 — Gemma/Qwen 역할 분리 + 컴파일 게이트.

기존 port_with_sonnet.py의 구조를 유지하되 Sonnet 호출부를 로컬 OpenAI-compatible 모델 호출로 교체한 버전.

전제:
  pip install openai

환경변수 예시:
  export LOCAL_LLM_BASE_URL=http://127.0.0.1:11434/v1
  export LOCAL_LLM_API_KEY=ollama
  export PLANNER_MODEL=gemma4:31b
  export CODER_MODEL=qwen3-coder-next:latest
  export REVIEWER_MODEL=gemma4:31b

사용:
  python tools/porting/pilot/port_with_local.py --tier exact --set BT1 --n 10 --out ../runs/local-pilot
  python tools/porting/pilot/port_with_local.py --tier family --set BT1 --n 10 --retries 4 --out ../runs/local-pilot
  python tools/porting/pilot/port_with_local.py --all-pending --set BT1 --retries 4 --keep --skip-cold
"""

from __future__ import annotations

import argparse
import json
import re
import sqlite3
import subprocess
import sys
from pathlib import Path
from typing import Callable

REPO = Path(__file__).resolve().parents[3]
DB = REPO / "docs" / "porting" / "card_ir.sqlite"
sys.path.insert(0, str(REPO / "tools" / "porting"))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from model_router import LocalModelRouter  # noqa: E402
from porting_task import build_task  # noqa: E402  DB 스키마 공유
from validate_port import validate as _validate_symbols, load_allowlist as _load_allowlist  # noqa: E402

try:
    _ALLOWLIST = _load_allowlist()
except Exception:  # noqa: BLE001  allowlist 없으면 검사 skip(빌드 게이트가 여전히 잡음)
    _ALLOWLIST = {}

try:
    _ACTION_MAP = json.loads((Path(__file__).resolve().parent / "action_map.json").read_text(encoding="utf-8"))
except Exception:  # noqa: BLE001
    _ACTION_MAP = {}


def action_map_surface(action_tags_json: str | None) -> str:
    """(action_tag→factory) 이 카드의 action_tags에 해당하는 정규 팩토리만 프롬프트에 주입.
    카드-단위 레퍼런스(60% 짝없음) 대신 액션-단위 매핑(83% 공유)으로 접지 — 카드가 달라도 동작은 이걸로 매핑."""
    if not _ACTION_MAP or not action_tags_json:
        return ""
    try:
        tags = json.loads(action_tags_json)
    except Exception:  # noqa: BLE001
        return ""
    rel = [(t, _ACTION_MAP[t]) for t in tags if t in _ACTION_MAP]
    if not rel:
        return ""
    lines = ["## This card's actions -> canonical factories (use the factory below; effects differ but the action maps here)"]
    for tag, e in rel:
        if e.get("factory"):
            lines.append(f"- [{tag}] {e['factory']}({e.get('sig', '')})  {e.get('note', '')}")
            if e.get("also"):
                lines.append(f"    variants: {', '.join(e['also'])}")
        else:
            lines.append(f"- [{tag}] ({e.get('kind')}) {e.get('note', '')}")
    return "\n".join(lines)


def _validate_symbols_text(cs: str) -> str:
    """(#3 pre-build validator) 생성 .cs가 존재하지 않는 헤드리스 심볼을 참조하면 §9 힌트 포함 지시문을 반환.
    무효 심볼 없으면 빈 문자열."""
    if not _ALLOWLIST:
        return ""
    findings = _validate_symbols(cs, _ALLOWLIST)
    if not findings:
        return ""
    lines = ["Static check (pre-build): the symbols below do NOT exist in the headless engine — you MUST fix them using the alternatives shown."]
    for f in findings:
        lines.append(f"  - {f['symbol']}: {f['suggestion']}")
    return "\n".join(lines)


def _diagnose_compile_error(router: LocalModelRouter, detail: str) -> str:
    """(Step 2 gemma 진단) validator가 못 잡는 실컴파일 오류(시그니처/타입/오버로드)를 gemma가 진단.
    오류에 등장한 팩토리의 '진짜 시그니처'와 §9 대안만 근거로 제공 → gemma가 정밀 수정 지시를 생성.
    인용할 지식이 없으면(순수 문법오류 등) 빈 문자열 → raw 오류가 그대로 coder에 전달된다."""
    if not _ALLOWLIST:
        return ""
    sigs = {**_ALLOWLIST.get("factory_signatures", {}), **_ALLOWLIST.get("commons_signatures", {})}
    known = _ALLOWLIST.get("known_hallucinations", {})
    ctx = [f"- {name}({sig})  ← 진짜 시그니처" for name, sig in sigs.items() if name in detail]
    ctx += [f"- {bad} 는 없음 → {fix}" for bad, fix in known.items() if bad in detail]
    if not ctx:
        return ""
    prompt = (
        "Identify the cause of the C# compile error below and write a precise fix instruction (<= 3 lines), "
        "grounded ONLY in the provided real signatures/alternatives. Do not rewrite the whole file — output only "
        "what to change and how.\n\n"
        f"## Error\n{detail[-800:]}\n\n## Real signatures / alternatives\n" + "\n".join(ctx[:8])
    )
    try:
        return router.plan("You diagnose C# compile errors. Output only a concise fix instruction.", prompt).strip()
    except Exception:  # noqa: BLE001
        return ""

_FRAMEWORK = (REPO / "src" / "HeadlessDCGO.Engine" / "Assets" / "Scripts" / "Script"
              / "CardEffectCommons" / "CardPortingFramework.cs")
_PROMPT_DIR = Path(__file__).resolve().parent / "prompts"


def read_prompt(name: str, fallback: str) -> str:
    path = _PROMPT_DIR / name
    return path.read_text(encoding="utf-8") if path.exists() else fallback


SYSTEM_PROMPT = read_prompt(
    "system_porting.md",
    "You port Digimon TCG card effects from the original (Unity C#) to the headless engine (.NET C#).\n"
    "Output ONLY the finished .cs file content, as a single csharp code block. No explanations or comments.",
)
PLANNER_PROMPT = read_prompt("planner.md", "You are a card-porting planner. Write only fix instructions.")
REVIEWER_PROMPT = read_prompt("reviewer.md", "You are a card-porting reviewer. Output PASS or FAIL: fix instructions.")


def symbol_surface() -> str:
    """헤드리스가 실제로 정의한 유효 심볼을 프롬프트에 붙여 심볼 환각을 줄인다."""
    if not _FRAMEWORK.exists():
        return "## Available symbols\nCardPortingFramework.cs not found. Do not invent symbols."

    fw = _FRAMEWORK.read_text(encoding="utf-8", errors="ignore")
    timing = re.search(r"enum EffectTiming\s*\{(.*?)\}", fw, re.S)
    timings = list(dict.fromkeys(re.findall(r"\b([A-Z][A-Za-z]+)\b\s*(?:=|,|\n)", timing.group(1)))) if timing else []

    by_class: dict[str, list[str]] = {}
    class_spans = [(m.start(), m.group(1)) for m in re.finditer(r"\bclass ([A-Za-z0-9_]+)", fw)]
    for m in re.finditer(r"public static [A-Za-z0-9_<>,?\[\]. ]+ ([A-Za-z0-9_]+)\(([^;{]*?)\)", fw):
        cls = next((name for pos, name in reversed(class_spans) if pos < m.start()), "?")
        by_class.setdefault(cls, []).append(f"{m.group(1)}({' '.join(m.group(2).split())})")

    out = [
        "## Available symbols (do NOT invent names/args outside this list; forbidden properties/methods on domain types too)",
        f"### Valid EffectTiming (only these): {', '.join(timings)}",
    ]
    for cls in ("CardEffectFactory", "CardEffectCommons"):
        if cls in by_class:
            out.append(f"### {cls} signatures (exactly these names/args/class only):")
            out.extend(f"{cls}.{sig}" for sig in by_class[cls])

    out.append(
        "### Note: AS-IS Unity domain traversal (card.Owner.Enemy.SecurityCards etc.) has no 1:1 property in headless. "
        "Re-express such conditions via the CardEffectCommons predicates above. HeadlessPlayerId/HeadlessEntityId expose only Value/IsEmpty."
    )

    cheat = REPO / "docs" / "audit" / "porting_translation_cheatsheet.md"
    if cheat.exists():
        out.append("\n## Translation rules (AS-IS domain pattern -> headless)\n" + cheat.read_text(encoding="utf-8"))
    return "\n".join(out)


def _all_signatures() -> tuple[list[str], dict[str, list[str]]]:
    """(유효 타이밍, 클래스→시그니처) — 트림의 소스. 모듈 로드 시 1회 파싱."""
    if not _FRAMEWORK.exists():
        return [], {}
    fw = _FRAMEWORK.read_text(encoding="utf-8", errors="ignore")
    timing = re.search(r"enum EffectTiming\s*\{(.*?)\}", fw, re.S)
    timings = list(dict.fromkeys(re.findall(r"\b([A-Z][A-Za-z]+)\b\s*(?:=|,|\n)", timing.group(1)))) if timing else []
    class_spans = [(m.start(), m.group(1)) for m in re.finditer(r"\bclass ([A-Za-z0-9_]+)", fw)]
    by_cls: dict[str, list[str]] = {}
    for m in re.finditer(r"public static [A-Za-z0-9_<>,?\[\]. ]+ ([A-Za-z0-9_]+)\(([^;{]*?)\)", fw):
        cls = next((name for pos, name in reversed(class_spans) if pos < m.start()), "?")
        by_cls.setdefault(cls, []).append(f"{m.group(1)}({' '.join(m.group(2).split())})")
    return timings, by_cls


_TIMINGS, _BY_CLASS = _all_signatures()
_CHEAT = REPO / "docs" / "audit" / "porting_translation_cheatsheet.md"


def symbol_surface_for(texts: list[str]) -> str:
    """카드별 트림 심볼 표면 — 대상/레퍼런스 텍스트에 등장하는 팩토리·커먼즈 시그니처만.
    전체 ~39K 대신 필요한 것만 → 로컬 모델(31B)의 컨텍스트 부담·환각·응답시간 대폭 감소.
    레퍼런스 포팅본이 이미 정답 심볼을 보여주므로 전체 목록 불필요."""
    blob = "\n".join(texts)
    used = set(re.findall(r"\b([A-Za-z0-9_]+)\s*\(", blob))  # names actually called
    out = [
        "## Available symbols (do NOT use names/args outside these. NEVER invent a commons/factory not listed)",
        f"### Valid EffectTiming (only these): {', '.join(_TIMINGS)}",
    ]
    # Anti-invention: compact full name list. If a needed commons isn't in the reference, pick from this list.
    for cls in ("CardEffectFactory", "CardEffectCommons"):
        allnames = sorted({s.split("(")[0] for s in _BY_CLASS.get(cls, [])})
        if allnames:
            out.append(f"### {cls} full name list (choose ONLY from these; do not invent names):\n{', '.join(allnames)}")
    # Detailed signatures: only those appearing in the reference/target (exact args).
    for cls in ("CardEffectFactory", "CardEffectCommons"):
        picked = [s for s in _BY_CLASS.get(cls, []) if s.split("(")[0] in used]
        if picked:
            out.append(f"### {cls} signature details (exact args):")
            out.extend(f"{cls}.{s}" for s in picked)
    out.append("### The CARD's own property queries live on `card` (card.HasCardColor / card.Level / card.CardNames / "
               "card.Owner / card.IsDigimon), NOT on CardEffectCommons. A PERMANENT-property predicate uses "
               "CardEffectCommons.<Predicate>(id). HeadlessPlayerId/HeadlessEntityId expose only Value/IsEmpty. "
               "If a needed commons is not listed above, do not invent — use the closest one, or leave it as-is (no guessing).")
    if _CHEAT.exists():
        out.append("\n## Translation rules\n" + _CHEAT.read_text(encoding="utf-8"))
    return "\n".join(out)


def select_targets(tier: str, set_code: str, n: int) -> list[dict]:
    conn = sqlite3.connect(str(DB))
    conn.row_factory = sqlite3.Row
    if tier == "exact":
        where = "port_status='pending' AND set_code=? AND reference_card IS NOT NULL"
    elif tier in ("family", "cold"):
        where = "port_status='pending' AND set_code=? AND reference_card IS NULL AND action_tags != '[]'"
    else:
        raise SystemExit(f"unknown tier: {tier}")
    rows = conn.execute(
        f"SELECT card_id, action_tags, shape FROM card WHERE {where} ORDER BY card_id LIMIT ?",
        (set_code, n),
    ).fetchall()
    conn.close()
    return [dict(r) for r in rows]


def family_reference(conn: sqlite3.Connection, action_tags: str, exclude: str) -> str | None:
    row = conn.execute(
        "SELECT card_id FROM card WHERE port_status='ported' AND action_tags=? AND card_id!=? LIMIT 1",
        (action_tags, exclude),
    ).fetchone()
    return row[0] if row else None


def _tags_of(card_id: str) -> str:
    conn = sqlite3.connect(str(DB))
    row = conn.execute("SELECT action_tags FROM card WHERE card_id=?", (card_id,)).fetchone()
    conn.close()
    return row[0] if row else "[]"


def _self_reference(ref_id: str) -> dict:
    t = build_task(sqlite3.connect(str(DB)), ref_id)
    return {"card_id": ref_id, "asis": t["target_asis"], "ported": _ported_text(ref_id)}


def _render_task(task: dict) -> str:
    parts = [f"# Target card: {task['card_id']}", "## Target AS-IS", task["target_asis"]]
    ref = task.get("reference")
    if ref:
        parts += [f"## Reference {ref['card_id']} — AS-IS", ref["asis"]]
        if ref.get("ported"):
            parts += [f"## Reference {ref['card_id']} — headless port (model it on this)", ref["ported"]]
    parts += ["## Instruction", task.get("instruction") or ""]
    return "\n\n".join(parts)


def build_prompt(conn: sqlite3.Connection, card_id: str, tier: str) -> tuple[str, dict] | None:
    task = build_task(sqlite3.connect(str(DB)), card_id)
    if tier == "family":
        ref = family_reference(sqlite3.connect(str(DB)), _tags_of(card_id), card_id)
        if ref is None:
            return None
        ref_task = build_task(sqlite3.connect(str(DB)), ref)
        task["reference"] = ref_task.get("reference") or _self_reference(ref)
        task["instruction"] = (
            f"Reference {ref} is the same action family as the target but may differ in structure. "
            "Use the reference's conversion approach, but port to the target AS-IS's actual structure and args."
        )
    elif tier == "cold":
        task["reference"] = None
        task["instruction"] = "Write the headless .NET port from the target AS-IS alone, with no reference."
    return _render_task(task), task


def pick_reference(conn: sqlite3.Connection, card_id: str) -> tuple[str | None, str]:
    """레퍼런스 자동 선정: exact > family > cold."""
    row = conn.execute("SELECT reference_card, action_tags FROM card WHERE card_id=?", (card_id,)).fetchone()
    if row and row[0]:
        return row[0], "exact"
    fam = conn.execute(
        "SELECT card_id FROM card WHERE port_status='ported' AND action_tags=? AND card_id!=? LIMIT 1",
        (row[1] if row else "[]", card_id),
    ).fetchone()
    if fam:
        return fam[0], "family"
    return None, "cold"


def build_prompt_for(card_id: str, ref_id: str | None, tier: str) -> tuple[str, dict]:
    task = build_task(sqlite3.connect(str(DB)), card_id)
    if tier == "exact":
        pass
    elif tier == "family" and ref_id:
        rt = build_task(sqlite3.connect(str(DB)), ref_id)
        task["reference"] = {"card_id": ref_id, "asis": rt["target_asis"], "ported": _ported_text(ref_id)}
        task["instruction"] = (
            f"Reference {ref_id} is the same action family as the target (structure may differ). "
            "Use the conversion approach, but port to the target AS-IS's actual structure and args. "
            "You MUST apply the translation rules (domain traversal -> commons)."
        )
    else:
        task["reference"] = None
        task["instruction"] = "Write the headless port from the target AS-IS alone, with no reference."
    return _render_task(task), task


def _ported_text(card_id: str) -> str:
    base = REPO / "src" / "HeadlessDCGO.Engine" / "Assets" / "Scripts" / "CardEffect"
    if not base.exists():
        return ""
    for p in base.rglob(f"{card_id}.cs"):
        return p.read_text(encoding="utf-8", errors="ignore")
    return ""


def compile_gate(cs_text: str, card_id: str, source_path: str, keep_on_pass: bool = False) -> tuple[bool, str]:
    """G1: 생성 .cs를 대상 경로에 배치하고 엔진 빌드. 실패 또는 keep=False면 원복."""
    target = REPO / "src" / "HeadlessDCGO.Engine" / "Assets" / "Scripts" / "CardEffect" / source_path
    backup = target.read_text(encoding="utf-8") if target.exists() else None
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(cs_text, encoding="utf-8")

    env_path = f"{REPO / '.dotnet'}:{__import__('os').environ.get('PATH', '')}"
    proc = subprocess.run(
        ["dotnet", "build", "src/HeadlessDCGO.Engine/HeadlessDCGO.Engine.csproj", "--nologo", "-v", "q"],
        cwd=str(REPO), capture_output=True, text=True, timeout=300,
        env={**__import__('os').environ, "PATH": env_path},
    )
    ok = proc.returncode == 0
    detail = "" if ok else (proc.stdout + proc.stderr)[-1800:]

    if ok and keep_on_pass:
        pass
    elif backup is not None:
        target.write_text(backup, encoding="utf-8")
    else:
        target.unlink(missing_ok=True)
    return ok, detail


def maybe_plan(router: LocalModelRouter, tier: str, attempt: int, user0: str, detail: str | None = None) -> str:
    """family는 처음부터, exact는 반복 실패 후 planner 지시를 추가한다."""
    if tier == "family" and attempt == 1:
        return router.plan(PLANNER_PROMPT, f"Analyze the porting task below and write implementation instructions for the coder.\n\n{user0}")
    if attempt >= 3 and detail:
        return router.plan(
            PLANNER_PROMPT,
            f"The porting attempt below has failed repeatedly. Analyze the compile-error cause and write only fix instructions for the coder.\n\n"
            f"## Original task\n{user0}\n\n## Compile error\n{detail[-1500:]}",
        )
    return ""


def review_pass(router: LocalModelRouter, user0: str, cs: str) -> tuple[bool, str]:
    review = router.review(
        REVIEWER_PROMPT,
        f"The card below compiled. Review whether the generated code means the same as the original AS-IS.\n\n"
        f"## Task\n{user0}\n\n## Generated code\n```csharp\n{cs}\n```",
    ).strip()
    return review.upper().startswith("PASS"), review


def port_card(
    conn: sqlite3.Connection,
    card_id: str,
    router: LocalModelRouter,
    system: str,
    max_retries: int,
    keep: bool,
    no_compile: bool = False,
    semantic_review: bool = False,
) -> dict:
    ref_id, tier = pick_reference(conn, card_id)
    # (cold 층2) cold은 다층 오류(using 누락·네임스페이싱)라 한 층씩 걷어내려면 재시도 여유가 더 필요.
    if tier == "cold":
        max_retries += 2
    user0, task = build_prompt_for(card_id, ref_id, tier)
    source_rel = task["target_path"].split("CardEffect/")[-1]
    # 카드별 트림 심볼 표면: 대상 AS-IS + 레퍼런스(AS-IS·포팅본)에 등장한 심볼만(system은 원칙 프롬프트).
    ref_texts = [task["target_asis"]]
    if task.get("reference"):
        ref_texts += [task["reference"].get("asis", ""), task["reference"].get("ported", "")]
    system = system + "\n\n" + symbol_surface_for(ref_texts)
    # (action_tag→factory) 이 카드의 action_tags에 해당하는 정규 팩토리를 주입 — 레퍼런스 없어도 액션으로 접지.
    _atrow = conn.execute("SELECT action_tags FROM card WHERE card_id=?", (card_id,)).fetchone()
    _amsurface = action_map_surface(_atrow[0] if _atrow else None)
    if _amsurface:
        system = system + "\n\n" + _amsurface
    record = {"card_id": card_id, "tier": tier, "reference": ref_id, "ok": False, "attempts": 0}

    last_detail = ""
    for attempt in range(1, max_retries + 2):
        record["attempts"] = attempt
        try:
            plan = maybe_plan(router, tier, attempt, user0, last_detail)
            user = user0
            if plan:
                user += f"\n\n## Planner instructions\n{plan}"
            if last_detail:
                user += (
                    f"\n\n## Previous attempt's compile error (fix it)\n{last_detail[-1200:]}\n"
                    "Fix exactly the errors above and output the complete corrected file (one code block)."
                )
            cs = router.code(system, user)
        except Exception as ex:  # noqa: BLE001
            record["error"] = str(ex)[:500]
            return record

        # (#3 pre-build validator) 무효 심볼이면 비싼 엔진 빌드를 건너뛰고 정밀 힌트로 즉시 재시도.
        vdetail = _validate_symbols_text(cs)
        if vdetail:
            record["validator_hits"] = record.get("validator_hits", 0) + 1
            last_detail = vdetail
            record["last_detail"] = vdetail
            continue

        if no_compile:
            record.update({"ok": True, "generated_chars": len(cs), "compile_skipped": True, "code": cs})
            return record

        ok, detail = compile_gate(cs, card_id, source_rel, keep_on_pass=keep)
        if ok:
            record["compile_ok"] = True
            if semantic_review:
                passed, review = review_pass(router, user0, cs)
                record["review"] = review[:1000]
                if not passed:
                    # keep=True로 이미 남긴 경우 의미검수 실패 시 원복이 필요할 수 있으니 기본은 keep=False 파일럿에서 사용 권장.
                    last_detail = "의미 검수 실패: " + review
                    continue
            record["ok"] = True
            return record

        # (Step 2 gemma 진단) 실컴파일 오류 → gemma가 진짜 시그니처/§9 근거로 정밀 수정 지시를 덧붙임.
        last_detail = detail
        diag = _diagnose_compile_error(router, detail)
        if diag:
            record["diagnosed"] = record.get("diagnosed", 0) + 1
            last_detail = f"{detail[-900:]}\n\n## 진단(gemma) — 이대로 고쳐라\n{diag}"
        record["last_detail"] = last_detail[-800:]

    return record


def run_all_pending(
    conn: sqlite3.Connection,
    router: LocalModelRouter,
    system: str,
    set_code: str,
    max_retries: int,
    keep: bool,
    log_path: Path,
    limit: int | None,
    skip_cold: bool = True,
    no_compile: bool = False,
    semantic_review: bool = False,
) -> None:
    where = "port_status='pending' AND readiness!='blocked' AND set_code=?"
    rows = conn.execute(f"SELECT card_id FROM card WHERE {where} ORDER BY card_id", (set_code,)).fetchall()
    done = set()
    if log_path.exists():
        for line in log_path.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            rec = json.loads(line)
            if rec.get("ok"):
                done.add(rec["card_id"])
    cards = [r[0] for r in rows if r[0] not in done][: limit or None]
    print(f"set={set_code} pending {len(rows)}장 중 미완 {len(cards)}장 (skip_cold={skip_cold}, 재시도 {max_retries})")

    passed = 0
    by_tier: dict[str, list[int]] = {}
    for i, cid in enumerate(cards, 1):
        ref_id, tier = pick_reference(conn, cid)
        if skip_cold and tier == "cold":
            rec = {"card_id": cid, "tier": "cold", "ok": False, "skipped": "no_reference"}
            _log(log_path, rec)
            _log_cold_queue(log_path.parent / "cold_queue.jsonl", rec)
            print(f"  [{i}/{len(cards)}] {cid} (cold): SKIP(레퍼런스 없음)")
            by_tier.setdefault("cold", [0, 0])[1] += 1
            continue

        rec = port_card(conn, cid, router, system, max_retries, keep, no_compile, semantic_review)
        _log(log_path, rec)
        by_tier.setdefault(rec["tier"], [0, 0])
        by_tier[rec["tier"]][1] += 1
        if rec.get("ok"):
            passed += 1
            by_tier[rec["tier"]][0] += 1
        mark = "PASS" if rec.get("ok") else ("CALL-FAIL" if "error" in rec else "FAIL")
        print(f"  [{i}/{len(cards)}] {cid} ({rec['tier']}, {rec['attempts']}회): {mark}")

    print(f"\n=== {set_code} 완료: {passed}/{len(cards)} 통과 ===")
    for tier, (ok, tot) in sorted(by_tier.items()):
        print(f"  {tier}: {ok}/{tot}")


def _log(path: Path, record: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "a", encoding="utf-8") as f:
        f.write(json.dumps(record, ensure_ascii=False) + "\n")


def _log_cold_queue(path: Path, record: dict) -> None:
    _log(path, record)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--tier", choices=["exact", "family", "cold"], help="파일럿 티어 모드")
    parser.add_argument("--all-pending", action="store_true", help="세트 전체 pending 포팅")
    parser.add_argument("--set", dest="set_code", default="BT1")
    parser.add_argument("--n", type=int, default=15)
    parser.add_argument("--limit", type=int, help="all-pending 제한")
    parser.add_argument("--retries", type=int, default=2, help="컴파일-수정 재시도 횟수")
    parser.add_argument("--keep", action="store_true", help="통과 카드를 대상 경로에 유지")
    parser.add_argument("--out", default="../runs/local-pilot")
    parser.add_argument("--no-compile", action="store_true", help="G1 컴파일 게이트 생략")
    parser.add_argument("--skip-cold", action="store_true", default=True, help="cold 카드는 큐에 기록하고 스킵")
    parser.add_argument("--include-cold", action="store_true", help="cold도 생성 시도")
    parser.add_argument("--semantic-review", action="store_true", help="컴파일 통과 후 reviewer 의미 검수 수행")
    args = parser.parse_args()

    system_prompt = SYSTEM_PROMPT  # 심볼 표면은 port_card 내부에서 카드별 트림으로 붙인다.
    router = LocalModelRouter()

    if args.include_cold:
        args.skip_cold = False

    if args.all_pending:
        out = (REPO / args.out).resolve() / args.set_code
        out.mkdir(parents=True, exist_ok=True)
        run_all_pending(
            sqlite3.connect(str(DB)), router, system_prompt, args.set_code,
            args.retries, args.keep, out / "results.jsonl", args.limit,
            skip_cold=args.skip_cold, no_compile=args.no_compile, semantic_review=args.semantic_review,
        )
        return

    if not args.tier:
        parser.error("--tier 또는 --all-pending 필요")

    out_dir = (REPO / args.out).resolve() / args.tier
    out_dir.mkdir(parents=True, exist_ok=True)
    log_path = out_dir / "results.jsonl"

    conn = sqlite3.connect(str(DB))
    targets = select_targets(args.tier, args.set_code, args.n)
    print(f"tier={args.tier} set={args.set_code} 대상 {len(targets)}장")

    passed = 0
    for t in targets:
        cid = t["card_id"]
        if args.tier == "cold" and args.skip_cold:
            rec = {"card_id": cid, "tier": "cold", "ok": False, "skipped": "no_reference"}
            _log(log_path, rec)
            _log_cold_queue(out_dir / "cold_queue.jsonl", rec)
            print(f"  {cid}: SKIP(cold)")
            continue
        rec = port_card(conn, cid, router, system_prompt, args.retries, args.keep, args.no_compile, args.semantic_review)
        _log(log_path, rec)
        passed += int(bool(rec.get("ok")))
        mark = "PASS" if rec.get("ok") else ("CALL-FAIL" if "error" in rec else "FAIL")
        print(f"  {cid}: {mark}")

    print(f"\n통과: {passed}/{len(targets)} (tier={args.tier})")
    print(f"산출: {out_dir}")


if __name__ == "__main__":
    main()
