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
    "draw":        {"factory": "DrawCardsEffect", "note": "draw N"},
    "delete":      {"factory": "SelectAndDestroyEffect", "note": "select and delete a Digimon (restrict via canTarget predicate)"},
    "trash":       {"factory": "SelectAndTrashFromZoneEffect",
                    "also": ["SelectAndTrashDigivolutionEffect"], "note": "select and trash from a zone / trash digivolution sources"},
    "to_hand":     {"factory": "SelectAndAddToHandFromZoneEffect",
                    "also": ["SelectAndBounceEffect", "AddThisCardToHandEffect"],
                    "note": "from a zone to hand / bounce a field permanent to hand / this card to hand"},
    "bounce":      {"factory": "SelectAndBounceEffect",
                    "also": ["SelectAndReturnToDeckEffect"], "note": "bounce to hand / to deck (toTop bool)"},
    "security":    {"factory": "SelectAndPutSecurityEffect",
                    "also": ["ReplaceBottomSecurityWithFaceUpOptionEffect"], "note": "put to security"},
    "memory":      {"factory": "GainMemoryActivatedEffect", "note": "gain/lose memory (+/-)"},
    "digivolve":   {"factory": "SelectAndDigivolveEffect",
                    "also": ["BlastDigivolveEffect", "BurstDigivolveEffect", "ArtsDigivolveEffect"],
                    "note": "select and digivolve / Blast/Burst/Arts special digivolve"},
    "deenergize":  {"factory": "SelectAndDeDigivolveEffect",
                    "also": ["SelectAndTrashDigivolutionEffect"], "note": "trash N digivolution sources (de-digivolve)"},
    "suspend":     {"factory": "SelectAndSuspendEffect", "note": "select and suspend (tap)"},
    "unsuspend":   {"factory": "SelectAndUnsuspendEffect", "note": "select and unsuspend (untap)"},
    "dp_plus":     {"factory": "SelectAndBuffDpEffect",
                    "also": ["PlayerScopeBuffDpEffect", "ChangeSelfDPStaticEffect"], "note": "DP +N (target/scope/self)"},
    "dp_minus":    {"factory": "SelectAndBuffDpEffect",
                    "also": ["ChangeDPStaticEffect"], "note": "DP -N (negative amount)"},
    "recovery":    {"factory": "RecoveryTriggerEffect", "note": "security recovery"},
    "blocker":     {"factory": "BlockerStaticEffect",
                    "also": ["BlockerSelfStaticEffect"], "note": "grant Blocker (scope/self)"},
    "piercing":    {"factory": "PiercingStaticEffect",
                    "also": ["PierceSelfEffect"], "note": "grant Piercing (scope/self)"},
    "play":        {"factory": "SelectAndPlayFromZoneEffect",
                    "also": ["PlayOptionCardEffect"], "note": "select and play from a zone / option play"},
    # Tags that are not a single factory (not an action) — marked with kind.
    "cannot":      {"kind": "restriction-family",
                    "prefix": "CanNot*StaticEffect",
                    "note": "pick by what is forbidden: CanNotAttackStaticEffect / CanNotBeDestroyedStaticEffect / "
                            "CanNotAddSecurityStaticEffect / CanNotDigivolveStaticEffect / CanNotBlockStaticEffect etc."},
    "once_per_turn": {"kind": "modifier",
                      "note": "not an action = [Once Per Turn] cap. Multi-fire timings (e.g. on-unsuspend) are "
                              "auto-capped by the bridge via OnceFlags (cheatsheet section 7 v3) — no explicit marker needed. "
                              "Otherwise gate the trigger as once."},
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
