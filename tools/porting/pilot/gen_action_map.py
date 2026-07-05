#!/usr/bin/env python3
"""Build the action_tag -> canonical headless factory map.

The card_ir DB tags every card with a small, HIGHLY-SHARED action vocabulary (19 tags;
83% of cards reuse a combination). Card-level references do NOT generalise (60% of cards
have a unique signature), but the ACTION does — so this map is the generalising unit:
each tag -> the real factory/pattern that implements it (every factory verified against
allowlist.json, so nothing is invented). Signatures are pulled from the allowlist so the
map stays in sync. Feeds the branch packet + cheatsheet §10 so a card can be ported from
its own extracted actions WITHOUT needing a similar-effect reference.

Usage: python3 tools/porting/pilot/gen_action_map.py   # writes action_map.json
"""
from __future__ import annotations

import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
ALLOWLIST = HERE / "allowlist.json"
OUT = HERE / "action_map.json"

# Curated tag -> canonical factory(ies). Every factory name is verified present in allowlist.json.
# `factory` = the primary "do this action" call; `also` = common variants; `kind` marks non-factory tags.
CURATED: dict[str, dict] = {
    "draw":        {"factory": "DrawCardsEffect", "note": "N장 드로우"},
    "delete":      {"factory": "SelectAndDestroyEffect", "note": "디지몬 골라 삭제(canTarget 술어로 대상 제한)"},
    "trash":       {"factory": "SelectAndTrashFromZoneEffect",
                    "also": ["SelectAndTrashDigivolutionEffect"], "note": "존에서 골라 trash / 진화원 trash"},
    "to_hand":     {"factory": "SelectAndAddToHandFromZoneEffect",
                    "also": ["SelectAndBounceEffect", "AddThisCardToHandEffect"],
                    "note": "존에서 손패로 / 필드 permanent를 손패로 바운스 / 자기 손패로"},
    "bounce":      {"factory": "SelectAndBounceEffect",
                    "also": ["SelectAndReturnToDeckEffect"], "note": "손패로 바운스 / 덱으로(toTop bool)"},
    "security":    {"factory": "SelectAndPutSecurityEffect",
                    "also": ["ReplaceBottomSecurityWithFaceUpOptionEffect"], "note": "시큐리티에 놓기"},
    "memory":      {"factory": "GainMemoryActivatedEffect", "note": "메모리 증감(+/-)"},
    "digivolve":   {"factory": "SelectAndDigivolveEffect",
                    "also": ["BlastDigivolveEffect", "BurstDigivolveEffect", "ArtsDigivolveEffect"],
                    "note": "골라 진화 / Blast·Burst·Arts 특수진화"},
    "deenergize":  {"factory": "SelectAndDeDigivolveEffect",
                    "also": ["SelectAndTrashDigivolutionEffect"], "note": "진화원 N장 trash(디에너자이즈)"},
    "suspend":     {"factory": "SelectAndSuspendEffect", "note": "골라 서스펜드(탭)"},
    "unsuspend":   {"factory": "SelectAndUnsuspendEffect", "note": "골라 언서스펜드(언탭)"},
    "dp_plus":     {"factory": "SelectAndBuffDpEffect",
                    "also": ["PlayerScopeBuffDpEffect", "ChangeSelfDPStaticEffect"], "note": "DP +N (대상/스코프/자기)"},
    "dp_minus":    {"factory": "SelectAndBuffDpEffect",
                    "also": ["ChangeDPStaticEffect"], "note": "DP -N (음수 amount)"},
    "recovery":    {"factory": "RecoveryTriggerEffect", "note": "시큐리티 회복"},
    "blocker":     {"factory": "BlockerStaticEffect",
                    "also": ["BlockerSelfStaticEffect"], "note": "Blocker 부여(스코프/자기)"},
    "piercing":    {"factory": "PiercingStaticEffect",
                    "also": ["PierceSelfEffect"], "note": "Piercing 부여(스코프/자기)"},
    "play":        {"factory": "SelectAndPlayFromZoneEffect",
                    "also": ["PlayOptionCardEffect"], "note": "존에서 골라 플레이 / 옵션 플레이"},
    # 팩토리 하나로 안 떨어지는 태그(액션이 아님) — kind로 표시.
    "cannot":      {"kind": "restriction-family",
                    "prefix": "CanNot*StaticEffect",
                    "note": "무엇을 금지하는지로 선택: CanNotAttackStaticEffect / CanNotBeDestroyedStaticEffect / "
                            "CanNotAddSecurityStaticEffect / CanNotDigivolveStaticEffect / CanNotBlockStaticEffect 등"},
    "once_per_turn": {"kind": "modifier",
                      "note": "액션이 아니라 [Once Per Turn] 캡. 온-언서스펜드 등 다중발화 타이밍은 브릿지가 "
                              "OnceFlags로 자동 캡(cheatsheet §7 v3) — 별도 표시 불필요. 그 외엔 트리거를 once로 게이트."},
}


def main() -> None:
    allow = json.loads(ALLOWLIST.read_text(encoding="utf-8"))
    sigs = allow.get("factory_signatures", {})
    facset = set(allow.get("CardEffectFactory", []))

    out: dict[str, dict] = {}
    missing: list[str] = []
    for tag, spec in CURATED.items():
        entry = dict(spec)
        for key in ("factory", *([] if "also" not in spec else ["_also_check"])):
            pass
        fac = spec.get("factory")
        if fac:
            if fac not in facset:
                missing.append(f"{tag}->{fac}")
            entry["sig"] = sigs.get(fac, "")
        # verify `also` factories too
        for f in spec.get("also", []):
            if f not in facset:
                missing.append(f"{tag}->also:{f}")
        out[tag] = entry

    OUT.write_text(json.dumps(out, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"action_map -> {OUT}  ({len(out)} tags)")
    if missing:
        print("⚠ allowlist에 없는 팩토리(오타?):", missing)
    else:
        print("✓ 모든 팩토리가 allowlist에 실재(발명 0)")


if __name__ == "__main__":
    main()
