"""카드 포팅 파일럿 하네스 — Sonnet 4.6로 티어별 유추 포팅 실측 (card_porting_pilot_design.md).

DB에서 티어별(exact/family/cold) 대상 카드를 뽑아 P-DB1 태스크 카드를 만들고, Sonnet 4.6에게 유추 포팅을
시킨 뒤 결과 .cs와 메타를 기록한다. G1(컴파일)은 이 스크립트가, G2~G4는 후속 스텝이 처리.

전제: pip install anthropic  (그리고 ANTHROPIC_API_KEY 또는 `ant auth login` 프로파일)
사용:
  python tools/porting/pilot/port_with_sonnet.py --tier exact  --set BT1 --n 15 --out ../runs/pilot
  python tools/porting/pilot/port_with_sonnet.py --tier family --set BT1 --n 15 --out ../runs/pilot
  python tools/porting/pilot/port_with_sonnet.py --tier cold   --set BT1 --n 15 --out ../runs/pilot
"""

from __future__ import annotations

import argparse
import json
import re
import sqlite3
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
DB = REPO / "docs" / "porting" / "card_ir.sqlite"
sys.path.insert(0, str(REPO / "tools" / "porting"))
from porting_task import build_task  # noqa: E402  (DB 스키마 공유)

MODEL = "claude-sonnet-4-6"  # 사용자 지정. Opus 기본을 쓰지 않는다.

SYSTEM_PROMPT = """\
너는 Digimon TCG 헤드리스 엔진의 카드 효과를 원본(Unity C#)에서 헤드리스(.NET C#)로 포팅한다.
핵심 원칙(반드시 지킴):
- 레퍼런스 카드의 AS-IS→헤드리스 변환을 그대로 적용하고, 대상 AS-IS의 인자값(DP·매수·조건 술어·타이밍 등)만
  대상에 맞게 바꿔 넣는다. 구조·팩토리 이름·논리 분해·네임스페이스 규칙은 레퍼런스와 동일하게 유지한다.
- 추측 금지. 원본에 없는 동작을 넣거나 가드를 완화하지 않는다. 원본이 하는 것을 정확히 미러한다.
- 헤드리스에 없는 프리미티브를 발명하지 않는다(있는 팩토리만 사용).

STOP 정책(맞는 프리미티브가 없을 때 — 필수, 발명·throw·근사 전부 금지):
- 어떤 타이밍의 효과를 충실히 커버하는 기존 헤드리스 팩토리/프리미티브가 없으면, 그 타이밍은 STOP 처리한다:
  · 그 `if (timing == ...)` 블록에 아무 효과도 등록하지 않는다(블록을 비우거나 생략).
  · 바로 위에 `// STOP: <사유>` 주석을 남긴다 — 어떤 프리미티브/조합/액션이 없어서 못 하는지, AS-IS가 뭘
    요구했는지 한 줄로 명시(예: `// STOP: self-suspend 코스트 후 reveal-route 조합 body 없음`).
  · 절대 throw 하지 않는다(`throw new NotSupportedException` 등 금지). STOP은 주석 달린 조용한 no-op이다.
  · 절대 근사·확대·가드완화로 안 맞는 프리미티브에 억지로 매핑하지 않는다. 충실한 STOP이 부정확한 등록보다 낫다.
- 일부 타이밍만 포팅하고 나머지는 STOP 주석 처리해도 된다(부분 포팅 허용).
- 카드 전체가 맞는 프리미티브가 없으면 빈 리스트를 반환하고 파일 상단에 `// STOP:` 사유를 단다. 컴파일은 되어야 한다.
출력: 완성된 .cs 파일 내용만. 코드 블록(```csharp ... ```) 하나로. 산문 설명은 금지하되, 위 `// STOP:` 주석은 필수."""


_FRAMEWORK = (REPO / "src" / "HeadlessDCGO.Engine" / "Assets" / "Scripts" / "Script"
              / "CardEffectCommons" / "CardPortingFramework.cs")


def symbol_surface() -> str:
    """헤드리스가 실제로 정의한 유효 심볼(타이밍·팩토리 시그니처) — 환각 방지용 프롬프트 부록.
    첫 파일럿 카드에서 모델이 EffectTiming/커먼즈/인자를 지어내 컴파일 실패 → DB가 가진 실심볼을 제공."""
    fw = _FRAMEWORK.read_text(encoding="utf-8", errors="ignore")
    timing = re.search(r"enum EffectTiming\s*\{(.*?)\}", fw, re.S)
    timings = list(dict.fromkeys(re.findall(r"\b([A-Z][A-Za-z]+)\b\s*(?:=|,|\n)", timing.group(1)))) if timing else []

    # 메서드를 선언 클래스로 귀속 — 커먼즈(술어)와 팩토리(효과)를 정확히 구분(잘못된 클래스 호출 방지).
    by_class: dict[str, list[str]] = {}
    for m in re.finditer(r"(?:public |internal )?(?:static )?(?:partial )?class ([A-Za-z0-9_]+)", fw):
        pass  # 클래스 경계 수집은 아래 스캔에서
    class_spans = [(m.start(), m.group(1)) for m in re.finditer(r"\bclass ([A-Za-z0-9_]+)", fw)]
    for m in re.finditer(r"public static [A-Za-z0-9_<>,?\[\]. ]+ ([A-Za-z0-9_]+)\(([^;{]*?)\)", fw):
        cls = next((name for pos, name in reversed(class_spans) if pos < m.start()), "?")
        by_class.setdefault(cls, []).append(f"{m.group(1)}({' '.join(m.group(2).split())})")

    out = ["## 사용 가능한 심볼(이 목록 밖의 이름·인자를 지어내지 마라. 도메인 타입에 없는 속성/메서드도 금지)",
           f"### 유효 EffectTiming (이 중에서만): {', '.join(timings)}"]
    for cls in ("CardEffectFactory", "CardEffectCommons"):
        if cls in by_class:
            out.append(f"### {cls} 시그니처(정확히 이 이름·인자·클래스만):")
            out.extend(f"{cls}.{sig}" for sig in by_class[cls])
    out.append(
        "### 주의: AS-IS의 Unity 도메인 탐색(card.Owner.Enemy.SecurityCards 등)은 헤드리스에 1:1 속성이 "
        "없다. 그런 조건은 위 CardEffectCommons 술어로 재표현하라(예: 상대 필드 조건은 "
        "HasMatchConditionOpponentsPermanent). HeadlessPlayerId/HeadlessEntityId는 Value·IsEmpty만 있다.")
    # (파일럿) 번역 치트시트 부록 — 의미 번역 계층(CS1061 도메인 환각) 규칙.
    cheat = REPO / "docs" / "audit" / "porting_translation_cheatsheet.md"
    if cheat.exists():
        out.append("\n## 번역 규칙(컴팩트: 매핑 테이블·예시만)\n" + _CHEAT_COMPACT)
    return "\n".join(out)


def _all_signatures() -> tuple[list[str], dict[str, list[str]]]:
    """(유효 타이밍, 클래스→시그니처 목록) 캐시. 전체 표면(트림 소스)."""
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
_CHEAT = (REPO / "docs" / "audit" / "porting_translation_cheatsheet.md")


def _compact_cheat() -> str:
    """치트시트 다이어트 — 매핑 테이블·헤더·코드예시만 유지하고 설명 산문은 제거.
    매핑 규칙(false STOP/환각 방지의 핵심)은 그대로 두면서 프롬프트를 ~35% 줄여 CLI 타임아웃을 완화한다."""
    if not _CHEAT.exists():
        return ""
    kept: list[str] = []
    in_fence = False
    for line in _CHEAT.read_text(encoding="utf-8").splitlines():
        s = line.strip()
        if s.startswith("```"):
            in_fence = not in_fence
            kept.append(line)
            continue
        if in_fence or s.startswith("#") or s.startswith("|"):
            kept.append(line)
    return "\n".join(kept)


_CHEAT_COMPACT = _compact_cheat()


def symbol_surface_for(texts: list[str]) -> str:
    """카드별 트림 심볼 표면 — 대상/레퍼런스 텍스트에 등장하는 팩토리·커먼즈 시그니처만.
    전체 43K 대신 필요한 것만 → 호출 속도·안정성 대폭 개선(레퍼런스가 이미 정답 심볼을 보여줌)."""
    blob = "\n".join(texts)
    used = set(re.findall(r"\b([A-Za-z0-9_]+)\s*\(", blob))  # 호출된 이름
    out = [f"### 유효 EffectTiming (이 중에서만): {', '.join(_TIMINGS)}"]
    for cls in ("CardEffectFactory", "CardEffectCommons"):
        picked = [s for s in _BY_CLASS.get(cls, []) if s.split("(")[0] in used]
        if picked:
            out.append(f"### {cls} 시그니처(정확히 이 이름·인자만):")
            out.extend(f"{cls}.{s}" for s in picked)
    out.append("### HeadlessPlayerId/HeadlessEntityId는 Value·IsEmpty만. permanent.X는 CardEffectCommons.X(card,id)로.")
    if _CHEAT.exists():
        out.append("\n## 번역 규칙(컴팩트: 매핑 테이블·예시만)\n" + _CHEAT_COMPACT)
    return "\n".join(out)


def select_targets(tier: str, set_code: str, n: int) -> list[dict]:
    conn = sqlite3.connect(str(DB))
    conn.row_factory = sqlite3.Row
    if tier == "exact":
        where = "port_status='pending' AND set_code=? AND reference_card IS NOT NULL"
    elif tier in ("family", "cold"):
        # family/cold: 레퍼런스가 없는 pending에서. family는 하네스가 액션-패밀리 레퍼런스를 붙이고,
        # cold는 레퍼런스 없이 대상만 준다(대조군). 액션 태그 보유 카드 우선(발화 검증 가능).
        where = "port_status='pending' AND set_code=? AND reference_card IS NULL AND action_tags != '[]'"
    else:
        raise SystemExit(f"unknown tier: {tier}")
    rows = conn.execute(
        f"SELECT card_id, action_tags, shape FROM card WHERE {where} ORDER BY card_id LIMIT ?",
        (set_code, n),
    ).fetchall()
    conn.close()
    return [dict(r) for r in rows]


def family_reference(conn, action_tags: str, exclude: str) -> str | None:
    """같은 액션 태그의 이미 포팅된 카드 = family 레퍼런스(구조 다를 수 있음, 순도 56%)."""
    row = conn.execute(
        "SELECT card_id FROM card WHERE port_status='ported' AND action_tags=? AND card_id!=? LIMIT 1",
        (action_tags, exclude),
    ).fetchone()
    return row[0] if row else None


def build_prompt(conn, card_id: str, tier: str) -> tuple[str, dict] | None:
    task = build_task(conn if False else sqlite3.connect(str(DB)), card_id)
    if tier == "family":
        ref = family_reference(sqlite3.connect(str(DB)), _tags_of(card_id), card_id)
        if ref is None:
            return None  # 이 카드엔 family 레퍼런스가 없음 → 표본에서 제외
        ref_task = build_task(sqlite3.connect(str(DB)), ref)
        task["reference"] = ref_task.get("reference") or _self_reference(ref)
        task["instruction"] = (
            f"레퍼런스 {ref}는 대상과 같은 액션 계열이지만 구조가 다를 수 있다. 레퍼런스의 변환 방식을 참고하되, "
            "대상 AS-IS의 실제 구조·인자에 맞게 포팅하라."
        )
    elif tier == "cold":
        task["reference"] = None
        task["instruction"] = "레퍼런스 없이 대상 AS-IS만으로 헤드리스 .NET 포팅을 작성하라(구조 동일 원칙 준수)."

    user = _render_task(task)
    return user, task


def _tags_of(card_id: str) -> str:
    conn = sqlite3.connect(str(DB))
    row = conn.execute("SELECT action_tags FROM card WHERE card_id=?", (card_id,)).fetchone()
    conn.close()
    return row[0] if row else "[]"


def _self_reference(ref_id: str) -> dict:
    t = build_task(sqlite3.connect(str(DB)), ref_id)
    return {"card_id": ref_id, "asis": t["target_asis"], "ported": ""}  # ported는 후속에서 채움(스텁)


def _render_task(task: dict) -> str:
    parts = [f"# 대상 카드: {task['card_id']}", "## 대상 AS-IS", task["target_asis"]]
    ref = task.get("reference")
    if ref:
        parts += [f"## 레퍼런스 {ref['card_id']} — AS-IS", ref["asis"]]
        if ref.get("ported"):
            parts += [f"## 레퍼런스 {ref['card_id']} — 헤드리스 포팅본(본떠라)", ref["ported"]]
    parts += ["## 지시", task["instruction"] or ""]
    return "\n\n".join(parts)


def call_sonnet_sdk(client, system: str, user: str) -> str:
    """Anthropic SDK 경로(API 키 필요)."""
    with client.messages.stream(
        model=MODEL,
        max_tokens=8000,
        thinking={"type": "adaptive"},
        output_config={"effort": "high"},
        system=[{"type": "text", "text": system, "cache_control": {"type": "ephemeral"}}],
        messages=[{"role": "user", "content": user}],
    ) as stream:
        message = stream.get_final_message()

    if message.stop_reason == "refusal":
        raise RuntimeError(f"refused: {message.stop_details}")
    text = "".join(b.text for b in message.content if b.type == "text")
    return _extract_code(text)


def call_sonnet_cli(system: str, user: str) -> str:
    """Claude Code CLI 경로(API 키 불필요 — 현 세션 인증 재사용). 순수 코드생성: 도구 비활성.
    큰 시스템 프롬프트(심볼 표면+치트시트)는 CLI 인자 길이 한계를 넘으므로 user 프롬프트 앞에 인라인한다."""
    import os

    combined = f"<규칙>\n{system}\n</규칙>\n\n{user}"
    proc = subprocess.run(
        ["claude", "-p", "--model", MODEL, "--allowed-tools", ""],
        input=combined, capture_output=True, text=True, timeout=180,
        cwd="/tmp", env={**os.environ},
    )
    if proc.returncode != 0:
        raise RuntimeError(f"claude cli exit {proc.returncode}: {proc.stderr[-500:]}")
    return _extract_code(proc.stdout)


def _extract_code(text: str) -> str:
    m = re.search(r"```(?:csharp|cs)?\n(.*?)```", text, re.S)
    return m.group(1).strip() if m else text.strip()


def compile_gate(cs_text: str, card_id: str, source_path: str, out_dir: Path | None = None,
                 keep_on_pass: bool = False) -> tuple[bool, str]:
    """G1: 생성 .cs를 대상 경로에 배치하고 엔진 빌드. keep_on_pass=True면 통과 시 그대로 남긴다."""
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
    detail = "" if ok else (proc.stdout + proc.stderr)[-1500:]
    if ok and keep_on_pass:
        pass  # 대상 경로에 그대로 유지(실제 포팅 산출)
    elif backup is not None:
        target.write_text(backup, encoding="utf-8")  # 원복(스켈레톤/이전본)
    else:
        target.unlink(missing_ok=True)
    return ok, detail


def pick_reference(conn, card_id: str) -> tuple[str | None, str]:
    """레퍼런스 자동 선정: exact(같은 시그니처 포팅본) > family(같은 액션태그 포팅본) > cold(없음)."""
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
        pass  # build_task가 이미 exact 레퍼런스 채움
    elif tier == "family" and ref_id:
        rt = build_task(sqlite3.connect(str(DB)), ref_id)
        ported = _ported_text(ref_id)
        task["reference"] = {"card_id": ref_id, "asis": rt["target_asis"], "ported": ported}
        task["instruction"] = (
            f"레퍼런스 {ref_id}는 대상과 같은 액션 계열이다(구조는 다를 수 있음). 변환 방식을 참고하되 대상 "
            "AS-IS의 실제 구조·인자에 맞게 포팅하라. 번역 규칙(도메인 탐색→커먼즈)을 반드시 적용하라.")
    else:  # cold
        task["reference"] = None
        task["instruction"] = "레퍼런스 없이 대상 AS-IS만으로 헤드리스 포팅을 작성하라(구조 동일 + 번역 규칙 준수)."
    return _render_task(task), task


def _ported_text(card_id: str) -> str:
    for p in (REPO / "src" / "HeadlessDCGO.Engine" / "Assets" / "Scripts" / "CardEffect").rglob(f"{card_id}.cs"):
        return p.read_text(encoding="utf-8", errors="ignore")
    return ""


def port_card(conn, card_id: str, call, base_system: str, max_retries: int, keep: bool) -> dict:
    """1카드 포팅 + 컴파일-수정 재시도. keep=True면 통과 시 대상 경로에 남긴다(실제 포팅 산출).
    심볼 표면은 이 카드가 쓰는 것만 트림(base_system은 원칙 프롬프트만)."""
    ref_id, tier = pick_reference(conn, card_id)
    user0, task = build_prompt_for(card_id, ref_id, tier)
    source_rel = task["target_path"].split("CardEffect/")[-1]
    # 카드별 트림 심볼 표면: 대상 AS-IS + 레퍼런스(AS-IS·포팅본)에 등장한 심볼만.
    ref_texts = [task["target_asis"]]
    if task.get("reference"):
        ref_texts += [task["reference"].get("asis", ""), task["reference"].get("ported", "")]
    system = base_system + "\n\n## 사용 가능한 심볼(밖의 이름·인자 금지)\n" + symbol_surface_for(ref_texts)
    record = {"card_id": card_id, "tier": tier, "reference": ref_id, "ok": False, "attempts": 0}

    user = user0
    for attempt in range(1, max_retries + 2):
        record["attempts"] = attempt
        try:
            cs = call(system, user)
        except Exception as ex:  # noqa: BLE001
            record["error"] = str(ex)[:200]
            return record
        ok, detail = compile_gate(cs, card_id, source_rel, keep_on_pass=keep)
        if ok:
            record["ok"] = True
            return record
        record["last_detail"] = detail[-400:]
        # 컴파일-수정 재시도: 오류를 피드백해 재생성.
        user = (f"{user0}\n\n## 직전 시도의 컴파일 오류(고쳐라)\n{detail[-900:]}\n"
                "위 오류만 정확히 수정한 완성본을 다시 출력하라(코드 블록 하나).")
    return record


def run_all_pending(conn, call, system: str, set_code: str, max_retries: int,
                    keep: bool, log_path: Path, limit: int | None, skip_cold: bool = True) -> None:
    """전체 pending 포팅(레퍼런스 자동선정 + 컴파일-수정 재시도). keep=True면 통과분 대상 경로에 유지.
    skip_cold=True: 레퍼런스 없는(cold) 카드는 건너뛴다(실측 0% 통과 — Opus 레퍼런스 시딩 대상으로 큐잉).
    재개 안전: 이미 log에 통과 기록된 카드는 스킵."""
    where = "port_status='pending' AND readiness!='blocked' AND set_code=?"
    rows = conn.execute(f"SELECT card_id FROM card WHERE {where} ORDER BY card_id", (set_code,)).fetchall()
    done = set()
    if log_path.exists():
        done = {json.loads(l)["card_id"] for l in log_path.read_text().splitlines()
                if l.strip() and json.loads(l).get("ok")}
    cards = [r[0] for r in rows if r[0] not in done][: limit or None]
    print(f"set={set_code} pending {len(rows)}장 중 미완 {len(cards)}장 (skip_cold={skip_cold}, 재시도 {max_retries})")

    passed = 0
    by_tier: dict[str, list[int]] = {}
    for i, cid in enumerate(cards, 1):
        ref_id, tier = pick_reference(conn, cid)
        if skip_cold and tier == "cold":
            _log(log_path, {"card_id": cid, "tier": "cold", "ok": False, "skipped": "no_reference"})
            print(f"  [{i}/{len(cards)}] {cid} (cold): SKIP(레퍼런스 없음 → Opus 시딩 대상)")
            by_tier.setdefault("cold", [0, 0])[1] += 1
            continue
        rec = port_card(conn, cid, call, system, max_retries, keep)
        _log(log_path, rec)
        by_tier.setdefault(rec["tier"], [0, 0])
        by_tier[rec["tier"]][1] += 1
        if rec["ok"]:
            passed += 1
            by_tier[rec["tier"]][0] += 1
        mark = "PASS" if rec["ok"] else ("CALL-FAIL" if "error" in rec else "FAIL")
        print(f"  [{i}/{len(cards)}] {cid} ({rec['tier']}, {rec['attempts']}회): {mark}")

    print(f"\n=== {set_code} 완료: {passed}/{len(cards)} 컴파일 통과 ===")
    for tier, (ok, tot) in sorted(by_tier.items()):
        print(f"  {tier}: {ok}/{tot}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--tier", choices=["exact", "family", "cold"], help="파일럿 티어 모드(표본 측정)")
    parser.add_argument("--all-pending", action="store_true", help="세트 전체 pending 포팅(프로덕션)")
    parser.add_argument("--set", dest="set_code", default="BT1")
    parser.add_argument("--n", type=int, default=15)
    parser.add_argument("--limit", type=int, help="all-pending 제한(디버그)")
    parser.add_argument("--retries", type=int, default=2, help="컴파일-수정 재시도 횟수")
    parser.add_argument("--keep", action="store_true", help="통과 카드를 대상 경로에 유지(실제 포팅)")
    parser.add_argument("--out", default="../runs/pilot")
    parser.add_argument("--no-compile", action="store_true", help="G1 컴파일 게이트 생략(생성만)")
    parser.add_argument("--backend", choices=["cli", "sdk"], default="cli",
                        help="cli=Claude Code(키 불필요) / sdk=Anthropic SDK(API 키)")
    args = parser.parse_args()

    system_prompt = SYSTEM_PROMPT + "\n\n" + symbol_surface()  # 환각 방지 심볼 표면 부록

    if args.backend == "sdk":
        import anthropic  # 지연 import
        client = anthropic.Anthropic()
        call = lambda s, u: call_sonnet_sdk(client, s, u)  # noqa: E731
    else:
        call = call_sonnet_cli

    if args.all_pending:
        out = (REPO / args.out).resolve() / args.set_code
        out.mkdir(parents=True, exist_ok=True)
        run_all_pending(sqlite3.connect(str(DB)), call, SYSTEM_PROMPT, args.set_code,  # 트림은 port_card 내부
                        args.retries, args.keep, out / "results.jsonl", args.limit)
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
        built = build_prompt(conn, cid, args.tier)
        if built is None:
            print(f"  {cid}: skip(레퍼런스 없음)")
            continue
        user, task = built
        try:
            cs = call(system_prompt, user)
        except Exception as ex:  # noqa: BLE001
            _log(log_path, {"card_id": cid, "tier": args.tier, "stage": "call", "ok": False, "error": str(ex)})
            print(f"  {cid}: CALL-FAIL {ex}")
            continue

        (out_dir / f"{cid}.cs").write_text(cs, encoding="utf-8")
        record = {"card_id": cid, "tier": args.tier, "shape": t["shape"], "chars": len(cs)}

        if not args.no_compile:
            ok, detail = compile_gate(cs, cid, task["target_path"].split("CardEffect/")[-1], out_dir)
            record["g1_compile"] = ok
            record["g1_detail"] = detail
            passed += int(ok)
            print(f"  {cid}: compile={'PASS' if ok else 'FAIL'}")
        _log(log_path, record)

    if not args.no_compile:
        print(f"\nG1 컴파일 통과: {passed}/{len(targets)} (tier={args.tier})")
    print(f"산출: {out_dir}")


def _log(path: Path, record: dict) -> None:
    with open(path, "a", encoding="utf-8") as f:
        f.write(json.dumps(record, ensure_ascii=False) + "\n")


if __name__ == "__main__":
    main()
