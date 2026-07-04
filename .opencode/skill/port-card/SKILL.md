---
name: port-card
description: Port one Digimon card 1:1 from the read-only DCGO/ original into the headless C# engine. Use whenever the task is to port, mirror, or implement a card effect by card id (e.g. ST1_06, EX8_074, BT1_010). Enforces AS-IS 1:1 fidelity, forbids inventing primitives, and STOPs to escalate anything not in the catalog.
---

# Card Porting (AS-IS 1:1)

You port **one** Digimon card at a time from the original `DCGO/` sources into the headless engine. The originals in `DCGO/` are the ground truth. Your job is **translation, not design**.

## Iron rules (never break)

1. **STOP beats guessing.** If you are not certain, STOP. Never invent a factory, never invent a timing, never bridge/simplify to "make it work". A missing primitive is an escalation, not a task. When in doubt → STOP.
2. **AS-IS 1:1 mirror.** The headless card file must have the *same* timing branches and the *same* `CardEffectFactory.<Name>(...)` calls as the original. Same names, same argument order. Do not change or simplify logic. Do not rationalize an omission by "it's rare".
3. **No primitive development.** Every factory the original calls already exists in the catalog. If it is not in the catalog → STOP (do not create it).
4. **`DCGO/` is read-only.** Never edit or commit `DCGO/`, `bin/`, or `obj/`.
5. **Never commit or push.** The user commits.

## Procedure (one card = one cycle)

1. **Read the original.** `DCGO/Assets/Scripts/CardEffect/<SET>/<COLOR>/<ID>.cs`. Determine SET/COLOR from the id and the original path. Read every `if (timing == EffectTiming.X)` branch and the `CardEffectFactory.<Method>(...)` call inside it.
2. **Confirm the rule text (AS-IS).** Cross-check what the card does against the original rule text — do not guess intent from the method name alone.
3. **Look up each factory in the catalog.** Find `<Method>` in `docs/porting/PRIMITIVE-CATALOG.md`.
   - **Look up by METHOD NAME, ignoring the class prefix.** The original may call `CardEffectCommons.X(...)` where headless has `CardEffectFactory.X(...)` (or vice versa) — a prefix mismatch is NOT "not in the catalog". Search both the factory table and the "CardEffectCommons 헬퍼 마스터" table.
   - Found → match the headless signature (same name is the norm), fill the arguments.
   - Not found in either table → **STOP** for that branch (see §Escalation).
4. **Translate coroutine effects by intent, not syntax.** Original active/trigger effects are coroutine builders (`IEnumerator`, `.Draw()`, `.Tap()`, `.Destroy()`). Do NOT copy them line-by-line. Read the intent and pick the mapped factory from the recipe's intent→factory table. **This applies to every call INSIDE a coroutine too** — `new DrawClass(...).Draw()` → `DrawCardsEffect`, `owner.AddMemory(±N)` → `AddMemoryTriggerEffect` (both ARE in the catalog); check the intent table + catalog BEFORE declaring STOP. If no mapping exists → STOP.
5. **Translate condition-lambda expressions via the expression map.** Every member access inside a `condition`/predicate body (`.HasPierce`, `.MemoryForPlayer`, `CardColor.X`, zone lists...) must be looked up in `docs/porting/EXPRESSION-MAP.md` and substituted per that table. Most entries exist under the same name; a few change shape (keyword reads → `CardEffectCommons.HasKeyword/HasPierce(card)`, colors are strings — `HasCardColor("Red")`). An expression not in the map that you cannot confirm compiles → STOP for that branch. Never invent an expression.
6. **Write the mirror file.** `src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/<SET>/<COLOR>/<ID>.cs`. If a skeleton (`// TODO: Skeleton only`) exists, replace it with the ported body. **Do NOT write any test** — the gate is automatic (step 8).
7. **Structural self-check (instead of writing tests).** Verify against the original: every `EffectTiming` branch is either mapped to a catalog factory call or `// STOP`-commented; every factory/helper method name and argument count exists in the catalog (either table); every condition-lambda member access is same-name-confirmed or substituted per the expression map; no invented factory or expression, no flattened predicate. If any check fails, that branch/card is a STOP.
8. **Gate.** Run `bash scripts/run-tests.sh CardEffect.Binding.Auto` and confirm `FAIL=0`. This builds the engine (catching wrong factory names / arg counts / non-compiling branches) and auto-verifies that every non-stub mirror actually registers an effect binding (i.e. is live, not inert). Do not write per-card tests. Do not declare done until green. If FAIL, re-diff the original against your mirror (missing branch / wrong argument / unmapped expression).

## Partial porting (per branch)

A card is independent per timing branch. If one branch maps and another must STOP, **port the mapping branches and leave a STOP comment on the others** — do not STOP the whole card.

```csharp
// STOP: <reason> — 강모델
```
Omit the `cardEffects.Add(...)` for the STOP branch. The card compiles and partially works; record the id + STOP branch for the strong model.

**Record every STOP** (whole-card or per-branch) by appending to `docs/porting/stop/<SET>.<COLOR>.md` as `<ID> | reason | original symbol`. This file is the strong model's (강모델) work queue for primitive pre-development.

## Escalation (STOP conditions — record, do not solve)

STOP and record `<ID> | reason | original symbol` when any of:
- The factory the original calls is **not in the catalog**.
- The `EffectTiming` the original uses **does not exist** in headless (compile error) — do not add timings.
- The original uses a **nested custom class / custom coroutine logic** (not a plain factory call).
- A **special-play recipe** (DigiXros / DNA / Blast / Jogress data) is required and not expressible via a catalog factory.

## References (read before writing)

- Full procedure, templates, worked example, and the coroutine intent→factory mapping table: `docs/porting/PORTING-RECIPE.md`
- Every available primitive with its exact signature (factory table + CardEffectCommons helper table): `docs/porting/PRIMITIVE-CATALOG.md`
- Condition-lambda expression substitutions (original member access → headless idiom): `docs/porting/EXPRESSION-MAP.md`

Read all three, read the original, then write the mirror (no test), run the structural self-check, then run the gate to green.
