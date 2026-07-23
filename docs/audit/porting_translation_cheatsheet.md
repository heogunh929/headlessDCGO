# Porting Translation Cheatsheet — AS-IS (Unity) domain patterns → Headless (.NET)

> ## ⚠️ SUPERSEDED (2026-07-23) — DO NOT inject this cheatsheet into any porting prompt
>
> **Its foundational rule is now REVERSED.** Section 0 tells you to lower the AS-IS `Func<Permanent,bool>`
> predicate into a `Func<HeadlessEntityId,bool>` and route every state read through `CardEffectCommons.X(card, id)`.
> The current canonical idiom does the **opposite**: ported cards keep the AS-IS **`Func<Permanent,bool>`**
> predicate and read the enriched `Permanent` domain object directly — e.g. `permanent.TopCard.CardNames.Contains(...)`,
> `permanent.IsSuspended`, `permanent.Level`. Verified in current src: `BT6_106`, `BT16_033`, `ST1_12`
> (2026-07-22/23 ports). A weak model following section 0 would **strip the AS-IS `Permanent` mirror** — the same
> class of failure as reproducing a deleted invented symbol.
>
> Also stale: the "auto-processing bridge" model (sections 7–8) is replaced by the **uniform `ActivateClass`**
> path (`SetUpICardEffect`/`SetUpActivateClass`, `CanUseCondition(Hashtable)` trigger gate) + window supply. The
> registry pipeline this cheatsheet assumes (`ToBinding`/`EffectRegistry`/gates) was physically deleted at the
> 2026-07-23 soft freeze (freeze_evidence_2026-07-23.md §1: invented-symbol grep = 0). The factory-name/signature
> tables below are **unverified against current src** — never copy a name from here; verify against the actual
> factory source (`CardEffectFactory.cs` and siblings).
>
> **Read instead:** `card_porting_standard.md` (revised, see its status banner), `coverage_exemplar_audit_2026-07-18.md`,
> and the live exemplar cards named above. What survives here as still-true *principle* (not mechanism): evaluate
> predicates faithfully / never blur them (fidelity-over-coverage), and never invent factory names or argument lists.
> Everything below is historical record only.

- Written: 2026-07-05. Basis: full analysis of CS1061 (domain-member hallucination) in failed cards from the pilot (BT1 exact).
- Purpose: codify the **semantic translation layer** that the symbol surface (factory/commons signatures) alone does not catch. The pilot confirmed:
  pass rate climbs to ~50% from the symbol surface, and everything beyond that is this layer. This cheatsheet is the only lever that opens that wall.
- Usage: inject these rules as an appendix to the symbol surface in `tools/porting/pilot/port_with_sonnet.py` (auto-loaded). A living
  document — when new patterns emerge from pilot failures, add them here and re-measure.

## 0. Core structural rule (most important)

The AS-IS condition predicate is a **`Func<Permanent, bool>`** and reads state via `permanent.property`.
The headless condition predicate is a **`Func<HeadlessEntityId, bool>`** and reads via `CardEffectCommons.predicate(card, id)`.

```
// AS-IS
bool CanSelect(Permanent permanent) => permanent.HasNoDigivolutionCards && permanent.IsDigimon;
// Headless (take id, query via commons)
bool CanSelect(HeadlessEntityId id) =>
    CardEffectCommons.HasNoDigivolutionCards(card, id) && CardEffectCommons.IsBattleAreaDigimon(card, id);
```

**Rule: `permanent.X` → `CardEffectCommons.X(card, id)` (a commons predicate of the same name usually exists).**
Domain objects (`PermanentView`/`HeadlessEntityId`/`HeadlessPlayerId`) have no game-state properties
(`HeadlessEntityId`/`HeadlessPlayerId` only have `Value` and `IsEmpty`). Route **all** state queries through commons.

## 1. Permanent property → commons predicate (id-taking form)

| AS-IS (`permanent.X`) | Headless |
|---|---|
| `permanent.HasNoDigivolutionCards` | `CardEffectCommons.HasNoDigivolutionCards(card, id)` |
| `permanent.IsDigimon` | `CardEffectCommons.IsBattleAreaDigimon(card, id)` (is a battle-zone Digimon) |
| `permanent.IsSuspended` | `CardEffectCommons.IsSuspended(card, id)` |
| `permanent` (is it an opponent-owned Digimon) | `CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)` |
| `permanent` (is it an own Digimon) | `CardEffectCommons.IsOwnerBattleAreaDigimon(card, id)` |
| `permanent.Level` (integer level) | `CardEffectCommons.LevelOf(card, id)` → int. Compare with `LevelOf(card,id) >= N`. Use `IsMinLevel`/`IsMaxLevel` for min/max. |
| `permanent.HasPierce` / `HasBlocker` etc. keyword-possession query | **Primitive gap (confirmed)** — headless has no keyword-possession query predicate (`Gain*` is for granting). If needed as a condition, record primitive debt or STOP. |

## 2. Owner/Enemy traversal → scope commons

| AS-IS | Headless |
|---|---|
| `card.Owner.Enemy.GetBattleAreaDigimons().Any(p => COND)` | `CardEffectCommons.HasMatchConditionOpponentsPermanent(card, id => COND(id))` |
| `card.Owner.Enemy...Count(p => COND) >= N` | `CardEffectCommons.MatchConditionPermanentCount(card, id => COND(id)) >= N` (opponent/own scope via COND's commons) |
| `card.Owner.SecurityCards.Count` | `CardEffectCommons.SecurityCount(card)` |
| `card.Owner.MemoryForPlayer >= N` (memory-value condition) | **Primitive gap (confirmed)** — no query commons that reads memory value as a condition. Express effects via `GainMemory*`/`SetMemoryTo` factories. If a condition query is needed, primitive debt. |
| `CardEffectCommons.IsOwnerTurn(card)` / `IsOpponentTurn(card)` | Same (already commons, use as-is) |
| `CardEffectCommons.IsExistOnBattleArea(card)` | Same (self exists in battle zone — active guard) |

## 3. Trigger guards (timing commons)

The AS-IS `CanUseCondition(Hashtable)` becomes a per-timing trigger predicate in headless:

| AS-IS | Headless timing / guard |
|---|---|
| `CanTriggerOnAttack(hashtable, card)` | Timing `OnAllyAttack` + `CardEffectCommons.CanTriggerOnAttack(...)` |
| `CanTriggerWhenDigivolving(hashtable, card)` | Timing `WhenDigivolving` (translated — AS-IS gates on `OnEnterFieldAnyone`) |
| `CanTriggerOptionMainEffect(hashtable, card)` | Timing `OptionSkill`/`SecuritySkill` |

## 4. State-read query gap (2026-07-06 correction — mostly "unknown name", not "absent")

**Correction**: the initial pilot treated residual failures as "query predicate absent (capability gap)", but the BT2 re-measurement (section 9) found on measurement that
**keyword-possession queries already exist** (`ContinuousKeywordGate.HasKeyword`), and trash count/color/owner were all real too. So
the local model **hallucinated because it did not know the real name** — this is mostly a naming/documentation gap → **resolved by the section 9 mapping** (not invention).

**Real capability gaps (strong-model territory, still absent)**:
- **Memory-value condition** (`MemoryForPlayer >= N`): no query predicate that reads memory value as a condition.
- **Main-phase check** (`IsMainPhase`): no card-facing predicate (see section 9 — usually unneeded).

Only these real gaps belong to [primitive_backlog.md]/[fidelity_debt.md] territory (strong-model pre-development, no per-card deferral). The remaining state-read
queries open up when you use the real names in section 9.

## 6. New EFFECT primitive translation (2026-07-05 — full P0 Build Order implemented)

Mirror the AS-IS STOP surface (dynamic/coroutine flows) to headless card-facing factories. **The section 4 gap is the condition-query (state-read) layer, and
the ones below are the effect layer — no longer STOP.** Full signatures = `PRIMITIVE-CATALOG.md` (147 kinds).

| AS-IS pattern | Headless |
|---|---|
| `UserSelectionManager` Set/IntSelection menu (mode selection) | `CardEffectFactory.SelectModeEffect(card, "description", new ModeChoiceEffect.Mode("label", availabilityPredicate_Func<bool>?, branchEffect_ICardEffect), ...)` — if the availability predicate is false, that mode is omitted |
| `SelectCardEffect` to pick from a zone into hand | `CardEffectFactory.SelectAndAddToHandFromZoneEffect(card, fromZone, canTarget, maxCount, canEndNotMax, "description")` |
| …pick to trash / return to deck / security | `SelectAndTrashFromZoneEffect` / `SelectAndReturnToDeckEffect` / `SelectAndPutSecurityEffect` (same signature form) |
| …pick to digivolve | `SelectAndDigivolveEffect(card, fromZone, canTarget, DigivolveCost, ...)` |
| `ChangeCostClass` (BeforePayCost reduction) | `CardEffectFactory.BeforePayCostReductionEffect(card, amount, condition, "description")` — registers deltas for both play and digivolve, applied based on which action the card is. If play-only, gate with `if (timing == EffectTiming.BeforePayCost && CardEffectCommons.IsPayCostRoot(card, PayCostRoot.Play))` |
| `CannotAddSecurityClass.SetUp(PlayerCondition, CardEffectCondition)` | `CanNotAddSecurityStaticEffect(scopePlayer, isInherited, card, condition, causingEffectPredicate)` — **pass CardEffectCondition as causingEffectPredicate** (blurring it over-blocks) |
| `CannotAddMemoryClass` | `CanNotAddMemoryStaticEffect(scopePlayer, isInherited, card, condition, causingEffectPredicate)` |
| `CannotReduceCostClass` | `CanNotReduceCostStaticEffect(permanentCondition, isInherited, card, condition)` |
| `CardEffectCommons.PlayOptionCards` (play an option from a zone) | `CardEffectFactory.PlayOptionCardEffect(card, sourceZone, optionPredicate, maxCount, canEndNotMax, "description")` — auto-resolves the option's [Main], then trash |
| `CardEffectCommons.AddEffectToPlayer(duration, card, effect, timing)` (delayed player effect) | Same name as-is. The nested fires once at timing T, then self-removes (fire-then-clear) |
| `CardEffectCommons.AddEffectToPermanent(perm, duration, card, effect, timing)` | Same name. Build the nested with the **target permanent's CardSource** (source = target). |
| …but grant the target's own `[On Deletion]` (fires when the target is deleted) | `CardEffectCommons.AddSelfRemovalEffectToPermanent(perm, duration, card, nested, timing)` — exempt from leave-play cleanup. The nested self-gates with `triggerGate: rc => rc.EffectContext.TriggerEntityId == target` |
| `AddSkillClass` for "your Digimon gain \<keyword\>" | player-scope `<keyword>StaticEffect(permanentCondition, isInherited, card, condition)` — pass cardSourceCondition as permanentCondition. Live set (cards that enter later also gain it). Piercing/Blitz/Retaliation/Scapegoat/Decoy/Barrier/Alliance/Rush/Reboot/Jamming/Blocker |
| `AddSkillClass` for "your Digimon gain \<triggered effect\>" (BT8_031 kind) | `CardEffectFactory.GrantTriggeredEffectToScopedSet(card, scopePlayer, nested)`. Configure the nested to read `TriggerEntityId` (the card that actually triggered) and apply cardSourceCondition to that card |

## 7. Triggered activated effects (2026-07-05 bridge v1)

At the `[When Attacking]` (OnAllyAttack) and `[On Deletion]` (OnDestroyedAnyone) timings, **actions that need resolution** (draw/trash/
delete/select etc.) are now resolved by the auto-processing bridge if you simply **return the activated factory as-is** at that timing.

```
// [When Attacking] draw 1
if (timing == EffectTiming.OnAllyAttack)
    effects.Add(CardEffectFactory.DrawCardsEffect(card, 1));
// [On Deletion] delete 1 opp Digimon (activated select)
if (timing == EffectTiming.OnDestroyedAnyone)
    effects.Add(CardEffectFactory.SelectAndDestroyEffect(card, canTarget, 1, false, "..."));
```

**v2 addition**: the boundary timings `[End of Turn]` (OnEndTurn), `[Start of Turn]` (OnStartTurn), and `[Start of Main Phase]`
(OnStartMainPhase) are also bridged — since these are turn boundaries with no subject, they scan the entire battle zone, once per turn. `[End of YOUR Turn]` is gated by the card with
`CardEffectCommons.IsOwnerTurn(card)`.

```
if (timing == EffectTiming.OnEndTurn && CardEffectCommons.IsOwnerTurn(card))
    effects.Add(CardEffectFactory.SelectAndDestroyEffect(card, ...));  // [End of Your Turn] delete etc.
```

**v3 addition**: `[on unsuspend]` (OnUnTappedAnyone) — subject-scoped, multiple firings within a turn. The bridge **auto-applies the once-per-turn cap**
(OnceFlags, reset at turn end). So the card only needs to return the activated factory without stating `[Once Per Turn]` (it will not re-fire on
re-unsuspend). memory/DP/recovery/unsuspend still use the existing triggered factories (scheduler-capped) — not bridge targets.

**v4 addition**: `[When Attacking]`'s **attack-declaration timing** `OnDeclaration` is also bridged (subject = attacker, emitted the same way as OnAllyAttack).
The activated effect that AS-IS declares at OnDeclaration (**including the Digi-Burst body** — the BT6_028 kind fires here) is resolved at declaration.
This opens the activated layer of 298 OnDeclaration cards.

Bridge coverage: at **[When Attacking] (OnAllyAttack, OnDeclaration), [On Deletion], [End/Start of Turn], [Start of Main
Phase], [on unsuspend]**, returning a draw/trash/delete/select etc. activated factory at that timing resolves it.

## 8. Special-play primitives (2026-07-05 — all Special Mechanics STOP resolved)

The STOP surface of the DigiXros/DNA/Blast family has all been opened as headless factories. **These mechanisms are no longer STOP.**
Headless special-play is an **auto-match model** (not interactive player selection, but the engine auto-matches materials that satisfy the condition) —
the card only declares the condition (predicate). Full signatures = `PRIMITIVE-CATALOG.md`.

| AS-IS pattern | Headless |
|---|---|
| `AddDigiXrosConditionClass` (basic DigiXros) | `CardEffectFactory.DigiXrosEffect(card, costReduction, new SpecialPlayMaterial(predicate, "label"), ...)` — each material is a predicate matching a battle-zone candidate |
| `AddMaxTrashCountDigiXrosClass` / `maxTamerDigivolutionCardsCount` (materials from trash / under-tamer digivolution source) | `CardEffectFactory.DigiXrosWithExtraMaterialsEffect(card, costReduction, maxTrashCount:Func<CardSource,int>?, maxUnderTamerCount:Func<CardSource,int>?, materials...)` — fill up to N material slots from the trash zone / under-tamer digivolution source (thread the getMaxTrashCount Func as-is) |
| `AddJogressConditionClass` (DNA/Jogress) | `CardEffectFactory.JogressEffect(card, condition, new SpecialPlayMaterial(predicate, "label"), ...)` or the name-based `JogressEffectFromNames(card, condition, "name1", "name2")` |
| `AddJogressLevelsClass` ("this card also counts as level N") | `CardEffectFactory.AddJogressLevelsEffect(card, getLevels:Func<CardSource,IReadOnlyList<int>>)` — getLevels takes the digivolving card (jogressCard) and returns the list of levels this card is additionally treated as. Level-based material predicates are judged with `material.JogressLevelsAgainst(jogressCard).Contains(N)` |
| `BurstDigivolutionCondition` (Burst Digivolve: digivolve on top of target + tamer bounce) | `CardEffectFactory.BurstDigivolveEffect(card, digimonCondition:Func<CardSource,bool>, tamerCondition:Func<CardSource,bool>, cost)` — free digivolve on top of the target Digimon + bounce a matching tamer to hand + cost. The engine auto-matches target and tamer |
| `IDigiBurst` (`[Digi-Burst N] <effect>`: trash N digivolution sources as cost) | `CardEffectFactory.DigiBurstEffect(card, count, innerEffect:ICardEffect, "description")` — trash N of own digivolution sources, then fire innerEffect. If the inner is activated, resolve; **if it is a continuous grant (keyword/stat), register**. Fires only when there are ≥N trashable sources. If returned at a trigger timing (OnDeclaration etc.), the bridge resolves it |
| `DNADigivolveWithHandOrTrashCardIntoHandOrTrash` (effect-driven DNA: digivolve with a hand/trash card) | `CardEffectFactory.DnaDigivolveFromHandOrTrashEffect(card, intoCondition, permanentCondition, materialCondition, intoFromHand:bool, materialFromHand:bool)` — fuse a field permanent + a hand/trash material on top of the into-card (hand/trash). The engine auto-matches |
| `AddAssemblyConditionClass` (Assembly: play with materials from trash) | Already wired — if the card declares `AssemblyConditionOf`, `PlayCardAction` offers an Assembly play with trash materials. No separate factory needed |

**Note**: for the material predicates above (`SpecialPlayMaterial`'s `Func<CardSource,bool>`, `digimonCondition`, `tamerCondition`),
**evaluate the predicate faithfully** per the section 0 rule (do not just do card-name equality; mirror the original conditions like level/color/type). If a Digi-Burst inner is
a **continuous effect** of the "gain keyword" kind, it is not activated, so it is auto-handled by the register path (the card just passes the inner through).

## 9. State-read queries: the real headless names (2026-07-06 BT2 re-measurement hallucination correction)

**These queries already exist in headless — do not invent nonexistent names; use the real names below.** (In the BT2 re-measurement the
local model hallucinated the left column, but all of them are real on the right.) Access state inside a card condition predicate via `card.Context`.

| Invented name (do NOT use) | Real headless |
|---|---|
| `HasReboot(x)` / `HasBlocker(x)` etc. keyword possession | `HeadlessDCGO.Engine.Headless.Runtime.ContinuousKeywordGate.HasKeyword(card.Context, target_InstanceId, "Reboot")` — same for every keyword (Blocker/Rush/Jamming/…) |
| `GetTrashCount()` / `GetOpponentTrashCount()` trash count | `((IZoneStateReader)card.Context.ZoneMover).GetCards(player, ChoiceZone.Trash).Count` — own = `card.Owner`, opponent = opponent playerId. Deck/security/hand are the same, just change the zone |
| `TopCardHasColor("Red")` color possession | `card.HasCardColor("Red")` (or `card.CardColors` — also reflects color-change effects) |
| `IsOwnerOwnedDigimon(x)` owner + type | Compose: `x.Owner == card.Owner && x.IsDigimon` (`Owner`/`Controller`/`IsDigimon`/`IsTamer` are real) |
| `card.PermanentId` | `card.InstanceId` (permanent id = that card's InstanceId) |
| `card.CardNames`, `card.Level`, `card.HasCardColor`, `card.DP` | Real accessors — build card conditions with these (do not invent separate functions for level/color/name/DP queries) |

**Genuinely absent (documented only; invention/implementation on hold)**:
- **Main-phase check** (`IsMainPhase`): no card-facing predicate. Usually an activated ability's timing/context already forces main phase,
  so a separate check is unneeded — in that case drop it from the condition. If you truly need the phase value, **STOP** (strong-model territory).

### Factory signatures — do not invent arguments

Many compile failures are **factory argument count/type hallucinations**. Always verify signatures in `PRIMITIVE-CATALOG.md` and
call them exactly. Common mistakes:

| Wrong call | Real signature |
|---|---|
| `SelectAndReturnToDeckEffect(...)` | `(CardSource card, Func<HeadlessEntityId,bool> canTarget, int maxCount, bool toTop, bool canEndNotMax, string description)` |
| `PlaceSelfDelayOptionSecurityEffect(card, condition)` | `(CardSource card)` — **only one argument, `card`. There is no condition overload** |
| 2-arg lambda in a predicate slot | Mostly `Func<bool>` (condition) or `Func<HeadlessEntityId,bool>` (canTarget). Verify the argument count against the catalog |

## 10. action_tag → canonical factory map (2026-07-06 — reference-replacement layer)

**Why**: per-card references are 60% signature-unique, so they do not generalize (seeding is pointless). But **action_tags are 83% shared** —
"cards all differ, but what action they perform overlaps". So the **action→factory** map below is the generalization layer that replaces references.
Map the card's action to these factories (all real — not invention). The harness auto-injects only the lines matching the card's action_tags into the prompt
(`action_map.json`, regenerate with `gen_action_map.py`). Verify signatures in `PRIMITIVE-CATALOG.md`.

| action_tag | Canonical factory (variants) | Note |
|---|---|---|
| `play` | `SelectAndPlayFromZoneEffect` / PlayOptionCardEffect | pick from a zone to play / play an option |
| `trash` | `SelectAndTrashFromZoneEffect` / SelectAndTrashDigivolutionEffect | pick from a zone to trash / trash a digivolution source |
| `once_per_turn` | *(modifier)* | not an action = [Once Per Turn] cap. For multi-fire timings the bridge auto-caps via OnceFlags (section 7 v3) |
| `security` | `SelectAndPutSecurityEffect` / ReplaceBottomSecurityWithFaceUpOptionEffect | place into security |
| `memory` | `GainMemoryActivatedEffect` | memory gain/loss (+/-) |
| `digivolve` | `SelectAndDigivolveEffect` / BlastDigivolveEffect, BurstDigivolveEffect, ArtsDigivolveEffect | pick to digivolve / special digivolve |
| `delete` | `SelectAndDestroyEffect` | pick a Digimon to delete (restrict via canTarget predicate) |
| `to_hand` | `SelectAndAddToHandFromZoneEffect` / SelectAndBounceEffect, AddThisCardToHandEffect | zone→hand / field bounce / self to hand |
| `deenergize` | `SelectAndDeDigivolveEffect` / SelectAndTrashDigivolutionEffect | trash N digivolution sources |
| `draw` | `DrawCardsEffect` | draw N |
| `suspend` | `SelectAndSuspendEffect` | pick to suspend (tap) |
| `bounce` | `SelectAndBounceEffect` / SelectAndReturnToDeckEffect | hand bounce / deck (toTop bool) |
| `cannot` | *(restriction-family)* `CanNot*StaticEffect` | per prohibited target: Attack/BeDestroyed/AddSecurity/Digivolve/Block etc. |
| `unsuspend` | `SelectAndUnsuspendEffect` | pick to unsuspend |
| `dp_minus` | `SelectAndBuffDpEffect` (negative) / ChangeDPStaticEffect | DP -N |
| `dp_plus` | `SelectAndBuffDpEffect` / PlayerScopeBuffDpEffect, ChangeSelfDPStaticEffect | DP +N (target/scope/self) |
| `recovery` | `RecoveryTriggerEffect` | security recovery |
| `blocker` | `BlockerStaticEffect` / BlockerSelfStaticEffect | grant Blocker (scope/self) |
| `piercing` | `PiercingStaticEffect` / PierceSelfEffect | grant Piercing (scope/self) |

**Note**: the map only grounds **individual actions** to canonical factories. The **composition** of multiple actions (wiring conditions/targets/timing) and the
**faithful evaluation** of predicates (section 0) still have to be done per card — do not blur them away with tags.

## 5. Pilot measurements (BT1 exact, 15 cards, Sonnet 4.6)

| Round | Prompt | Compile pass |
|---|---|---|
| 1 | none | (many hallucinations) |
| 2 | factory + timing symbol surface | 8/15 (53%) |
| 3 | + commons class distinction | 7/15 (diminishing returns — residual is domain translation) |
| 4 | **+ this cheatsheet** | **12/15 (80%)** |

- The round 3→4 gain (+5 cards) is exactly this cheatsheet's coverage. The remaining 3 cards are all section 4 primitive gaps.
- Conclusion: **symbol surface + this cheatsheet gets ~80% of exact cards to compile**. The rest are primitive gaps (a separate track that
  cannot be opened by prompting). A living document — when new domain patterns emerge in other sets, add them to sections 1–2.
