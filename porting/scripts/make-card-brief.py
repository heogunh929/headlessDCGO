#!/usr/bin/env python3
"""카드별 포팅 브리핑 생성기 (pipeline-v2: 사전 추출).

원본 DCGO 카드가 참조하는 심볼을 정규식으로 전수 추출하고,
PRIMITIVE-CATALOG / PORTING-RECIPE(의도표) / EXPRESSION-MAP 에서
해당 행만 기계적으로 조회해 카드당 3~6KB 브리핑을 만든다.

로컬 porter 는 90KB 카탈로그 대신 이 브리핑만 읽는다 — 조회·의도매핑·
표현치환 판단이 전부 여기서 끝나 있으므로 porter 는 순수 번역만 한다.
브리핑의 "미해결" 섹션에 있는 심볼만 STOP 대상이다(브리핑에 시그니처가
있는 심볼을 STOP 하는 것은 위반).

사용:
  python3 porting/scripts/make-card-brief.py BT1 Red            # SET+COLOR 전체
  python3 porting/scripts/make-card-brief.py BT1 Red BT1_011    # 특정 카드만
출력: porting/briefs/<SET>.<COLOR>/<ID>.md
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]    # repo root (porting/scripts/ -> ..)
PORTING = Path(__file__).resolve().parents[1]  # porting/
CATALOG = PORTING / "docs/PRIMITIVE-CATALOG.md"
RECIPE = PORTING / "docs/PORTING-RECIPE.md"
EXPR_MAP = PORTING / "docs/EXPRESSION-MAP.md"
DCGO_CARDS = REPO / "DCGO/Assets/Scripts/CardEffect"
BRIEF_OUT = PORTING / "briefs"

# 레시피가 명시한 진성 STOP 표면 (강모델 전용 큐)
KNOWN_STOP = {
    "AddSkillClass": "효과 동적 부여 — 강모델 전용",
    "PlayOptionCards": "레시피 STOP-목록",
    "AddMaxTrashCountDigiXrosClass": "DigiXros 트래시-보정 — 강모델 전용",
    "DNADigivolveWithHandOrTrashCardIntoHandOrTrash": "W6 예외 STOP",
    "ChangeEndTurnMinMemoryClass": "중첩 커스텀 coroutine — 강모델 전용",
    "AddSelfLinkConditionStaticEffect": "대체 링크원 — 강모델 전용",
}

# new X( 에서 무시할 일반 C#/원본 배관 타입
CTOR_NOISE = {
    "List", "Dictionary", "HashSet", "Hashtable", "Func", "Action",
    "WaitForSeconds", "WaitUntil", "Vector2", "Vector3", "GameObject",
    "ActivateClass",  # 코루틴 빌더 자체 — 의도표가 다룸
}

# 의도표가 전담하는 코루틴 클래스 — 미해결이 아니라 의도표 행이 답이다
INTENT_ONLY_CTORS = {
    "DrawClass", "SuspendPermanentsClass", "DestroyPermanentsClass",
    "IgnoreColorConditionClass", "CanNotSuspendClass", "CanNotBeDestroyedClass",
    "ChangeCostClass", "AddDigiXrosConditionClass", "AddJogressConditionClass",
    "AddAssemblyConditionClass", "MindLinkClass", "CanNotAffectedClass",
    "ChangeCardNamesClass",
}

GENERIC_WORDS = {
    "CardEffectFactory", "CardEffectCommons", "EffectTiming", "CardSource",
    "ICardEffect", "Permanent", "SetUp", "SetIsLinkedEffect", "Count",
    "Contains", "owner", "card", "timing", "coroutine", "condition",
    "EffectDuration", "PermanentEffectFactory", "UntilEachTurnEndEffects",
}


def parse_master_table(lines: list[str], header_prefix: str) -> dict[str, list[str]]:
    """'| `Name` | ret | `sig` |' 형식 표를 이름 → 행 목록으로."""
    out: dict[str, list[str]] = {}
    in_section = False
    for line in lines:
        if line.startswith("## "):
            in_section = line.startswith(header_prefix)
            continue
        if in_section:
            m = re.match(r"\|\s*`(\w+)`\s*\|", line)
            if m:
                out.setdefault(m.group(1), []).append(line.rstrip())
    return out


def parse_category_descs(lines: list[str]) -> dict[str, str]:
    """빠른참조/클래스표면의 '- **Name** — desc' 불릿을 이름 → 불릿 전문으로."""
    out: dict[str, str] = {}
    for line in lines:
        m = re.match(r"- \*\*([\w/ ]+)\*\*", line)
        if m:
            for name in re.split(r"[/ ]+", m.group(1)):
                if name and name not in out:
                    out[name] = line.rstrip()
    return out


def table_rows_with_triggers(md: str, begin_re: str) -> list[tuple[set[str], str]]:
    """표의 각 행에서 (트리거 식별자 집합, 행 전문) 추출. begin_re 이후 첫 표."""
    lines = md.splitlines()
    rows: list[tuple[set[str], str]] = []
    started = False
    in_table = False
    for line in lines:
        if not started:
            if re.search(begin_re, line):
                started = True
            continue
        if line.startswith("|"):
            in_table = True
            first_cell = line.split("|")[1] if line.count("|") >= 2 else ""
            idents = {
                w for w in re.findall(r"[A-Za-z_]\w{4,}", first_cell)
                if w not in GENERIC_WORDS
            }
            if idents:
                rows.append((idents, line.rstrip()))
        elif in_table and line.strip() and not line.startswith(">"):
            break  # 표 종료
    return rows


def extract_card_symbols(src: str) -> dict[str, list[str]]:
    return {
        "factory": sorted(set(re.findall(r"CardEffectFactory\.(\w+)\s*\(", src))),
        "commons": sorted(set(re.findall(r"CardEffectCommons\.(\w+)\s*[\(<]", src))),
        "ctor": sorted(
            {
                n
                for n in re.findall(r"\bnew\s+([A-Z]\w+)\s*[\(<]", src)
                if n not in CTOR_NOISE
            }
        ),
        "timings": sorted(set(re.findall(r"EffectTiming\.(\w+)", src))),
    }


MIRROR_TEMPLATE = """\
using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.{set}.{color};

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class {card_id} : CEntity_Effect
{{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {{
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // ... 원본과 같은 EffectTiming 분기 ...

        return cardEffects;
    }}
}}"""


def build_brief(
    card_id: str,
    src: str,
    factory_master: dict[str, list[str]],
    commons_master: dict[str, list[str]],
    descs: dict[str, str],
    intent_rows: list[tuple[set[str], str]],
    expr_rows: list[tuple[set[str], str]],
    original_rel: str,
    card_set: str,
    color: str,
) -> str:
    syms = extract_card_symbols(src)
    found: list[str] = []
    unresolved: list[str] = []
    hard_stop: list[str] = []

    def lookup(name: str, kind: str) -> None:
        if name in KNOWN_STOP:
            hard_stop.append(f"- `{name}` — {KNOWN_STOP[name]}")
            return
        if name in INTENT_ONLY_CTORS:
            return  # 의도→팩토리 표 행이 답 — 조회 대상 아님
        rows = factory_master.get(name) or commons_master.get(name)
        if rows:
            head = f"**`{name}`** ({kind})"
            if name in descs:
                found.append(f"{head}\n{descs[name]}")
            else:
                found.append(head)
            found.extend(rows)
            found.append("")
        elif name in descs:
            found.append(f"**`{name}`** ({kind}) — 클래스 직접 생성/특수 표면:")
            found.append(descs[name])
            found.append("")
        else:
            unresolved.append(f"- `{name}` ({kind})")

    for n in syms["factory"]:
        lookup(n, "CardEffectFactory")
    for n in syms["commons"]:
        lookup(n, "CardEffectCommons")
    for n in syms["ctor"]:
        lookup(n, "new 클래스")

    matched_intents = [row for trig, row in intent_rows if any(t in src for t in trig)]
    matched_exprs = [row for trig, row in expr_rows if any(t in src for t in trig)]
    has_coroutine = bool(re.search(r"IEnumerator|\.Draw\(\)|\.Tap\(\)|\.Destroy\(\)", src))

    p: list[str] = []
    p.append(f"# 포팅 브리핑 — {card_id}")
    p.append("")
    p.append(f"- 원본: `{original_rel}`")
    p.append(f"- 원본 타이밍 분기: {', '.join('`'+t+'`' for t in syms['timings']) or '(없음)'}")
    p.append(f"- 코루틴 존재: {'예 — 아래 의도→팩토리 표로 번역(구문 미러 금지)' if has_coroutine else '아니오'}")
    p.append("")
    p.append("> **규칙**: 이 브리핑에 시그니처/매핑이 있는 심볼은 STOP 금지 — 그대로 사용한다.")
    p.append("> STOP 은 §미해결 심볼에 있는 것만 가능하다. 여기 없는 표현·팩토리를 발명하지 말 것.")
    p.append("")
    p.append("## 미러 뼈대 (이 틀을 그대로 사용 — `namespace` 선언 필수)")
    p.append("")
    p.append("```csharp")
    p.append(MIRROR_TEMPLATE.format(set=card_set, color=color, card_id=card_id))
    p.append("```")
    p.append("")
    p.append("> `namespace` 선언이 빠지면 게이트가 카드를 발견하지 못해 **FAIL** 처리된다.")
    p.append("> 시그니처는 `override IReadOnlyList<ICardEffect>` 그대로, 클래스는 `public sealed`.")
    p.append("> STOP 분기가 실제로 하나도 없으면 `porting/stop/` 에는 **아무것도 기록하지 않는다**.")
    p.append("")
    if found:
        p.append("## 심볼 조회 결과 (헤드리스에 존재 — 그대로 사용)")
        p.append("")
        p.extend(found)
    if matched_intents:
        p.append("## 코루틴 의도 → 팩토리 매핑 (해당 행만 발췌)")
        p.append("")
        p.append("| 원본 코루틴 의도 | 헤드리스 팩토리 |")
        p.append("|---|---|")
        p.extend(matched_intents)
        p.append("")
    if matched_exprs:
        p.append("## condition/술어 표현 치환 (해당 행만 발췌)")
        p.append("")
        p.append("| 원본 표현 | 헤드리스 표현 | 비고 |")
        p.append("|---|---|---|")
        p.extend(matched_exprs)
        p.append("")
    if hard_stop:
        p.append("## 확정 STOP (강모델 큐 — 이 분기는 `// STOP` 주석만)")
        p.append("")
        p.extend(hard_stop)
        p.append("")
    if unresolved:
        p.append("## 미해결 심볼 (자동조회 실패 — STOP 후보)")
        p.append("")
        p.append("> 아래 심볼은 카탈로그 양쪽 표에서 자동조회에 실패했다. 브리핑 생성기의")
        p.append("> 한계일 수 있으므로, 사용 분기만 `// STOP: <심볼> — 강모델` 처리하고")
        p.append("> `porting/stop/`에 기록한다.")
        p.append("")
        p.extend(unresolved)
        p.append("")
    return "\n".join(p) + "\n"


def main() -> int:
    if len(sys.argv) < 3:
        print(__doc__)
        return 1
    card_set, color = sys.argv[1], sys.argv[2]
    only = set(sys.argv[3:])

    catalog_lines = CATALOG.read_text(encoding="utf-8").splitlines()
    factory_master = parse_master_table(catalog_lines, "## 알파벳 마스터")
    commons_master = parse_master_table(catalog_lines, "## CardEffectCommons 헬퍼 마스터")
    descs = parse_category_descs(catalog_lines)
    recipe_md = RECIPE.read_text(encoding="utf-8")
    intent_rows = table_rows_with_triggers(recipe_md, r"의도→팩토리 번역")
    expr_md = EXPR_MAP.read_text(encoding="utf-8")
    expr_rows = table_rows_with_triggers(expr_md, r"^## 1\.") + table_rows_with_triggers(
        expr_md, r"^## 2\."
    )

    src_dir = DCGO_CARDS / card_set / color
    if not src_dir.is_dir():
        print(f"원본 폴더 없음: {src_dir}", file=sys.stderr)
        return 1
    out_dir = BRIEF_OUT / f"{card_set}.{color}"
    out_dir.mkdir(parents=True, exist_ok=True)

    count = 0
    for f in sorted(src_dir.glob("*.cs")):
        card_id = f.stem
        if only and card_id not in only:
            continue
        src = f.read_text(encoding="utf-8", errors="replace")
        brief = build_brief(
            card_id, src, factory_master, commons_master, descs,
            intent_rows, expr_rows, str(f.relative_to(REPO)),
            card_set, color,
        )
        out = out_dir / f"{card_id}.md"
        out.write_text(brief, encoding="utf-8")
        size = out.stat().st_size
        print(f"{card_id}: {size:5d}B -> {out.relative_to(REPO)}")
        count += 1
    print(f"\n{count}개 브리핑 생성 완료: {out_dir.relative_to(REPO)}/")
    return 0


if __name__ == "__main__":
    sys.exit(main())
