# AS-IS ↔ TO-BE match-check — manifest part 12/13 (`both_part_12.txt`, 128 files)

- AS-IS base: `/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/`
- TO-BE base: `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/`
- Method: full read of both sides per file, symbol-by-symbol; verdicts from direct source observation only (no prior audit docs / verdict CSVs / code-comment claims used as grounds). Line counts shown per side.

## Tally

- **Total: 128** — MATCH **111**, PROBLEM **17**, stub/trivial (both empty) **0**.
- No file is a legitimate stub-to-stub: every PROBLEM is a case where AS-IS carries real content and the TO-BE path file is an unported 7-line skeleton (or, for #18 ChangePlayCost, a behavioral fidelity gap in a live bridge).

### PROBLEM files (17)

| # | File | Nature | Class |
|---|------|--------|-------|
| 18 | `CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangePlayCost.cs` | Behavioral gap: `activateClass` not threaded into `CanNotBeAffected` immunity test (uses freshly-built `ChangeCostClass` instead) | **Live rules divergence** |
| 67 | `StarterDeck.cs` | Unported skeleton; AS-IS = deck-loader + PlayerPrefs logic | Unity data/persistence |
| 73 | `ShowPhaseNotificationObject.cs` | Unported skeleton; AS-IS = phase-notification UI + `IsSelecting` toggling | Unity UI (state-adjacent) |
| 76 | `DigiXrosEffectObject.cs` | Unported skeleton; AS-IS = DOTween/SE animation | Unity presentation |
| 84 | `GSSReader.cs` | Unported skeleton; AS-IS = Google-Sheets CSV loader (UnityWebRequest) | Unity data loader |
| 101 | `Networking/GamePacketFactory.cs` | Unported skeleton; AS-IS = static packet factory (Register/Create/GetId + 2 dicts) | Photon transport |
| 105 | `AutomaticOrder/StartTurnTamerMemory.cs` | Unported skeleton; AS-IS = deterministic "Set Memory to" tamer-skill ordering | **CoreRule logic gap** |
| 110 | `CardDistributionTab.cs` | Unported skeleton; AS-IS = deck-count/ratio bar UI | Unity UI |
| 112 | `JogressEffectObject.cs` | Unported skeleton; AS-IS = jogress evolution animation coroutine | Unity presentation |
| 118 | `PlayerSelection/ValueSelection.cs` | Unported; AS-IS = IPlayerSelection value model (int/bool) | Selection model (replaced by agent LegalAction) |
| 120 | `Networking/GamePacketRegistration.cs` | Unported skeleton; AS-IS = `[RuntimeInitializeOnLoadMethod]` registering 6 packet factories | Photon transport |
| 121 | `LoadJSON_CardEntity.cs` | Unported; AS-IS = ScriptableObject data container | Unity data container |
| 124 | `PlayerSelection/PermanentSelection.cs` | Unported; AS-IS = IPlayerSelection permanent model | Selection model |
| 125 | `PlayerSelection/CardSelection.cs` | Unported; AS-IS = IPlayerSelection card model | Selection model |
| 126 | `LoadCSV_CardEntity.cs` | Unported; AS-IS = ScriptableObject data container | Unity data container |
| 127 | `PlayerSelection/IPlayerSelection.cs` | Unported; AS-IS = empty marker interface | Selection model (marker) |
| 128 | `Networking/IGamePacket.cs` | Unported; AS-IS = Photon serialize/deserialize interface contract | Photon transport |

**Assessment of the PROBLEM set.** Only two are game-rule-affecting and warrant remediation attention: **#18 ChangePlayCost** (a live bridge whose immunity check diverges from AS-IS — the DP sibling threads `activateClass` correctly, this one does not) and **#105 StartTurnTamerMemory** (`AutomaticOrder.GetSkillIndexAutomaticOrder` deterministic ordering has no mirror anywhere — `MultipleSkills.cs:377` self-documents "AutomaticOrder / autoEffectOrder has no mirror"). The remaining 15 are Unity presentation / ScriptableObject data / Photon-transport / the replaced PlayerSelection model — substrate-class code where the skeleton stub is defensible under the headless design, but each is recorded here as "not a faithful port as it stands" because AS-IS has genuine content and TO-BE is empty (no AS-IS-grounded mirror exists).

### Recurring substrate translations observed in MATCH files (grounded, not flagged)

- `partial class` → `static partial class` + added namespace, dropped `using UnityEngine`/`using Photon` — cosmetic.
- `IEnumerator`/`StartCoroutine`/`yield` → `Task`/`await` for `ICardEffect` coroutine members (matches ported `ActivateClass` signature).
- `card.PermanentOfThisCard()` (extension) → `ICardEffect.ResolvePermanentOfThisCard(card)` (static resolver).
- Reference-equality (`==` on CardSource/Permanent) → `InstanceId` comparison (per-access view identity).
- `Player`/`GetBattleAreaDigimons()` → `HeadlessPlayerId`/`IZoneStateReader.GetCards(owner, BattleArea).Where(IsDigimon)`.
- MainPhaseAction subclasses: Photon `byte[]` ctor + `Serialize`/`Deserialize` stripped, `Execute` game logic preserved 1:1 with `Task` return.
- Preserved AS-IS quirks/typos (faithful, not "corrected"): `SetUpCanNotUntapClass`, `SetAttackingPermaent`, `CanNotBeSwitchAttackTarget` method/class asymmetry, `changeeTraits`, `_changetMinMemory`, `getMaxUnderTamerCount`, class `CanNotDigivolveClass` in `CanNotEvolveClass.cs`, class `AddDigivolutionRequirementClass` in `AddEvolutionConditionClass.cs`, `CollisionClass.HasCollision` predicate-first guard order, `ChangeCardLevelClass .cs` filename space.

### Edge-notes on MATCH bridges (non-blocking, flagged for awareness)

- Files 1/10/18-of-batch-1 (`ChangeDP`, `GiveEffectToPermanent/ChangeDP`, `ChangeOriginDP` bridges): when `activateClass` is null, the bridge passes `activateClass?.EffectSourceCard == null` and the substrate `ArgumentNullException.ThrowIfNull(sourceCard)` throws, whereas AS-IS silently `yield break`s. Edge-case only; all thread `activateClass` correctly otherwise (contrast #18 ChangePlayCost which does not).
- `DeleteSelf.cs` (batch 1 #3): substrate stores delete-timing in an instance metadata key rather than reproducing AS-IS's two `PermanentEffects` providers, and omits the `AddDetailClass` display note — UI-descriptive, no rules impact.
- `Collision.cs` (batch 2 #14) / `ChangeDP`/`ChangeOriginDP`: only omission is the terminal Unity VFX `CreateBuffEffect`/`CreateDebuffEffect` coroutine — no game-state effect.

---

## Per-file verdicts (global manifest order 1–128)

### 1. `Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangeDP.cs`
- AS-IS (71 lines) | TO-BE (18 lines, delegating bridge)
- Verdict: MATCH
- Evidence: Thin `Task` bridge to substrate `ChangeDigimonDPPlayerEffect(permanentCondition, changeValue, effectDuration, activateClass?.EffectSourceCard, activateClass)` (CardEffectCommons.cs:1712). Substrate rebuilds AS-IS PermanentCondition verbatim — `IsPermanentExistsOnBattleArea` + `!TopCard.CanNotBeAffected(activateClass ?? changeDPClass)` + user predicate — then `CardEffectFactory.ChangeDPStaticEffect(...)` + `AddEffectToPlayer(..., EffectTiming.None)` (AS-IS 23-52). Bridge passes original `activateClass` as 5th arg so `CanNotBeAffected` sees the same object AS-IS uses. Omissions: visual buff/debuff coroutines (no state), null-`activateClass` becomes `ThrowIfNull` (substrate convention).

### 2. `Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/IgnoreDigivolutionRequirement.cs`
- AS-IS (40 lines) | TO-BE (49 lines)
- Verdict: MATCH
- Evidence: `GainIgnoreDigivolutionRequirementPlayerEffect` mirrors AS-IS: same `activateClass`/`EffectSourceCard` null guards, same PermanentCondition/CardCondition null-coalescing wrappers, same `CardEffectFactory.AddDigivolutionRequirementStaticEffect(...)` with identical named args incl. literal `"Ignore Digivolution requirements and change digivolution cost"`, same `EffectTiming.None` return. Only substrate cosmetics differ.

### 3. `Script/OptionResolutionClass.cs`
- AS-IS (34 lines) | TO-BE (55 lines)
- Verdict: MATCH
- Evidence: `OptionResolutionClass : ICardEffect, IOptionResolutionEffect` — all members present: `SetUpOptionResolutionClass`, `ResolutionCondition`/`ResolutionCoroutine`, `CanResolve` (identical `ResolutionCondition == null || ResolutionCondition(optionCard)`), `Resolve` with nested `CanResolve` + null-coroutine guard. `Func<...,IEnumerator>`→`Func<...,Task>`, `StartCoroutine`→`await` standard swap; control flow unchanged.

### 4. `Script/CardEffects/ImmuneFromDPMinusClass.cs`
- AS-IS (42 lines) | TO-BE (46 lines)
- Verdict: MATCH
- Evidence: Byte-for-byte identical body. Same fields `_permanentCondition`/`_cardEffectCondition`, `SetUpImmuneFromDPMinusClass`, and the 4-level nested-if in `ImmuneFromDPMinus`. Only header differs.

### 5. `Script/CardEffects/CannotReturnToLibraryClass.cs`
- AS-IS (41 lines) | TO-BE (46 lines)
- Verdict: MATCH
- Evidence: Identical `CannotReturnToLibraryClass : ICardEffect, ICannotReturnToLibraryEffect`. Setup method, both `Func` props, and `CannotReturnToLibrary` nested-if guard chain verbatim; only header differs.

### 6. `Script/CardEffectFactory/KeyWordEffects/Ascension.cs`
- AS-IS (37 lines) | TO-BE (48 lines)
- Verdict: MATCH
- Evidence: `AscensionSelfEffect(bool, CardSource, Func<bool>, bool=false)` verbatim: `new ActivateClass()`, `SetUpICardEffect("Ascension", ...)`, `SetUpActivateClass(..., -1, false, DataBase.AscensionEffectDescription())`, `SetIsInheritedEffect`/`SetIsLinkedEffect`, three local functions identical (`CanTriggerAscension && (condition == null || condition())` etc.). Only substrate change: `ActivateCoroutine` returns `Task`.

### 7. `Script/CardEffects/CannotReturnToHandClass.cs`
- AS-IS (40 lines) | TO-BE (46 lines)
- Verdict: MATCH
- Evidence: Identical `CannotReturnToHandClass : ICardEffect, ICannotReturnToHandEffect` — setup, props, nested-if chain in `CannotReturnToHand`. Verbatim modulo header.

### 8. `Script/CardEffectCommons/CanUseEffects/WhenWouldLink.cs`
- AS-IS (41 lines) | TO-BE (44 lines)
- Verdict: MATCH
- Evidence: `CanTriggerWhenWouldLink(Hashtable, Func<CardSource,bool>, Func<Permanent,bool>, Func<SelectCardEffect.Root,bool>=null, Func<ICardEffect,bool>=null)` byte-identical nesting: card→cardCondition→permanent→permanentCondition→root→rootCondition→cardEffect→cardEffectCondition→true, else false.

### 9. `Script/CardEffectCommons/CanUseEffects/OnSuspend.cs`
- AS-IS (41 lines) | TO-BE (43 lines)
- Verdict: MATCH
- Evidence: `CanTriggerWhenSelfPermanentSuspends` delegates with `(permanent) => permanent.cardSources.Contains(card)`; `CanTriggerWhenPermanentSuspends` iterates `GetPermanentsFromHashtable`, guards `IsPermanentExistsOnBattleArea`, `permanentCondition != null`, `permanentCondition(Permanent)` → true. Identical.

### 10. `Script/CardEffectCommons/CanUseEffects/OnTrashBySelfDigiBurst.cs`
- AS-IS (41 lines) | TO-BE (43 lines)
- Verdict: MATCH
- Evidence: `CanTriggerOnTrashBySelfDigiBurst` reproduces the nested `CardEffectCondition` closure verbatim — null check, `!string.IsNullOrEmpty(EffectDiscription)`, `.Contains("Digi-Burst")`, `EffectSourceCard != null`, `IsExistOnBattleArea(...)`, `cardSources` containment — then `return CanTriggerOnTrashSelfDigivolutionCard(...)`. Only `PermanentOfThisCard()`→`ResolvePermanentOfThisCard(...)` substrate swap.

### 11. `Script/CardEffectCommons/CanUseEffects/CanSuspend.cs`
- AS-IS (41 lines) | TO-BE (42 lines)
- Verdict: MATCH
- Evidence: `CanActivatePermanentSuspendCostEffect(Permanent, bool)` mirrored verbatim (battle-area `!IsSuspended && CanSuspend`; `includeBreeding` breeding branch). Sibling `CanActivateSuspendCostEffect(CardSource, bool)` intentionally omitted here (CS0111) and lives at substrate CardEffectCommons.cs:3817 — verified result-equivalent (top-card unsuspended + `CanSuspend` + breeding stack scan).

### 12. `Script/CardEffects/ActivateClass.cs`
- AS-IS (30 lines) | TO-BE (53 lines)
- Verdict: MATCH
- Evidence: `ActivateClass : ICardEffect, ActivateICardEffect` with `PermanentWhenTriggered`/`TopCardWhenTriggered` (=null), `_activateCoroutine`, `SetUpActivateClass` (identical `SetCanActivateCondition`/`SetMaxCountPerTurn`/`SetIsOptional`/`SetEffectDiscription(DataBase.ReplaceToASCII(...))` + assign), `Activate` (null-guarded invoke). Only coroutine→async translation.

### 13. `Script/CardEffectCommons/CanUseEffects/OnTrashHand.cs`
- AS-IS (40 lines) | TO-BE (42 lines)
- Verdict: MATCH
- Evidence: `CanTriggerOnTrashSelfHand` delegates with `cardSource => cardSource == card`; `CanTriggerOnTrashHand` reproduces cardEffect→cardEffectCondition→discardedCards→`Count(... cardCondition ...) >= 1`→true. Identical short-circuits.

### 14. `Script/CardEffects/CannotAddSecurityClass.cs`
- AS-IS (39 lines) | TO-BE (43 lines)
- Verdict: MATCH
- Evidence: Identical `CannotAddSecurityClass : ICardEffect, ICannotAddSecurityEffect` — setup, `PlayerCondition`/`CardEffectCondition`, `cannotAddSecurity` nested-if. Verbatim modulo header.

### 15. `Script/CardEffects/CannotAddMemoryClass.cs`
- AS-IS (38 lines) | TO-BE (43 lines)
- Verdict: MATCH
- Evidence: Identical `CannotAddMemoryClass : ICardEffect, ICannotAddMemoryEffect` — setup, both props, `cannotAddMemory` nested-if matching its Security sibling. Verbatim modulo header.

### 16. `Script/CardEffectCommons/CanUseEffects/OnReturnLibraryBottomDigivolutionCards.cs`
- AS-IS (39 lines) | TO-BE (41 lines)
- Verdict: MATCH
- Evidence: `CanTriggerOnReturnToLibraryBottomDigivolutionCard` verbatim: `IsExistOnBattleArea(card)`→hashtable→permanent→`Permanent == resolve(card)`→deckBottomCards→`Count(DigivolutionCards.Contains && cardCondition) >= 1`→true. Only `PermanentOfThisCard()`→`ResolvePermanentOfThisCard(card)` swap.

### 17. `Script/CardEffectCommons/CanUseEffects/OptionEffect.cs`
- AS-IS (40 lines) | TO-BE (40 lines)
- Verdict: MATCH
- Evidence: `CanTriggerOptionMainEffect` verbatim (card→`Card == card`→true). Sibling `CanDeclareOptionDelayEffect(CardSource)` omitted here (CS0111), lives at substrate CardEffectCommons.cs:3852 — verified: `!IsExistOnBattleArea` → false, else `!(top card entered this turn)`; AS-IS `EnterFieldTurnCount != TurnCount` re-expressed via `enteredThisTurn` metadata (defensible substrate re-expression).

### 18. `Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangePlayCost.cs`
- AS-IS (62 lines) | TO-BE (18 lines, delegating bridge)
- Verdict: **PROBLEM** — `CanNotBeAffected` argument diverges from AS-IS (behavioral fidelity gap).
- Evidence: AS-IS `ChangePlayCostPlayerEffect(..., ICardEffect activateClass)` builds PermanentCondition with `!permanent.TopCard.CanNotBeAffected(activateClass)` (ChangePlayCost.cs:26) — immunity tested against the ORIGINAL triggering effect. The TO-BE bridge calls substrate as `ChangePlayCostPlayerEffect(permanentCondition, changeValue, setFixedCost, effectDuration, activateClass?.EffectSourceCard)` — drops `activateClass` entirely. Substrate (CardEffectCommons.cs:3109) has NO `activateClass` param and instead tests `!permanent.TopCard.CanNotBeAffected(changeCostClass)` (line 3126) — the freshly-built `ChangeCostClass`. Observable: `CardSource.CanNotBeAffected` (CardSource.cs:1060) forwards the effect into `CanNotAffectedClass.SkillCondition(cardEffect)` (CanNotAffectedClass.cs:22), a predicate that can inspect the effect's type/description — a named ActivateClass vs the anonymous `ChangeCostClass` (null effectName, different type) can diverge. Contrast: the DP sibling (file 1) threads `activateClass` via optional param + `activateClass ?? changeDPClass`, so the substrate comment "Mirrors ChangeDigimonDPPlayerEffect exactly" is inaccurate for the cost path.

### 19. `Script/CardEffects/CanSelectAssemblyClass.cs`
- AS-IS (38 lines) | TO-BE (42 lines)
- Verdict: MATCH
- Evidence: Identical `CanSelectAssemblyClass : ICardEffect, ICanSelectAssemblyEffect` — setup, `CanSelectCondition`, `CanSelect` nested-if. Verbatim modulo header.

### 20. `Script/CardEffects/CanSelectDigiXrosClass.cs`
- AS-IS (38 lines) | TO-BE (42 lines)
- Verdict: MATCH
- Evidence: Identical `CanSelectDigiXrosClass : ICardEffect, ICanSelectDigiXrosEffect` — setup, `CanSelectCondition`, `CanSelect` guard chain matching its Assembly sibling. Verbatim modulo header.

### 21. `Script/CardEffectCommons/CanUseEffects/OnCardsReturnToHandFromTrash.cs`
- AS-IS (38 lines) | TO-BE (41 lines)
- Verdict: MATCH
- Evidence: `CanTriggerWhenOwnerCardsReturnToHandFromTrash` + `CanTriggerWhenCardsReturnToHandFromTrash`; owner-overload local `CardCondition` (`cardSource.Owner == card.Owner && (cardCondition == null || cardCondition(cardSource))`), `GetCardSourcesFromHashtable`/`Filter(!IsDigiEgg && ...)`/`Count >= 1` character-identical. Only namespace/using/`static`.

### 22. `Script/CardEffectCommons/CanUseEffects/OnCardsReturnToLibraryFromTrash.cs`
- AS-IS (38 lines) | TO-BE (41 lines)
- Verdict: MATCH
- Evidence: "Library" variant of #21; both trigger methods line-for-line, same `!IsDigiEgg` filter, owner-condition closure, `Count >= 1` gate, fallthrough `return false`. Only namespace/using/`static`.

### 23. `Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/DeleteSelf.cs`
- AS-IS (46 lines) | TO-BE (32 lines)
- Verdict: MATCH (bridge; documented deviation)
- Evidence: `DeleteTiming` enum re-declared 1:1. TO-BE converts enum→string and delegates to substrate `AddSelfDeleteEffect(Permanent, string, CardSource)` (CardEffectCommons.cs:2582); mapping AtOwnTurnEnd→"own"/AtOpponentTurnEnd→"opponent"/AtTurnEnd→"each" consistent with AS-IS `deleteOnOwnturn = timing != AtOpponentTurnEnd`. Substrate stores timing in `DeleteAtTurnEndKey` metadata (consumed by GameFlowProcessor) rather than two `PermanentEffects` providers, and omits the AS-IS `GetDetailEffect`→`AddDetailClass(message,...)` display note — UI-descriptive only.

### 24. `Script/CardEffects/AddSkillClass.cs`
- AS-IS (36 lines) | TO-BE (41 lines)
- Verdict: MATCH
- Evidence: `AddSkillClass : ICardEffect, IAddSkillEffect` — fields `_cardSourceCondition`/`_getEffects`/`_limitedTiming`, `SetUpAddSkillClass`, `ShouldAddEffect` (`_limitedTiming == null` → true else `timing == _limitedTiming`), `GetCardEffect` (conditional `_getEffects` reassign + `SetEffectSourceCard(card)`) identical.

### 25. `Script/CardEffects/CanAttackTargetDefendingPermanentClass.cs`
- AS-IS (36 lines) | TO-BE (41 lines)
- Verdict: MATCH
- Evidence: Same three predicate props (Attacker/Defender/CardEffect Condition), `SetUp...`, and deeply-nested `CanAttackTargetDefendingPermanent` guard chain character-identical incl. fallthrough `return false`.

### 26. `Script/CardEffects/CanNotBeDestroyedBySkillClass.cs`
- AS-IS (36 lines) | TO-BE (41 lines)
- Verdict: MATCH
- Evidence: `_canNotBeDestroyedCondition`, `SetUpCanNotBeDestroyedBySkillClass`, and the four-level nested null/condition guard in `CanNotBeDestroyedBySkill` identical; only namespace/using changed.

### 27. `Script/CardEffectCommons/CanUseEffects/WhenUseDigiBurst.cs`
- AS-IS (37 lines) | TO-BE (39 lines)
- Verdict: MATCH
- Evidence: `CanTriggerWhenUseDigiBurst` identical: permanent→TopCard→`permanentCondition == null || permanentCondition(permanent)`→cardEffect→`cardEffectCondition == null || cardEffectCondition(CardEffect)`→true, else false.

### 28. `Script/CardEffects/CanNotTrashFromDigivolutionCardsClass.cs`
- AS-IS (34 lines) | TO-BE (40 lines)
- Verdict: MATCH
- Evidence: `SetUpCanNotTrashFromDigivolutionCardsClass`, `CardCondition`/`CardEffectCondition`, and `CanNotTrashFromDigivolutionCards` identical incl. `return !cardSource.IsFlipped;` and the AS-IS asymmetry (only `CardEffectCondition` null-checked, not `CardCondition`) preserved.

### 29. `Script/CardEffectCommons/DigiXrosEffects.cs`
- AS-IS (32 lines) | TO-BE (43 lines)
- Verdict: MATCH
- Evidence: `GetDigiXrosConditionsFromNames` 1:1 — `foreach(name)` building `DigiXrosConditionElement(cardSource => CardCondition(cardSource, name), name)`, `new DigiXrosCondition(elements, CanTargetCondition_ByPreSelecetedList, CostReduction)`, local `CardCondition` (`!= null && Owner == card.Owner && IsDigimon && CardNames_DigiXros.Contains(name)`) identical. Extra lines are comments.

### 30. `Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/ChangeDP.cs`
- AS-IS (57 lines) | TO-BE (17 lines)
- Verdict: MATCH (bridge to faithful substrate)
- Evidence: AS-IS-signature `Task` overload delegating to substrate `ChangeDigimonDP(Permanent?, int, EffectDuration, CardSource, ICardEffect?)` (CardEffectCommons.cs:1627, read in full): null/`IsPermanentExistsOnBattleArea`/`changeValue == 0` guards, `CanUseCondition` closure with `CanNotBeAffected`, `CardEffectFactory.ChangeTargetDPStaticEffect(...)`, `AddEffectToPermanent(..., EffectTiming.None)` all 1:1. Only UI buff/debuff coroutine dropped. Null-`activateClass` throw-vs-`yield break` edge noted.

### 31. `Script/CardEffectCommons/MinMax_DP_Cost_Level/Cost/IsMinCost.cs`
- AS-IS (33 lines) | TO-BE (40 lines)
- Verdict: MATCH
- Evidence: All AS-IS guards preserved (owner match, on-battle-area, digimon-or-tamer, optional `condition` gating SUBJECT only, `HasPlayCost`, `IsDigimonOnly && !IsDigimon` folded). Cost scan: DigimonOnly→digimon w/ `HasPlayCost` else digimon-or-tamer w/ `HasPlayCost`, then `costs.Count >= 1 && GetCostItself == costs.Min()`. `Player`→`HeadlessPlayerId`, `GetBattleAreaDigimons/Permanents`→`GetCards(owner, BattleArea)`, owner-variant→plain exists-check are valid substrate translations (owner already equal).

### 32. `Script/CardEffects/AddDetailClass.cs`
- AS-IS (34 lines) | TO-BE (41 lines)
- Verdict: MATCH
- Evidence: `AddDetailClass : ICardEffect, IAddDetailEffect` — `_permanentCondition`/`_detail`/`_triggerEffect`, `SetUpAddDetailClass`, `PermanentCondition`, `GetDetail`, `TriggerEffect` identical.

### 33. `Script/CardEffects/BlockerClass.cs`
- AS-IS (35 lines) | TO-BE (39 lines)
- Verdict: MATCH
- Evidence: `BlockerClass : ICardEffect, IBlockerEffect`, `SetUpBlockerClass`, `PermanentCondition`, `IsBlocker` nested guard identical. Only `using Photon`/UnityEngine dropped, namespace added.

### 34. `Script/CardEffects/IcecladClass.cs`
- AS-IS (35 lines) | TO-BE (39 lines)
- Verdict: MATCH
- Evidence: `IcecladClass : ICardEffect, IIcecladEffect`, `SetUpIcecladClass`, `HasIceclad` four-level guard chain identical; no logic change.

### 35. `Script/CardEffects/RebootClass.cs`
- AS-IS (35 lines) | TO-BE (39 lines)
- Verdict: MATCH
- Evidence: `RebootClass : ICardEffect, IRebootEffect`, `SetUpRebootClass`, `HasReboot` guard chain identical. Substrate-only header differences.

### 36. `Script/CardEffects/RushClass.cs`
- AS-IS (35 lines) | TO-BE (39 lines)
- Verdict: MATCH
- Evidence: `RushClass : ICardEffect, IRushEffect`, `SetUpRushClass`, `HasRush` guard chain identical to keyword-class family.

### 37. `Script/CardEffects/ScapegoatClass.cs`
- AS-IS (35 lines) | TO-BE (39 lines)
- Verdict: MATCH
- Evidence: `ScapegoatClass : ICardEffect, IScapegoatEffect`, `SetUpScapegoatClass`, `HasScapegoat` guard chain identical; only namespace/using differences.

### 38. `Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/ChangeOriginDP.cs`
- AS-IS (54 lines) | TO-BE (17 lines)
- Verdict: MATCH (bridge to faithful substrate)
- Evidence: Bridges to substrate `ChangeBaseDigimonDP(...)` (CardEffectCommons.cs:3158, read in full): same guards, `CanUseCondition` closure, `CardEffectFactory.ChangeBaseDPStaticEffect(...)`, the important `changeBaseDPClass.SetActivatedTime(DateTime.Now)`, `AddEffectToPermanent(..., EffectTiming.None)`. Only UI buff/debuff coroutine dropped. Same null-`activateClass` throw-vs-`yield break` edge as #30.

### 39. `Script/CardEffects/CanNotEvolveClass.cs`
- AS-IS (33 lines) | TO-BE (39 lines)
- Verdict: MATCH
- Evidence: AS-IS file declares class `CanNotDigivolveClass : ICardEffect, ICanNotDigivolveEffect` (filename mismatch is in AS-IS itself); TO-BE keeps exact name — not an invented rename. `_PermanentCondition`/`_CardCondition`, `SetUpCanNotEvolveClass`, `CanNotEvolve` identical.

### 40. `Script/CardEffectCommons/CanUseEffects/OnMove.cs`
- AS-IS (35 lines) | TO-BE (38 lines)
- Verdict: MATCH
- Evidence: `CanTriggerOnMove` (movedPermanent→`IsPermanentExistsOnBattleArea`→`permanentCondition == null || permanentCondition(permanent)`→true) and `GetMovedPermanentFromHashtable` (returns `GetPermanentFromHashtable(hashtable)`) identical.

### 41. `Script/CardEffectCommons/CanUseEffects/WhenDiscardLibrary.cs`
- AS-IS (34 lines) | TO-BE (37 lines)
- Verdict: MATCH
- Evidence: `CanTriggerWhenSelfDiscardLibrary` → `CanTriggerWhenDiscardLibrary`; predicate byte-identical: `GetDiscardedCardsFromHashtable`, null guard, `.Some(cardSource != null && !cardSource.IsBeingRevealed && (cardCondition == null || cardCondition(cardSource)))`. Only namespace/using/`static`.

### 42. `Script/CardEffects/ImmuneFromDeDigivolveClass.cs`
- AS-IS (33 lines) | TO-BE (38 lines)
- Verdict: MATCH
- Evidence: `ImmuneFromDeDigivolveClass : ICardEffect, IImmuneFromDeDigivolveEffect` — `PermanentCondition`, `SetUp...`, `ImmuneDeDigivolve` nested null-guard cascade identical. Only namespace/using.

### 43. `Script/CardEffects/CanNotSuspendClass.cs`
- AS-IS (32 lines) | TO-BE (37 lines)
- Verdict: MATCH
- Evidence: `CanNotSuspendClass : ICardEffect, ICanNotSuspendEffect`, `PermanentCondition`, `SetUpCanNotSuspendClass`, `CanNotSuspend` cascade identical. Only namespace/using.

### 44. `Script/CardEffects/CannotIgnoreDigivolutionConditionClass.cs`
- AS-IS (31 lines) | TO-BE (38 lines)
- Verdict: MATCH
- Evidence: `SetUpCannotIgnoreDigivolutionConditionClass(Func<Player,Permanent,CardSource,bool>)`, field `IgnoreDigivolutionCondition`, method `cannotIgnoreDigivolutionCondition` identical incl. compound guard `player != null && targetPermanent != null && cardSource != null` then `TopCard != null` then delegate. Only namespace/using.

### 45. `Script/CardEffects/AddJogressLevelsClass.cs`
- AS-IS (31 lines) | TO-BE (37 lines)
- Verdict: MATCH
- Evidence: `AddJogressLevelsClass : ICardEffect, IAddJogressLevelsEffect`, `_getJogressLevels`, `SetUp...`, `GetJogressLevels` four-deep guard identical (else `null`). Only namespace/using.

### 46. `Script/CardEffects/CanNotBeDestroyedClass.cs`
- AS-IS (31 lines) | TO-BE (37 lines)
- Verdict: MATCH
- Evidence: `CanNotBeDestroyedClass : ICardEffect, ICanNotBeDestroyedEffect`, `_permanentCondition`, `SetUp...`, `CanNotBeDestroyed` cascade identical. Only namespace/using.

### 47. `Script/CardEffects/CanNotBeRemovedClass.cs`
- AS-IS (31 lines) | TO-BE (37 lines)
- Verdict: MATCH
- Evidence: `CanNotBeRemovedClass : ICardEffect, ICanNotBeRemovedEffect`, `_permanentCondition`, `SetUp...`, `CanNotBeRemoved` 3-deep guard identical. Only namespace/using.

### 48. `Script/CardEffects/CanNotUnsuspendClass.cs`
- AS-IS (31 lines) | TO-BE (37 lines)
- Verdict: MATCH
- Evidence: `CanNotUnsuspendClass : ICardEffect, ICanNotUnsuspendEffect`. AS-IS setter named `SetUpCanNotUntapClass` (legacy "Untap") — TO-BE preserves it faithfully (not corrected). `CanNotUnsuspend` cascade identical.

### 49. `Script/CardEffects/CannotBlockClass.cs`
- AS-IS (31 lines) | TO-BE (37 lines)
- Verdict: MATCH
- Evidence: `CannotBlockClass : ICardEffect, ICannotBlockEffect`, `_permanentsCondition = null`, `SetUp...`. `CannotBlock(attacking, defending)` uses `IsPermanentExistsOnBattleArea(attacking)` then `(defending)`, then delegate — identical order/conditions.

### 50. `Script/CardEffects/DontHaveDPClass.cs`
- AS-IS (31 lines) | TO-BE (37 lines)
- Verdict: MATCH
- Evidence: `DontHaveDPClass : ICardEffect, IDontHaveDPEffect`, `PermanentCondition`, `SetUp...`, `DontHaveDP` cascade identical. Only namespace/using.

### 51. `Script/CardEffectCommons/CanUseEffects/OnCardsAddedToHand.cs`
- AS-IS (32 lines) | TO-BE (35 lines)
- Verdict: MATCH
- Evidence: `CanTriggerOnHandAdded(Hashtable, Player, Func<ICardEffect,bool>)` identical: `GetPlayersFromHashtable`, `Players != null`, `Players.Contains(player)`, cardEffect, `cardEffectCondition == null || cardEffectCondition(CardEffect)` → true, else false. Only namespace/using/`static`.

### 52. `Script/CardEffects/CanNotSwitchAttackTargetClass.cs`
- AS-IS (30 lines) | TO-BE (37 lines)
- Verdict: MATCH
- Evidence: `CanNotSwitchAttackTargetClass : ICardEffect, ICanNotSwitchAttackTargetEffect`, `PermanentCondition`, `SetUp...`, method `CanNotBeSwitchAttackTarget` (asymmetric name vs class — preserved faithfully) with identical cascade. Only namespace/using.

### 53. `Script/MainPhaseAction/AttackPermanentAction.cs`
- AS-IS (45 lines) | TO-BE (22 lines)
- Verdict: MATCH
- Evidence: Game-logic ctor `AttackPermanentAction(int, int)` + `Execute` → `stateMachine.SetAttackingPermaent(PermanentIndex, AttackTargetPermanentIndex)` preserved 1:1 incl. AS-IS "Permaent" typo. Removed `byte[]` ctor/`Deserialize`/`Serialize` are Photon transport (`Protocol.*`, `ExitGames.Client.Photon`); `Execute` returns `Task` for async base. No game-state logic omitted.

### 54. `Script/CardEffectCommons/KeyWordEffects/Collision.cs`
- AS-IS (31 lines) | TO-BE (35 lines)
- Verdict: MATCH
- Evidence: `GainCollision(Permanent, EffectDuration, ICardEffect)` preserves all four early-exit guards in order (`targetPermanent == null`, `!IsPermanentExistsOnBattleArea`, `activateClass == null`, `activateClass.EffectSourceCard == null`), `card = targetPermanent.TopCard`, `PermanentEffectFactory.CollisionEffect(...)`, `AddEffectToPermanent(... OnCounterTiming)` identical. Only omission: terminal `CreateBuffEffect` Unity VFX coroutine (no game state).

### 55. `Script/CardEffects/CanNotAffectedClass.cs`
- AS-IS (30 lines) | TO-BE (36 lines)
- Verdict: MATCH
- Evidence: `CanNotAffectedClass : ICardEffect, ICanNotAffectedEffect`, `CardCondition`/`SkillCondition`, `SetUp...`, `CanNotAffect(CardSource, ICardEffect)` identical: `cardSource != null && cardEffect != null` → both conditions non-null → `CardCondition(cardSource) && SkillCondition(cardEffect)`. Only namespace/using.

### 56. `Script/CardEffects/CanNotAttackTargetDefendingPermanentClass.cs`
- AS-IS (30 lines) | TO-BE (36 lines)
- Verdict: MATCH
- Evidence: `_attackerCondition`/`_defenderCondition = null`, `SetUp...`. `CanNotAttackTargetDefendingPermanent` uses `IsPermanentExistsOnBattleArea(attacker)` then `_attackerCondition == null || _attackerCondition(attacker)` and same for defender — identical short-circuit semantics. Only namespace/using.

### 57. `Script/CardEffects/CanNotMoveClass.cs`
- AS-IS (30 lines) | TO-BE (36 lines)
- Verdict: MATCH
- Evidence: `CanNotMoveClass : ICardEffect, ICanNotMoveEffect`, `_cardCondition`/`_cardEffectCondition`, `SetUp...`, `CanNotMove` identical: `cardSource != null` → both delegates non-null → `_cardCondition(cardSource) && _cardEffectCondition(cardEffect)`. Only namespace/using.

### 58. `Script/CardEffects/CanNotPutFieldClass.cs`
- AS-IS (30 lines) | TO-BE (36 lines)
- Verdict: MATCH
- Evidence: `CanNotPutFieldClass : ICardEffect, ICanNotPutFieldEffect`, two delegates, `SetUp...`, `CanNotPutField` cascade identical to AS-IS. Only namespace/using.

### 59. `Script/CardEffects/CanNotSelectBySkillClass.cs`
- AS-IS (30 lines) | TO-BE (36 lines)
- Verdict: MATCH
- Evidence: `CanNotSelectBySkillClass : ICardEffect, ICanNotSelectBySkillEffect`, `PermanentCondition`/`CardEffectCondition`, `SetUp...`. `CanNotSelectBySkill` keeps full compound guard `permanent != null && TopCard != null && cardEffect != null && EffectSourceCard != null` then both predicates → `&&`. Identical. Only namespace/using.

### 60. `Script/MainPhaseAction/ActivateCardAction.cs`
- AS-IS (45 lines) | TO-BE (21 lines)
- Verdict: MATCH
- Evidence: Game-logic ctor `ActivateCardAction(int cardIndex, int skillIndex)` + `Execute` → `stateMachine.SetActCardSkill(CardIndex, SkillIndex)` preserved 1:1. Removed `byte[]` ctor/`Deserialize`/`Serialize` are Photon transport; `Execute` returns `Task`. No game logic omitted.

### 61. `Script/CardEffects/ChangePermanentLevelClass.cs`
- AS-IS (30 lines) | TO-BE (35 lines)
- Verdict: MATCH
- Evidence: `ChangePermanentLevelClass : ICardEffect, IChangePermanentLevelEffect`, `GetLevel` (=null), `SetUp...`. `GetPermanentLevel(int level, Permanent permanent)` identical: guards then `level = GetLevel(permanent, level)` (arg order permanent-then-level preserved) return `level`. Only namespace/using.

### 62. `Script/CardEffects/CollisionClass.cs`
- AS-IS (29 lines) | TO-BE (36 lines)
- Verdict: MATCH
- Evidence: `CollisionClass : ICardEffect, ICollisionEffect`, `SetUpCollisionClass(Func<Permanent,bool>)`, `PermanentCondition`, `HasCollision` identical — AS-IS guard order `PermanentCondition != null` FIRST then `permanent != null`→`TopCard != null`→`PermanentCondition(permanent)`; TO-BE preserves this exact (unusual) ordering. Only namespace/using.

### 63. `Script/MainPhaseAction/ActivatePermanentAction.cs`
- AS-IS (44 lines) | TO-BE (21 lines)
- Verdict: MATCH
- Evidence: Core logic preserved 1:1 — `PermanentIndex`/`SkillIndex`, `(int, int)` ctor, `Execute` → `stateMachine.SetActSkill(PermanentIndex, SkillIndex)`. Drops `byte[]` ctor + `Serialize`/`Deserialize` (Photon transport), returns `Task.CompletedTask`. Namespace is `...CardEffectCommons` (mildly odd for this type) but harmless.

### 64. `Script/CardEffects/DontBattleSecurityDigimonClass.cs`
- AS-IS (29 lines) | TO-BE (35 lines)
- Verdict: MATCH
- Evidence: Byte-for-byte identical body — `SetUpDontBattleSecurityDigimonClass`, `Func<CardSource,bool> CardSourceCondition`, nested null/condition guards in `DontBattleSecurityDigimon`. Only added namespace/usings.

### 65. `Script/CardEffectCommons/CanUseEffects/OnFaceUpSecurityIncrease.cs`
- AS-IS (30 lines) | TO-BE (33 lines)
- Verdict: MATCH
- Evidence: `CanTriggerOnFaceUpSecurityIncreases` identical — same signature/defaults, `player == null || player.Equals(_player)` short-circuit, `FaceUpCards.Count(cardSource => cardCondition == null || cardCondition(cardSource)) >= 1`. Only `partial`→`static partial`.

### 66. `Script/CardEffectCommons/CanUseEffects/WhenPermanentWouldPlay.cs`
- AS-IS (30 lines) | TO-BE (33 lines)
- Verdict: MATCH
- Evidence: `CanTriggerWhenPermanentWouldPlay` identical: `IsEvolution` via `CardEffectCommons.IsEvolution(hashtable)`, `if (!IsEvolution)` gate, `GetCardFromHashtable`, `cardCondition == null || cardCondition(Card)`. Same negation/short-circuit; only `static` added.

### 67. `Script/StarterDeck.cs`
- AS-IS (56 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — TO-BE unported skeleton; AS-IS carries real logic.
- Evidence: AS-IS defines `StarterDeck.SetStarterDecks()` (branch on `ContinuousController.instance.DeckDatas.Count == 0`, foreach add-all vs add-if-not-`HasPlayerPrefs`) and `[Serializable] StarterDeckData` with `AddDeckData()` (ShuffleDeckCode/GetDeckCode, `DeckDatas.Add`, `PlayerPrefs.SetInt(Key,2)`) and `HasPlayerPrefs()` (`PlayerPrefs.HasKey` + `GetInt==2`). TO-BE is comment-only ("Skeleton only. Port or implement deterministic .NET logic later."). Unity DataLoader/persistence infra — deferral defensible, but not ported.

### 68. `Script/CardEffectCommons/MinMax_DP_Cost_Level/DP/IsMaxDP.cs`
- AS-IS (24 lines) | TO-BE (38 lines)
- Verdict: MATCH
- Evidence: All AS-IS guards reproduced (combined into one OR): null, `TopCard==null`→`InstanceId.IsEmpty`, owner→`OwnerId!=owner`, `!IsPermanentExistsOn...BattleAreaDigimon`, `permanentCondition`, `!HasDP && BaseDP<=0`. AS-IS evaluates DP guard before `permanentCondition`; TO-BE reverses those two — both side-effect-free `return false` short-circuits, behavior-neutral. Scan mirrors AS-IS `GetBattleAreaDigimons().Filter((HasDP||BaseDP>0) && cond).Map(DP)` via `GetCards(owner,BattleArea).Where(IsDigimon && cond && (HasDP||BaseDP>0)).Select(DP)`; final `dps.Count>=1 && permanent.DP==dps.Max()` identical. Owner-variant helper drop compensated by separate `OwnerId != owner`.

### 69. `Script/CardEffectCommons/MinMax_DP_Cost_Level/DP/IsMinDP.cs`
- AS-IS (25 lines) | TO-BE (37 lines)
- Verdict: MATCH
- Evidence: Guard order matches AS-IS exactly (null, InstanceId/TopCard, owner, exists-on-battle-area, `condition` before `!HasDP && BaseDP<=0`). Scan mirrors AS-IS `Filter(condition).Filter(HasDP||BaseDP>0).Map(DP)` with `Where(IsDigimon && cond && (HasDP||BaseDP>0)).Select(DP)`; final `permanent.DP == DPs.Min()`.

### 70. `Script/CardEffects/CanNotPlayClass.cs`
- AS-IS (28 lines) | TO-BE (34 lines)
- Verdict: MATCH
- Evidence: Identical body — `_cardCondition`, `SetUpCanNotPlayClass`, `CanNotPlay` with `_cardCondition != null`→`cardSource != null`→`_cardCondition(cardSource)`→true, else false. Only namespace/usings.

### 71. `Script/CardEffects/IgnoreColorConditionClass.cs`
- AS-IS (28 lines) | TO-BE (34 lines)
- Verdict: MATCH
- Evidence: Identical — AS-IS nests `cardSource != null` outer / `_cardCondition != null` inner (opposite order to CanNotPlayClass); TO-BE preserves exact ordering. Returns true only when delegate fires.

### 72. `Script/CardEffects/TreatAsDigimonClass.cs`
- AS-IS (28 lines) | TO-BE (34 lines)
- Verdict: MATCH
- Evidence: Identical — `Func<Permanent,bool> PermanentCondition`, `SetUpTreatAsDigimonClass`, `IsDigimon` with `PermanentCondition != null && permanent != null`→`TopCard != null`→`PermanentCondition(permanent)`. Same combined-AND/nesting.

### 73. `Script/ShowPhaseNotificationObject.cs`
- AS-IS (55 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — TO-BE unported skeleton; AS-IS carries real logic.
- Evidence: AS-IS `MonoBehaviour` has `Init/Off`, `ShowPhase(GameContext.phase)` (Breeding/Main switch setting `PhaseText.text`, `gameObject.SetActive`), and `CloseCoroutine` toggling `GManager.instance.turnStateMachine.IsSelecting` true→false around animator/`WaitForSeconds`. TO-BE comment-only. Bulk is Unity UI/animation but `IsSelecting` toggling is state-adjacent; nothing ported.

### 74. `Script/CardEffectCommons/CanUseEffects/WhenAddHand.cs`
- AS-IS (29 lines) | TO-BE (32 lines)
- Verdict: MATCH
- Evidence: `CanTriggerWhenAddHand` identical — `GetPlayersFromHashtable`, `Players.Count(player => playerCondition==null || playerCondition(player)) >= 1`, cardEffect, `cardEffectCondition == null || cardEffectCondition(CardEffect)`. Only `static` added.

### 75. `Script/CardEffects/AddEvolutionConditionClass.cs`
- AS-IS (27 lines) | TO-BE (34 lines)
- Verdict: MATCH
- Evidence: File contains class `AddDigivolutionRequirementClass : ICardEffect, IAddDigivolutionRequirementEffect` (class name differs from filename in AS-IS itself); TO-BE preserves same class/interface names — grounded, not invented. Body identical: `Func<Permanent,CardSource,IgnoreRequirement,bool,int> _getEvoCost`, `SetUp...`, `GetEvoCost` guarding `permanent!=null && cardSource!=null`→`TopCard!=null`→`_getEvoCost!=null` (else `-1`).

### 76. `Script/DigiXrosEffectObject.cs`
- AS-IS (54 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — TO-BE unported skeleton; AS-IS carries real (presentation) logic.
- Evidence: AS-IS `DigiXrosEffectObject : EvolutionEffectObject` overrides `EvolutionEffectAnimation` (early `yield break` when `!showCutInAnimation`, `animTime=2f`, delegate to `base`), plus `Shake`/`PlaySlashSE`/`ShakeIEnumerator` (DOTween shake, SE playback). TO-BE comment-only. Purely Unity animation/audio — deferral defensible, not ported.

### 77. `Script/CardEffectCommons/MinMax_DP_Cost_Level/DigivolutionCards/IsMinDigivolutionCards.cs`
- AS-IS (23 lines) | TO-BE (37 lines)
- Verdict: MATCH
- Evidence: Guards match AS-IS exactly (null, TopCard/InstanceId, `OwnerId!=owner`, `!IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, TopCard)`, `condition`), correctly OMITS the DP `!HasDP&&BaseDP<=0` guard (absent in AS-IS for this card-count variant). Scan mirrors `GetBattleAreaDigimons().Filter(condition).Map(DigivolutionCards.Count)` via `GetCards(...).Where(IsDigimon && cond).Select(...DigivolutionCards.Count)`; `PermanentOfThisCard()` hop is substrate indirection. Final `counts.Count>=1 && ...Count == counts.Min()` equivalent.

### 78. `Script/CardEffects/AddAssemblyConditionClass.cs`
- AS-IS (27 lines) | TO-BE (33 lines)
- Verdict: MATCH
- Evidence: Identical — `Func<CardSource,AssemblyCondition> _getAssemblyCondition`, `SetUp...`, `GetAssemblyCondition` triple guard `cardSource!=null`→`!=null`→`(cardSource)!=null` returning `_getAssemblyCondition(cardSource)` (evaluated twice, verbatim), else null.

### 79. `Script/CardEffects/AddDigiXrosConditionClass.cs`
- AS-IS (27 lines) | TO-BE (33 lines)
- Verdict: MATCH
- Evidence: Identical — `Func<CardSource,DigiXrosCondition> _getDigiXrosCondition`, `SetUp...`, `GetDigiXrosCondition` same triple null-guard + double-invocation return, else null.

### 80. `Script/CardEffects/AddAppFusionConditionClass.cs`
- AS-IS (26 lines) | TO-BE (33 lines)
- Verdict: MATCH
- Evidence: Identical — `Func<CardSource,AppFusionCondition> _getAppFusionCondition`, `SetUp...`, `GetAppFusionCondition` same guards returning delegate result, else null.

### 81. `Script/CardEffects/AddBurstDigivolutionConditionClass.cs`
- AS-IS (26 lines) | TO-BE (33 lines)
- Verdict: MATCH
- Evidence: Identical — `Func<CardSource,BurstDigivolutionCondition> _getBurstDigivolutionCondition`, `SetUp...`, `GetBurstDigivolutionCondition` same triple guard + double-invocation, else null.

### 82. `Script/CardEffects/AddLinkConditionClass.cs`
- AS-IS (26 lines) | TO-BE (33 lines)
- Verdict: MATCH
- Evidence: Identical — `Func<CardSource,LinkCondition> _getLinkCondition`, `SetUp...`, `GetLinkCondition` same `cardSource!=null`→`!=null`→`(cardSource)!=null` return pattern, else null.

### 83. `Script/CardEffects/ChangeCardLevelClass .cs`
- AS-IS (27 lines) | TO-BE (32 lines)
- Verdict: MATCH
- Evidence: Filename with intentional space before `.cs` exists on both sides. Body identical — `Func<CardSource,int,int> GetLevel = null`, `SetUpChangeCardLevelClass`, `GetCardLevel(int level, CardSource card)` with `card!=null`→`GetLevel!=null`→`level = GetLevel(card, level)` then `return level`. Only drops Photon/UnityEngine usings.

### 84. `Script/GSSReader.cs`
- AS-IS (52 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — not ported (skeleton placeholder only).
- Evidence: AS-IS `MonoBehaviour` with substantive logic: fields `SheetID`/`SheetName`, props `IsLoading`/`Datas`, coroutine `GetFromWeb()` (Google-Sheets gviz CSV URL, `UnityWebRequest.Get`, `request.result == ConnectionError` check, `OnLoadEnd`), `Reload()`, static `ConvertCSVtoJaggedArray` (skip header, split `,`, trim `"`). TO-BE only migration-scaffold comments. No AS-IS symbol present.

### 85. `Script/CardEffectCommons/MinMax_DP_Cost_Level/Level/IsMaxLevel.cs`
- AS-IS (23 lines) | TO-BE (34 lines)
- Verdict: MATCH
- Evidence: `CardEffectCommons.IsMaxLevel` present, equivalent guards, final `Levels.Count >= 1 && permanent.Level == Levels.Max()`. Substrate translations faithful: `TopCard == null`→`InstanceId.IsEmpty`, `TopCard.Owner != owner`→`OwnerId != owner`, `GetBattleAreaDigimons().Filter(HasLevel)`→`GetCards(owner, BattleArea).Select(new Permanent).Where(IsDigimon && HasLevel)`. Helper swap `IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, permanent.TopCard)`→`IsPermanentExistsOnBattleAreaDigimon(permanent)` provably equivalent: the only extra owner-variant predicate `IsOwnerPermanent(permanent, card)` tests `permanent.TopCard.Owner == card.Owner`, and here `card == permanent.TopCard` so tautologically true (verified GameContextDeterminarion.cs:388-527).

### 86. `Script/CardEffects/ChangeBaseCardColorClass.cs`
- AS-IS (26 lines) | TO-BE (31 lines)
- Verdict: MATCH
- Evidence: `ChangeBaseCardColorClass : ICardEffect, IChangeBaseCardColorEffect`, `SetUp...`, `Func` prop `ChangeBaseCardColors`, `GetBaseCardColors` nested `if (ChangeBaseCardColors != null) { if (cardSource != null) {...} }` byte-identical. Only namespace + using.

### 87. `Script/CardEffectCommons/CanUseEffects/PermanentEnterField/WhenDigivolving.cs`
- AS-IS (26 lines) | TO-BE (30 lines)
- Verdict: MATCH
- Evidence: `CanTriggerWhenDigivolving` → `CanTriggerOnEnterField(hashtable, card, true, rootCondition)` and `CanTriggerWhenPermanentDigivolving` → `CanTriggerOnPermanentEnterField(..., true, ...)` identical incl. `true` digivolve flag and default `rootCondition = null`. Only namespace/using.

### 88. `Script/CardEffects/ChangeCardColorClass.cs`
- AS-IS (25 lines) | TO-BE (31 lines)
- Verdict: MATCH
- Evidence: `ChangeCardColorClass : ICardEffect, IChangeCardColorEffect`, `SetUp...`, `Func` prop `ChangeCardColors`, `GetCardColors` identical nested null-guard. Verbatim; only namespace/using.

### 89. `Script/CardEffects/ChangeDPDeleteEffectMaxDPClass.cs`
- AS-IS (25 lines) | TO-BE (31 lines)
- Verdict: MATCH
- Evidence: `_changeMaxDP` (=null), `SetUp...`, `GetMaxDP(int, ICardEffect)` with nested `if (cardEffect != null) { if (_changeMaxDP != null) { maxDP = _changeMaxDP(maxDP, cardEffect); } }` identical. Only namespace/using.

### 90. `Script/CardEffectCommons/CanUseEffects/PermanentEnterField/OnPlay.cs`
- AS-IS (25 lines) | TO-BE (29 lines)
- Verdict: MATCH
- Evidence: `CanTriggerOnPlay` → `CanTriggerOnEnterField(hashtable, card, false, rootCondition)` and `CanTriggerOnPermanentPlay` → `CanTriggerOnPermanentEnterField(..., false, ...)` identical incl. `false` play flag. Only namespace/using.

### 91. `Script/CardEffects/AddMaxTrashCountDigiXrosClass.cs`
- AS-IS (24 lines) | TO-BE (30 lines)
- Verdict: MATCH
- Evidence: `_getMaxTrashCount`, `SetUp...`, `GetMaxTrashCount` returning `_getMaxTrashCount(cardSource)` in nested null guards with `return 0;` fallback identical. Only namespace/using.

### 92. `Script/CardEffects/AddMaxUnderTamerCountDigiXrosClass.cs`
- AS-IS (24 lines) | TO-BE (30 lines)
- Verdict: MATCH
- Evidence: Identical incl. AS-IS lowercase method `getMaxUnderTamerCount` (preserved, not corrected), `_getMaxUnderTamerCount`, `SetUp...`, nested null-guard body with `return 0;`. Only namespace/using.

### 93. `Script/CardEffects/ChangeTraitsClass.cs`
- AS-IS (24 lines) | TO-BE (30 lines)
- Verdict: MATCH
- Evidence: `ChangeTraitsClass : ICardEffect, IChangeTraitsEffect`, `SetUp...`, preserved-misspelling prop `changeeTraits`, method `ChangTraits` (also preserved misspelling) with `if (changeeTraits != null) Traits = changeeTraits(cardSource, Traits);` identical. Only namespace/using.

### 94. `Script/CardEffectCommons/ShowReducedCost.cs`
- AS-IS (31 lines) | TO-BE (21 lines)
- Verdict: MATCH
- Evidence: AS-IS `ShowReducedCost(Hashtable)` is a coroutine whose entire body is UI presentation with no state mutation — fetches `PlayCardClass`/`Card`/`Permanents`, calls `GManager.instance.memoryObject.ShowMemoryPredictionLine(...)`, `yield return new WaitForSeconds(0.2f)`. TO-BE is `async Task` no-op (`await Task.CompletedTask`). Argument `Card.PayingCost(..., checkAvailability: false)`/`ExpectedMemory(...)` is read-only cost computation feeding the display only — no side effect lost. Faithful headless substrate translation.

### 95. `Script/CardEffects/ChangeBaseCardNameClass.cs`
- AS-IS (23 lines) | TO-BE (29 lines)
- Verdict: MATCH
- Evidence: `ChangeBaseCardNameClass : ICardEffect, IChangeBaseCardNameEffect`, `SetUpChangeBaseCardNamesClass`, `changeBaseCardNames`, `ChangeBaseCardNames` with single `if (changeBaseCardNames != null) BaseCardNames = changeBaseCardNames(cardSource, BaseCardNames);` identical. Only namespace/using.

### 96. `Script/CardEffects/ChangeCardLevelForAssemblyClass.cs`
- AS-IS (23 lines) | TO-BE (29 lines)
- Verdict: MATCH
- Evidence: `ChangeCardLevelForAssemblyClass : ICardEffect, IChangeCardLevelForAssemblyEffect`, `SetUp...`, `changeCardLevel` (`Func<CardSource, List<int>, List<int>>`), `ChangeCardLevelForAssembly` body identical. Only namespace/using.

### 97. `Script/CardEffects/ChangeCardNamesClass.cs`
- AS-IS (23 lines) | TO-BE (29 lines)
- Verdict: MATCH
- Evidence: `ChangeCardNamesClass : ICardEffect, IChangeCardNamesEffect`, `SetUp...`, field `_changeCardNames = null`, `ChangeCardNames` with `if (_changeCardNames != null) cardNames = _changeCardNames(cardSource, cardNames);` identical. Only namespace/using.

### 98. `Script/CardEffects/ChangeCardNamesForDigiXrosClass.cs`
- AS-IS (23 lines) | TO-BE (29 lines)
- Verdict: MATCH
- Evidence: `ChangeCardNamesForDigiXrosClass : ICardEffect, IChangeCardNamesForDigiXrosEffect`, `SetUp...`, prop `changeCardNames`, `ChangeCardNamesForDigiXros` body identical. Only namespace/using.

### 99. `Script/CardEffects/DisableEffectClass.cs`
- AS-IS (24 lines) | TO-BE (28 lines)
- Verdict: MATCH
- Evidence: `DisableEffectClass : ICardEffect, IDisableCardEffect`, prop `DisableCondition`, `SetUp...`, `IsDisabled` with `if (DisableCondition(cardEffect)) return true; return false;` identical (AS-IS invokes `DisableCondition` without null guard — preserved verbatim). Only namespace/using.

### 100. `Script/CardEffectCommons/CanUseEffects/WhenLoseSecurity.cs`
- AS-IS (24 lines) | TO-BE (27 lines)
- Verdict: MATCH
- Evidence: `CanTriggerWhenLoseSecurity(Hashtable, Func<Player, bool>)` identical: `GetPlayerFromHashtable`, `if (Player != null) { if (playerCondition == null || playerCondition(Player)) return true; } return false;` — short-circuit preserved. Only namespace/using.

### 101. `Script/Networking/GamePacketFactory.cs`
- AS-IS (44 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — not ported (skeleton placeholder only).
- Evidence: AS-IS static factory with two dictionaries (`Factories<byte,Func<byte[],IGamePacket>>`, `IdLookup<Type,byte>`), `NextID` counter, three methods: `Register<T>` (dedup via `IdLookup.ContainsKey`, `NextID++`, register factory), `Create(byte, byte[])` (`TryGetValue` else `null`), `GetId(Type)` (throws `InvalidOperationException` when unregistered). TO-BE only scaffold comments. Every AS-IS symbol absent.

### 102. `Script/CardEffects/ChangeEndTurnMinMemoryClass.cs`
- AS-IS (22 lines) | TO-BE (28 lines)
- Verdict: MATCH
- Evidence: `ChangeEndTurnMinMemoryClass : ICardEffect, IChangeEndTurnMinMemoryEffect`, field `_changetMinMemory = null` (misspelling preserved), `SetUp...`, `GetMinMemory(int)` with `if (_changetMinMemory != null) minMemory = _changetMinMemory(minMemory);` identical. Only namespace/using.

### 103. `Script/SkillInfo.cs`
- AS-IS (15 lines) | TO-BE (35 lines)
- Verdict: MATCH
- Evidence: Small pure-data holder. AS-IS `SkillInfo` (global namespace) has 3-arg ctor assigning `CardEffect`/`Hashtable`/`Timing` and three auto-props (`ICardEffect CardEffect`, `Hashtable Hashtable`, `EffectTiming Timing`) — all reproduced verbatim. Only difference: `namespace ...Script.CardEffectCommons;` + header comments. Type shape and member set identical.

### 104. `Script/CardEffectCommons/IsDigivolvedByTheEffect.cs`
- AS-IS (23 lines) | TO-BE (24 lines)
- Verdict: MATCH
- Evidence: `IsDigivolvedByTheEffect(Permanent, CardSource, ICardEffect)` preserves nested `IsPermanentExistsOnBattleArea(permanent)`→TopCard-equals-cardSource→`permanent.DigivolvingEffect == cardEffect`→true. AS-IS reference-equality `permanent.TopCard == cardSource`→`InstanceId == InstanceId` (with added `!= null` guards) — faithful substrate view-equality adaptation. Only behavioral edge is degenerate both-null (AS-IS true vs TO-BE false), unreachable since on-battle-area permanent has non-null TopCard.

### 105. `Script/AutomaticOrder/StartTurnTamerMemory.cs`
- AS-IS (39 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — unported skeleton; genuine CoreRule logic gap.
- Evidence: AS-IS `AutomaticOrder.GetSkillIndexAutomaticOrder(List<SkillInfo>)` — deterministic ordering that clones the list, filters skills whose `CardEffect.EffectName.Contains("Set Memory to ")`, hoists those "Set Memory" tamer skills to the front via `Concat`, returns the original index of the first resulting skill (or 0). TO-BE is 7-line skeleton. Logic not ported anywhere; `MultipleSkills.cs:377` explicitly states "AutomaticOrder / autoEffectOrder has no mirror — always route to the port". Deterministic Set-Memory-tamer prioritization omitted — genuine behavior gap.

### 106. `Script/CardEffectCommons/CanUseEffects/OnAttackTargetSwitch.cs`
- AS-IS (21 lines) | TO-BE (24 lines)
- Verdict: MATCH
- Evidence: `CanTriggerOnAttackTargetSwitch(Hashtable, CardSource)` → `CanTriggerOnPermanentAttackTargetSwitch(hashtable, permanent => permanent.cardSources.Contains(card))`, and `CanTriggerOnPermanentAttackTargetSwitch` → `CanTriggerOnPermanentAttack`. Character-identical; only `static`/namespace.

### 107. `Script/CardEffectCommons/CanUseEffects/OnUnsuspend.cs`
- AS-IS (21 lines) | TO-BE (24 lines)
- Verdict: MATCH
- Evidence: `CanTriggerWhenSelfPermanentUnsuspends` → `CanTriggerWhenPermanentUnsuspends(hashtable, permanent => permanent.cardSources.Contains(card))` and `CanTriggerWhenPermanentUnsuspends` → `CanTriggerWhenPermanentSuspends(hashtable, permanentCondition)`. Identical; only namespace/`static`.

### 108. `Script/CardEffectCommons/CanUseEffects/WhenDiscardSecurity.cs`
- AS-IS (21 lines) | TO-BE (24 lines)
- Verdict: MATCH
- Evidence: `CanTriggerOnTrashSelfSecurity` → `CanTriggerOnTrashSecurity(hashtable, cardEffectCondition, cardSource => cardSource == card)` and `CanTriggerOnTrashSecurity` → `CanTriggerOnTrashHand(hashtable, cardEffectCondition, cardCondition)`. Identical; only namespace/`static`.

### 109. `Script/MainPhaseAction/PassAction.cs`
- AS-IS (28 lines) | TO-BE (17 lines)
- Verdict: MATCH
- Evidence: Only game logic `Execute(TurnStateMachine) => stateMachine.PassTurn()` faithfully ported as `await stateMachine.PassTurn()` (void→async Task). Dropped `PassAction(byte[])`, `Deserialize` (empty), `Serialize` (`return null`) are Photon transport — no game logic lost.

### 110. `Script/CardDistributionTab.cs`
- AS-IS (36 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — unported skeleton; AS-IS carries real logic.
- Evidence: AS-IS `CardDistributionTab : MonoBehaviour` `SetCardDistributionTab(DeckData)`: null-guards `CardCondition`, computes `deckData.AllDeckCards().Count(CardCondition)`, derives `ratio = count/MaxCount` clamped to 1, sets `CountText.text`, resizes bar `sizeDelta.y = maxLength * ratio`. TO-BE is 7-line skeleton; class absent. UI-layer, but genuine logic vs empty stub.

### 111. `Script/CardEffects/VortexCanAttackPlayersClass.cs`
- AS-IS (18 lines) | TO-BE (24 lines)
- Verdict: MATCH
- Evidence: `VortexCanAttackPlayersClass : ICardEffect, IVortexCanAttackPlayersEffect` with `_attackerCondition`, `SetUp...(Func<Permanent,bool>)`, `VortexCanAttackPlayersPermanent(Permanent)` returning `CardEffectCommons.IsPermanentExistsOnBattleArea(attacker) && (_attackerCondition == null || _attackerCondition(attacker))`. Byte-identical; only namespace/usings.

### 112. `Script/JogressEffectObject.cs`
- AS-IS (30 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — unported skeleton; AS-IS carries real (presentation) control flow.
- Evidence: AS-IS `JogressEffectObject : EvolutionEffectObject` overrides `EvolutionEffectAnimation` (`IEnumerator`): early `yield break` when `!ContinuousController.instance.showCutInAnimation`, `animTime = 1.65f`, assigns `jogressEvoRootImages[i].sprite` from each root's `CardSprite`, yields to `base`. TO-BE 7-line skeleton; class absent. UI/animation, but genuine control flow.

### 113. `Script/CEntity_Effect.cs`
- AS-IS (29 lines) | TO-BE (7 lines path skeleton)
- Verdict: MATCH
- Evidence: Path-file is skeleton, but `CEntity_Effect` base class faithfully mirrored in `CardEffectInterfaces.cs:671-698`: `virtual CardEffects(EffectTiming, CardSource)` returning `new List<ICardEffect>()`, `GetCardEffects(...)` returning `CardEffects(...).Filter(cardEffect => cardEffect != null)`, static `isExistOnField(CardSource)`. AS-IS `card.PermanentOfThisCard() != null` rendered as `!card.PermanentOfThisCard().IsEmpty` (grounded substrate adaptation; PermanentView sentinel non-null). All three symbols preserved.

### 114. `Script/CardEffectCommons/CanUseEffects/IgnoreBattle.cs`
- AS-IS (14 lines) | TO-BE (17 lines)
- Verdict: MATCH
- Evidence: `CanUseIgnoreBattle(Hashtable, CardSource) => CanTriggerOptionMainEffect(hashtable, card)`. Identical; only namespace/`static`.

### 115. `Script/CardEffectCommons/CanUseEffects/OnEndAttack.cs`
- AS-IS (14 lines) | TO-BE (17 lines)
- Verdict: MATCH
- Evidence: `CanTriggerOnEndAttack(Hashtable, CardSource) => CanTriggerOnAttack(hashtable, card)`. Identical; only namespace/`static`.

### 116. `Script/CardEffectCommons/CanUseEffects/SecurityEffect.cs`
- AS-IS (14 lines) | TO-BE (17 lines)
- Verdict: MATCH
- Evidence: `CanTriggerSecurityEffect(Hashtable, CardSource) => CanTriggerOptionMainEffect(hashtable, card)`. Identical; only namespace/`static`.

### 117. `Script/CardEffectCommons/CanUseEffects/WhendAddSecurity.cs`
- AS-IS (14 lines) | TO-BE (17 lines)
- Verdict: MATCH
- Evidence: `CanTriggerWhenAddSecurity(Hashtable, Func<Player,bool>) => CanTriggerWhenLoseSecurity(hashtable, playerCondition)` — deliberate AS-IS delegation to the *LoseSecurity* helper preserved verbatim. Identical; only namespace/`static`.

### 118. `Script/PlayerSelection/ValueSelection.cs`
- AS-IS (24 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — unported; concrete selection type absent.
- Evidence: AS-IS `ValueSelection : IPlayerSelection` has `_value`, two ctors (`int`, and `bool`→`value ? 1 : 0`), `ValueAsInt() => _value`, `ValueAsBool() => _value != 0`. TO-BE 7-line skeleton; class exists nowhere in TO-BE — only referenced in comments (`SelectCountEffect.cs`, `TurnStateMachine.cs:357`, `OptionalSkill.cs:81`). Selection model replaced by agent-LegalAction mechanism; concrete type unported.

### 119. `Script/CardEffectCommons/CanUseEffects/CanUnsuspend.cs`
- AS-IS (14 lines) | TO-BE (16 lines, comment-only)
- Verdict: MATCH
- Evidence: TO-BE path is comment-only, but `CanUnsuspend(Permanent)` genuinely exists at `CardEffectCommons.cs:3867`: returns `IsSuspended(top, permanent.InstanceId) && permanent.CanUnsuspend` guarded by null/empty check. AS-IS `permanent != null && permanent.TopCard != null && permanent.IsSuspended && permanent.CanUnsuspend`; `TopCard != null` folded into substrate null-guard and `IsSuspended`. Grounded in real ported code.

### 120. `Script/Networking/GamePacketRegistration.cs`
- AS-IS (15 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — unported skeleton (Photon transport).
- Evidence: AS-IS `PacketRegistration.RegisterAll()` (`[RuntimeInitializeOnLoadMethod]`) registers six `GamePacketFactory` byte[]-ctor factories (AttackPermanentAction, ActivateCardAction, ActivatePermanentAction, CheatAction, PlayCardAction, PassAction). TO-BE 7-line skeleton; `PacketRegistration`/`RegisterAll` absent. Intentionally stripped by headless design but not mirrored.

### 121. `Script/LoadJSON_CardEntity.cs`
- AS-IS (14 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — unported (data container).
- Evidence: AS-IS `LoadJSON_CardEntity : ScriptableObject` (namespace `DCGO.CardEntities`, `[CreateAssetMenu]`) with fields `prevCardIndex`, `setCardIndex`, `[HideInInspector] promoCardIndex`. TO-BE 7-line skeleton; class absent. Trivial Unity ScriptableObject, but type/fields unported.

### 122. `Script/Utils.cs`
- AS-IS (9 lines) | TO-BE (12 lines)
- Verdict: MATCH
- Evidence: Both declare `static class Utils` with `PluralFormSuffix(int count) => count >= 2 ? "s" : "";`. Byte-identical body; TO-BE only adds namespace + XML doc comments.

### 123. `Script/MainPhaseAction/MainPhaseAction.cs`
- AS-IS (6 lines) | TO-BE (14 lines)
- Verdict: MATCH
- Evidence: AS-IS `abstract class MainPhaseAction : IGamePacket` with abstract `Execute(TurnStateMachine)`, `Deserialize(byte[])`, `Serialize()`. TO-BE preserves abstract `Execute` (void→`Task`). Dropped `IGamePacket` base + two serialize/deserialize abstracts are Photon transport half, intentionally stripped. No game logic lost (abstract transport signatures, no bodies).

### 124. `Script/PlayerSelection/PermanentSelection.cs`
- AS-IS (11 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — unported; concrete selection type absent.
- Evidence: AS-IS `PermanentSelection : IPlayerSelection` has `bool[] IsTurnPlayerList` and `int[] PermanentIDList` auto-props (private set) + ctor assigning both. TO-BE 7-line skeleton; class/members absent (confirmed by symbol search). Selection model replaced; concrete type unported.

### 125. `Script/PlayerSelection/CardSelection.cs`
- AS-IS (9 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — unported; concrete selection type absent.
- Evidence: AS-IS `CardSelection : IPlayerSelection` has `int[] CardIDList` (private set) + ctor assigning it. TO-BE 7-line skeleton; class/`CardIDList` absent. Unported concrete selection type.

### 126. `Script/LoadCSV_CardEntity.cs`
- AS-IS (8 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — unported (data container).
- Evidence: AS-IS `LoadCSV_CardEntity : ScriptableObject` (`[CreateAssetMenu]`) with single `public TextAsset csvFile;`. TO-BE 7-line skeleton; class absent. Trivial Unity ScriptableObject, but type/field unported.

### 127. `Script/PlayerSelection/IPlayerSelection.cs`
- AS-IS (6 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — unported (marker interface).
- Evidence: AS-IS declares `public interface IPlayerSelection {}` — empty marker. TO-BE 7-line skeleton; interface declared nowhere in TO-BE (only referenced by name in AS-IS-describing comments). Carries no logic, but declaration absent; implementers (Value/Card/PermanentSelection) likewise unported.

### 128. `Script/Networking/IGamePacket.cs`
- AS-IS (5 lines) | TO-BE (7 lines)
- Verdict: **PROBLEM** — unported (Photon transport contract).
- Evidence: AS-IS declares `public interface IGamePacket { byte[] Serialize(); void Deserialize(byte[] bytes); }`. TO-BE 7-line skeleton; interface absent (consistent with MainPhaseAction dropping its `IGamePacket` base). Photon serialization transport contract, intentionally stripped but not mirrored.
