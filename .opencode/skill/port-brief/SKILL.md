---
name: port-brief
description: Port one Digimon card 1:1 from the read-only DCGO/ original into the headless C# engine, driven by the card's pre-extracted brief (porting/briefs/<SET>.<COLOR>/<ID>.md) instead of the 90KB catalog. Use for brief-wired local-model porting. Same AS-IS 1:1 fidelity and STOP contract as port-card, but the lookup is already done in the brief.
---

# Card Porting from a Brief (AS-IS 1:1, pipeline-v2)

You port **one** card from the original `DCGO/` source into the headless engine.
Your input is the card's **brief** — `porting/briefs/<SET>.<COLOR>/<ID>.md` — which
already extracted every symbol this card touches and looked it up for you. You do **not**
read the 90KB `PRIMITIVE-CATALOG.md`; the brief is the catalog slice for this card.
Your job is **translation, not design, not lookup**.

## Iron rules (never break)

1. **STOP beats guessing.** Not certain? STOP. Never invent a factory, a timing, or a bridge to "make it work".
2. **AS-IS 1:1 mirror.** Same timing branches, same `CardEffectFactory.<Name>(...)` calls, same argument order as the original. No simplification. Do not rationalize an omission by "it's rare".
3. **No primitive development.** Every factory this card needs is either in the brief (use it) or in the brief's 미해결 section (STOP it). Never create one.
4. **`DCGO/` is read-only.** Never edit or commit `DCGO/`, `bin/`, or `obj/`.
5. **Never commit or push.** The user commits.

## The brief is the source of truth for lookups

The brief has four sections. Three tell you the answer; only one is a STOP:

- **`## 심볼 조회 결과 (헤드리스에 존재 — 그대로 사용)`** — resolved symbols with their exact
  headless signature. **Use them verbatim. STOP on any symbol in here is a VIOLATION.**
- **`## 코루틴 의도 → 팩토리 매핑 (해당 행만 발췌)`** — coroutine intent → headless factory.
  Translate coroutine effects by this table, not line-by-line syntax.
- **`## condition/술어 표현 치환 (해당 행만 발췌)`** — member-access substitutions for
  `condition`/predicate bodies. Substitute per this table.
- **`## 미해결 심볼 (자동조회 실패 — STOP 후보)`** — the **only** STOP-eligible symbols.
  Auto-lookup failed for these. STOP the branch that uses one and record it.

If a symbol is not in any brief section and you cannot confirm it compiles → treat it like a
미해결 symbol (STOP the branch). Never invent.

## Procedure (one card = one cycle)

1. **Read the brief** `porting/briefs/<SET>.<COLOR>/<ID>.md`. If it is missing, stop and
   report — the `port-card-brief` command / `porting/scripts/port-batch.sh` generates it first. Read
   the original `DCGO/Assets/Scripts/CardEffect/<SET>/<COLOR>/<ID>.cs` for the exact timing branches.
2. **Map each `EffectTiming` branch** using the brief's resolved-symbol / intent / expression
   tables. Fill arguments 1:1 from the original.
3. **Write the mirror** `src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/<SET>/<COLOR>/<ID>.cs`,
   replacing the `// TODO: Skeleton only` stub, **using the brief's `## 미러 뼈대` skeleton
   verbatim** — the `namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.<SET>.<COLOR>;`
   declaration is MANDATORY (without it the gate cannot discover the card and FAILs), the
   signature is `public override IReadOnlyList<ICardEffect> CardEffects(...)`, the class is
   `public sealed`. **Write no test** — the gate is automatic.
4. **Partial porting per branch.** If one branch uses a 미해결 symbol, port the resolvable
   branches and leave `// STOP: <symbol> — 강모델` on the others (omit their `cardEffects.Add`).
   Do not STOP the whole card.
5. **Record every STOP** by appending to `porting/stop/<SET>.<COLOR>.md` as
   `<ID> | reason | original symbol`. **If no branch was STOPped, write NOTHING to the stop
   log** — a fully ported card must leave no stop entry.
6. **Structural self-check.** Every original `EffectTiming` branch is either mapped (from a
   brief-resolved symbol) or `// STOP`-commented; every call name+arity matches the brief;
   no invented factory/expression; no flattened predicate.
7. **Gate.** Run `bash scripts/run-tests.sh CardEffect.Binding.Auto` and confirm `FAIL=0`
   (builds the engine + auto-verifies the mirror registers a live binding). Do not declare
   done until green. If FAIL, re-diff the original against your mirror.

## Escalation (STOP conditions — record, do not solve)

STOP and record `<ID> | reason | original symbol` when:
- The symbol is in the brief's **미해결** section (auto-lookup failed).
- The original uses an `EffectTiming` that does not exist in headless (compile error).
- The original uses a **nested custom class / custom coroutine logic** not covered by any brief table.
- A **special-play recipe** (DigiXros / DNA / Blast / Jogress) is required and no brief mapping expresses it.

Read the brief, read the original, write the mirror (no test), self-check, gate to green.
