# Mutation-Helper Bridge Mapping (AS-IS `CardEffectCommons.*` → mirror substrate)

Investigation for the DCGO2 effect-model rebuild. Goal: enumerate every AS-IS
`CardEffectCommons.<Helper>(...)` mutation coroutine that ported cards actually
call, and map each to its mirror substrate equivalent so that thin
AS-IS-signature `Task` overloads can be added that delegate to the verified
substrate implementation.

## Derivation

```
# AS-IS coroutine names (125 total)
grep -rhE 'public static IEnumerator [A-Za-z_]+\(' \
  DCGO/Assets/Scripts/Script/CardEffectCommons*.cs DCGO/Assets/Scripts/Script/CardEffectCommons/ -r \
  --binary-files=text | grep -oE 'IEnumerator [A-Za-z_]+\(' | sort -u

# Card-called names (259 unique) — CardEffectCommons.<Name>( sites under card files
grep -rhoE 'CardEffectCommons\.[A-Za-z_]+\(' DCGO/Assets/Scripts/CardEffect/ --include=*.cs \
  --binary-files=text | sed 's/CardEffectCommons\.//' | tr -d '(' | sort

# Intersection = 91 rows below
```

All greps used `--binary-files=text` (many AS-IS files are non-UTF8 and are
silently skipped as "binary" otherwise — known pitfall, see
`grep-binary-skip-pitfall` memory).

## Summary by gap classification (91 helpers)

| Class | Count | Meaning |
|---|---|---|
| `SAME-NAME-DIFF-SIG` | 84 | Mirror has a same-named method; only mechanical signature differences (`ICardEffect activateClass`→`CardSource sourceCard`, `IEnumerator`→`Task`/`bool`/`void`, `List<T>`→`IReadOnlyList<T>`) — quick-win delegation targets. A few have real caveats flagged inline (dropped params, wrong-shape name collision, architecture mismatch) — see ⚠️ markers. |
| `EXISTS-AS-IS-COMPAT` | 3 | Mirror signature already effectively AS-IS-compatible (param list ~identical modulo the universal type swaps) — trivial or near-zero-effort wrapper. |
| `NO-MIRROR` | 3 | No usable substrate equivalent exists (explicit STOP stub, or the mirror only has a differently-shaped declarative factory). |
| `UI-ONLY` | 1 | AS-IS body is pure presentation with no game-state mutation — no substrate needed. |

**NO-MIRROR list**: `DNADigivolveWithHandOrTrashCardIntoHandOrTrash`, `PlayOptionCards`, `RevealDeckTopCardsAndProcessForAll`

**UI-ONLY list**: `ShowReducedCost`

**Highest-priority caveats inside SAME-NAME-DIFF-SIG** (real behavior at stake, not just types):
- `SimplifiedRevealDeckTopCardsAndSelect` (409 calls, highest call count in the whole set) — mirror is an architecturally different declarative `IActivatedCardEffect` factory (not an imperative `Task`), and **6 of 11 AS-IS params are unmodeled** (`canTargetCondition_ByPreSelecetedList`, `canEndSelectCondition`, `canNoSelect`, `canEndNotMax`, `mutualConditions`, `isSendAllCardsToSamePlace`, `isOpponentDeck`, `revealedCardsCoroutine` — several used by 150+ of 362 caller files). Needs real design work, not a thin wrapper.
- `TrashDigivolutionCardsAndProcessAccordingToResult` (9 calls) — the same-named mirror method is a **different shape** (top/bottom count-based) than AS-IS (arbitrary `List<CardSource>`-based); the correct substrate is `Headless.Runtime.DigivolutionStackHelpers.TrashSpecificSourcesAsync` instead.
- `TrashDigivolutionCardsFromTopOrBottom` (121 calls) — mirror is missing the optional `cardCondition` filter param, confirmed used by real callers (ST24 series et al.).
- `PlayOptionCards`/`RevealDeckTopCardsAndProcessForAll` above are NO-MIRROR but have plausible declarative-factory candidates noted in their sections.
- `PlaceDelayOptionCards` / `PlayToken` family (all 14 `PlayXToken` helpers) — mirror's shared `PlayToken` primitive drops the AS-IS field-capacity check (`fieldCardFrames` empty-slot count vs `quantity`) and doesn't call `CanPlayAsNewPermanent` at all; pre-existing substrate gap, not introduced by wrapping.
- `PlayPermanentCards` (977 calls, highest raw count) — mirror's `CanPlayAsNewPermanent` receives `cardEffect: null` (stubbed with `_ = cardEffect;`) instead of the real per-target-frame `cardEffect`-gated filtering AS-IS performs — latent gap.

---

## SAME-NAME-DIFF-SIG (84) — quick-win delegation targets

### ActivateMainOfOptionSide
- AS-IS: `public static IEnumerator ActivateMainOfOptionSide(CardSource card, ICardEffect activateClass, Func<ICardEffect, IEnumerator> afterMainEffect = null, bool asEffectOfThisDigimon = false)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:733
- Card-call count: 1
- Mirror: `public static Task<int> ActivateMainOfOptionSide(CardSource card, CardSource sourceCard, CancellationToken cancellationToken = default)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2833 (Task<int>)
- ⚠️ Mirror silently drops `afterMainEffect` and `asEffectOfThisDigimon` — wrapper must reconcile or design-item the drop.
- activateClass usage: real logic is on `mainActivateClass` (from `OptionMainEffect(card)`); the `activateClass` param itself is only forwarded verbatim into `afterMainEffect(activateClass)` — pass-through only for this method.

### AddSelfDeleteEffect
- AS-IS: `public static IEnumerator AddSelfDeleteEffect(Permanent permanent, DeleteTiming deleteTiming, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/DeleteSelf.cs:7
- Card-call count: 4
- Mirror: `public static void AddSelfDeleteEffect(Permanent? permanent, string deleteTiming, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2663 (void)
- ⚠️ `DeleteTiming` enum → `string` needs a mapping table, not just a cast.
- activateClass usage: real — feeds `PermanentEffectFactory.DeleteSelfEffect` for effect identity/chaining; not UI (only the message string is UI).

### BecomeDigimonThatCantDigivolve
- AS-IS: `public static IEnumerator BecomeDigimonThatCantDigivolve(Permanent targetPermanent, int DP, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/TamerBecomesDigimonThatCanNotDigivolve.cs:10
- Card-call count: 11
- Mirror: `public static bool BecomeDigimonThatCantDigivolve(Permanent? targetPermanent, int DP, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2685 (bool)
- Clean 1:1 param mapping.
- activateClass usage: real — drives 3 chained factory grants (TreatAsDigimon/ChangeBaseDP/CanNotDigivolve); only trailing `CreateBuffEffect` is UI.

### BlitzProcess
- AS-IS: `public static IEnumerator BlitzProcess(CardSource cardSource, ICardEffect activateClass, Func<IEnumerator> beforeOnAttackCoroutine = null)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Blitz.cs:31
- Card-call count: 5
- Mirror: `public static bool BlitzProcess(CardSource cardSource)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2608 (bool)
- ⚠️ Mirror drops both `activateClass` (used for `CanActivateBlitz` gate) and `beforeOnAttackCoroutine`. Also: `src/HeadlessDCGO.Engine/.../CardEffectFactory/KeyWordEffects/Blitz.cs:123` still has a stale 3-arg call site that doesn't match this 1-arg substrate method — appears to be a legacy pre-cutover call, worth checking before building on this.
- activateClass usage: real in AS-IS gate `CanActivateBlitz(cardSource, activateClass)`; not UI.

### BouncePeremanentAndProcessAccordingToResult
- AS-IS: `public static IEnumerator BouncePeremanentAndProcessAccordingToResult(List<Permanent> targetPermanents, ICardEffect activateClass, IEnumerator successProcess, IEnumerator failureProcess)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:489
- Card-call count: 23
- Mirror: `public static async Task BouncePeremanentAndProcessAccordingToResult(IReadOnlyList<Permanent> targetPermanents, CardSource sourceCard, Func<Task>? successProcess, Func<Task>? failureProcess)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:214 (Task)
- ⚠️ AS-IS `successProcess`/`failureProcess` are already-started `IEnumerator` instances; mirror wants `Func<Task>` factories — wrapper must adapt IEnumerator→Task-factory, not just re-type.
- activateClass usage: real — feeds `CardEffectHashtable(activateClass)` into `HandBounceClaass` ctor.

### ChangeBaseDigimonDP
- AS-IS: `public static IEnumerator ChangeBaseDigimonDP(Permanent targetPermanent, int changeValue, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/ChangeOriginDP.cs:10
- Card-call count: 6
- Mirror: `public static bool ChangeBaseDigimonDP(Permanent? targetPermanent, int changeValue, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3392 (bool)
- Clean 1:1 mapping.
- activateClass usage: real — `CanNotBeAffected(activateClass)` immunity gate; only buff/debuff VFX branch is UI.

### ChangeDigimonDP
- AS-IS: `public static IEnumerator ChangeDigimonDP(Permanent targetPermanent, int changeValue, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/ChangeDP.cs:10
- Card-call count: 584 (highest-frequency `Gain/Change`-style helper)
- Mirror: `public static bool ChangeDigimonDP(Permanent? targetPermanent, int changeValue, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:1807 (bool; delegates to private `ChangeDigimonStat` at :1758)
- Clean 1:1 mapping, doc comment says "verbatim verified".
- activateClass usage: real — `CanNotBeAffected(activateClass)` gate reproduced via `ContinuousImmunityGate.BlocksOpponentEffect`; not UI.

### ChangeDigimonDPPlayerEffect
- AS-IS: `public static IEnumerator ChangeDigimonDPPlayerEffect(Func<Permanent, bool> permanentCondition, int changeValue, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangeDP.cs:10
- Card-call count: 28
- Mirror: `public static bool ChangeDigimonDPPlayerEffect(Func<Permanent, bool>? permanentCondition, int changeValue, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:1824 (bool)
- Clean 1:1 mapping.
- activateClass usage: real — `CanNotBeAffected` gate inside `PermanentCondition` closure + owner scope via `sourceCard.Owner`; buff/debuff loop is UI.

### ChangeDigimonSAttack
- AS-IS: two overloads — `(Permanent, int, EffectDuration, ICardEffect)` and `(Permanent, int, EffectDuration, ICardEffect, bool activateAnimation, string hashstring = null)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/ChangeSAttack.cs:10,62
- Card-call count: 163
- Mirror: `public static bool ChangeDigimonSAttack(Permanent? targetPermanent, int changeValue, EffectDuration effectDuration, CardSource sourceCard, bool activateAnimation = true, string? hashstring = null)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:1812 (bool)
- ⚠️ Mirror already collapsed both AS-IS overloads into one method — wrapper needs 2 AS-IS-signature `Task` overloads, both forwarding here.
- activateClass usage: real — same immunity-gate pattern as ChangeDigimonDP; `activateAnimation`/`hashstring` are correctly UI-only and discarded.

### ChangeDigimonSAttackPlayerEffect
- AS-IS: `public static IEnumerator ChangeDigimonSAttackPlayerEffect(Func<Permanent, bool> permanentCondition, int changeValue, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangeSAttack.cs:10
- Card-call count: 12
- Mirror: `public static bool ChangeDigimonSAttackPlayerEffect(Func<Permanent, bool>? permanentCondition, int changeValue, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3353 (bool, delegates to `GainToPlayerScope`)
- Clean 1:1 mapping.
- activateClass usage: real — identical pattern to ChangeDigimonDPPlayerEffect; not UI.

### ChangePlayCostPlayerEffect
- AS-IS: `public static IEnumerator ChangePlayCostPlayerEffect(Func<Permanent, bool> permanentCondition, int changeValue, bool setFixedCost, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangePlayCost.cs:11
- Card-call count: 4
- Mirror: `public static bool ChangePlayCostPlayerEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool setFixedCost, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3372 (bool)
- Exact param-order 1:1 mapping.
- activateClass usage: real — `CanNotBeAffected` gate + `setFixedCost` distinction via dedicated keys; only the `CreateDebuffEffect` loop is UI.

### ChangeSecurityDigimonCardDPPlayerEffect
- AS-IS: `public static IEnumerator ChangeSecurityDigimonCardDPPlayerEffect(Func<CardSource, bool> cardCondition, int changeValue, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangeCardDP.cs:10
- Card-call count: 15
- Mirror: `public static bool ChangeSecurityDigimonCardDPPlayerEffect(Func<CardSource, bool>? cardCondition, int changeValue, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:1456 (bool)
- Exact 1:1 mapping.
- activateClass usage: source-attribution only (`activateClass.EffectSourceCard` → registered as `card`) — no `CanNotBeAffected` gate in this helper; grants are read later by SecurityResolver.

### DNADigivolvePermanentsIntoHandOrTrashCard
- AS-IS: `public static IEnumerator DNADigivolvePermanentsIntoHandOrTrashCard(Func<CardSource, bool> canSelectDNACardCondition, bool payCost, bool isHand, ICardEffect activateClass, Func<Permanent, bool>[] permanentConditions = null, Func<CardSource, IEnumerator> successProcess = null, bool ignoreSelection = false, Func<IEnumerator> failedProcess = null, bool isOptional = true)` — DCGO/Assets/Scripts/Script/CardEffectCommons/DNADigivolveEffects.cs:458
- Card-call count: 55
- Mirror: `public static async Task DNADigivolvePermanentsIntoHandOrTrashCard(Func<CardSource, bool>? canSelectDNACardCondition, bool payCost, bool isHand, CardSource sourceCard, Func<Permanent, bool>[]? permanentConditions = null, Func<CardSource, Task>? successProcess = null, bool ignoreSelection = false, Func<Task>? failedProcess = null, bool isOptional = true, CancellationToken cancellationToken = default)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2259 (Task)
- Near-perfect 1:1, best delegation candidate of the whole set. Note: mirror discards `payCost` at runtime (`_ = payCost`) — predicate-form DNA is treated as cost-0, cost carried by a recipe instead; flag as a behavior nuance.
- activateClass usage: real — resolves acting player and is threaded as `cardEffect:` into selection setups.

### DeckBouncePeremanentAndProcessAccordingToResult
- AS-IS: `public static IEnumerator DeckBouncePeremanentAndProcessAccordingToResult(List<Permanent> targetPermanents, ICardEffect activateClass, IEnumerator successProcess, IEnumerator failureProcess)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:515
- Card-call count: 16
- Mirror: `public static async Task DeckBouncePeremanentAndProcessAccordingToResult(IReadOnlyList<Permanent> targetPermanents, CardSource sourceCard, Func<Task>? successProcess, Func<Task>? failureProcess)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:254 (Task)
- Same IEnumerator→Func<Task> adapter need as BouncePeremanent...
- activateClass usage: real — feeds `CardEffectHashtable(activateClass)` into `DeckBottomBounceClass`.

### DeletePeremanentAndProcessAccordingToResult
- AS-IS: `public static IEnumerator DeletePeremanentAndProcessAccordingToResult(List<Permanent> targetPermanents, ICardEffect activateClass, Func<List<Permanent>, IEnumerator> successProcess, Func<IEnumerator> failureProcess)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:463
- Card-call count: 322 (2nd-highest count in the whole set)
- Mirror: `public static async Task DeletePeremanentAndProcessAccordingToResult(IReadOnlyList<Permanent> targetPermanents, CardSource sourceCard, Func<IReadOnlyList<Permanent>, Task>? successProcess, Func<Task>? failureProcess, CancellationToken cancellationToken = default)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:115 (Task)
- Clean 1:1 shape.
- activateClass usage: real — feeds `CardEffectHashtable(activateClass)` into `DestroyPermanentsClass`, establishing deletion provenance (drives immunity/replacement gating); not UI.

### DigivolveIntoExcecutingAreaCard
- AS-IS: `public static IEnumerator DigivolveIntoExcecutingAreaCard(Permanent targetPermanent, Func<CardSource, bool> cardCondition, bool payCost, (int, Func<CardSource,bool>)? reduceCostTuple, (int, Func<CardSource,bool>)? fixedCostTuple, int ignoreDigivolutionRequirementFixedCost, ICardEffect activateClass, IEnumerator successProcess, bool ignoreSelection = false, IgnoreRequirement ignoreRequirements = IgnoreRequirement.None)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:1106
- Card-call count: 1
- Mirror: `public static async Task DigivolveIntoExcecutingAreaCard(Permanent? targetPermanent, Func<CardSource, bool>? cardCondition, bool payCost, (int, Func<CardSource,bool>?)? reduceCostTuple, (int, Func<CardSource,bool>?)? fixedCostTuple, int ignoreDigivolutionRequirementFixedCost, CardSource sourceCard, Func<Task>? successProcess, bool ignoreSelection = false, IgnoreRequirement ignoreRequirements = IgnoreRequirement.None, CancellationToken cancellationToken = default)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2804 (Task, delegates to private `DigivolveIntoZoneCoreAsync`)
- Clean 1:1 shape.
- activateClass usage: heavily real — `cardEffect:` into `CanPlayCardTargetFrame`, gates `CanNotEvolve`, drives cost-reduction chaining; not UI.

### DigivolveIntoHandOrTrashCard
- AS-IS: `public static IEnumerator DigivolveIntoHandOrTrashCard(Permanent targetPermanent, Func<CardSource, bool> cardCondition, bool payCost, (int,Func<CardSource,bool>)? reduceCostTuple, (int,Func<CardSource,bool>)? fixedCostTuple, int ignoreDigivolutionRequirementFixedCost, bool isHand, ICardEffect activateClass, IEnumerator successProcess, bool ignoreSelection = false, IgnoreRequirement ignoreRequirements = IgnoreRequirement.None, IEnumerator failedProcess = null, bool isOptional = true)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:756
- Card-call count: 342 (3rd-highest count)
- Mirror: `public static Task DigivolveIntoHandOrTrashCard(Permanent? targetPermanent, Func<CardSource, bool>? cardCondition, bool payCost, (int,Func<CardSource,bool>?)? reduceCostTuple, (int,Func<CardSource,bool>?)? fixedCostTuple, int ignoreDigivolutionRequirementFixedCost, bool isHand, CardSource sourceCard, Func<Task>? successProcess, bool ignoreSelection = false, IgnoreRequirement ignoreRequirements = IgnoreRequirement.None, Func<Task>? failedProcess = null, bool isOptional = true, CancellationToken cancellationToken = default)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:1959 (Task, delegates to `DigivolveIntoZoneCoreAsync`)
- Nearly 1:1 param-for-param already.
- activateClass usage: same as DigivolveIntoExcecutingAreaCard — real.

### DrawAndDiscardCards
- AS-IS: `public static IEnumerator DrawAndDiscardCards((Player drawPlayer, Player trashPlayer) player, int drawAmount, int trashAmount, CardSource card, ICardEffect activateClass, Func<CardSource, bool> canTrashTargetCondition = null, Func<List<CardSource>, CardSource, bool> canTargetCondition_ByPreSelecetedList = null, Func<List<CardSource>, bool> canEndSelectCondition = null, bool canNoSelect = false, bool canEndNotMax = false, bool isShowOpponent = true, Func<List<CardSource>, IEnumerator> afterSelectPermanentCoroutine = null)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:1408
- Card-call count: 3
- Mirror: `public static async Task DrawAndDiscardCards((HeadlessPlayerId drawPlayer, HeadlessPlayerId trashPlayer) player, int drawAmount, int trashAmount, CardSource sourceCard, Func<CardSource, bool>? canTrashTargetCondition = null, bool canNoSelect = false, bool canEndNotMax = false, CancellationToken cancellationToken = default)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2706 (Task)
- ⚠️ Mirror drops `canTargetCondition_ByPreSelecetedList`, `canEndSelectCondition`, `isShowOpponent`, `afterSelectPermanentCoroutine` — a full-fidelity wrapper needs a decision on these (STOP/design-item vs. folding `afterSelectPermanentCoroutine` into a post-flush callback).
- activateClass usage: real — `DrawClass(..., activateClass)` for draw provenance, and `cardEffect:` into `selectHandEffect.SetUp` governing discard-selection binding.

### FortitudeProcess
- AS-IS: `public static IEnumerator FortitudeProcess(Hashtable hashtable, CardSource card, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Fortitude.cs:54
- Card-call count: 1
- Mirror: `public static Task FortitudeProcess(CardSource card, CardSource sourceCard) => PlayPermanentCards(new[]{card}, sourceCard, payCost:false, isTapped:false, root: ChoiceZone.Trash, activateETB:true);` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2645 (Task)
- Mirror drops `Hashtable hashtable` — safe, AS-IS body never reads it (dead param).
- activateClass usage: pass-through only — forwarded into `PlayPermanentCards` as effect-chain root, not itself inspected.

### GainAlliance
- AS-IS: `public static IEnumerator GainAlliance(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Alliance.cs:136
- Card-call count: 14
- Mirror: `public static bool GainAlliance(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) => GainKeywordToPermanent(..., ContinuousKeywordGate.Alliance, "gainAlliance");` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3433 (bool)
- activateClass usage: real — `CanNotBeAffected` gate + `rootCardEffect: activateClass` into factory grant; only tail `CreateBuffEffect` is UI.

### GainBarrier
- AS-IS: `public static IEnumerator GainBarrier(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Barrier.cs:65
- Card-call count: 2
- Mirror: `public static bool GainBarrier(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) => GainKeywordToPermanent(..., ContinuousKeywordGate.Barrier, "gainBarrier");` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3461 (bool)
- activateClass usage: real — same pattern as GainAlliance; tail buff visual is UI.

### GainBlocker
- AS-IS: `public static IEnumerator GainBlocker(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Blocker.cs:10
- Card-call count: 87
- Mirror: `public static bool GainBlocker(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) => GainKeywordToPermanent(..., ContinuousKeywordGate.Blocker, "gainBlocker");` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3405 (bool)
- activateClass usage: real — `CanNotBeAffected` gate feeds `BlockerStaticEffect(condition:...)`; tail buff visual is UI.

### GainBlockerPlayerEffect
- AS-IS: `public static IEnumerator GainBlockerPlayerEffect(Func<Permanent, bool> permanentCondition, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Blocker.cs:46
- Card-call count: 6
- Mirror: `public static bool GainBlockerPlayerEffect(Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard) => GainToPlayerScope(effectDuration, sourceCard, "gainBlockerPlayer", permanentCondition, keyword: ContinuousKeywordGate.Blocker);` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3198 (bool)
- Closest 1:1 param match in the Gain* family.
- activateClass usage: real — per-permanent `CanNotBeAffected` folded into `PermanentCondition` closure; buff-visual loop is UI.

### GainCanNotAttack
- AS-IS: `public static IEnumerator GainCanNotAttack(Permanent targetPermanent, Func<Permanent, bool> defenderCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotAttack.cs:10
- Card-call count: 56
- Mirror: `public static bool GainCanNotAttack(Permanent? targetPermanent, Func<Permanent, bool>? defenderCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't attack") => GainRestrictionToPermanent(..., RestrictionHelpers.CannotAttackKey, "gainCanNotAttack", defenderCondition);` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3067 (bool)
- activateClass usage: real — `CanNotBeAffected` gate + `EffectSourceCard` into `CanNotAttackStaticEffect`; tail debuff visual is UI.

### GainCanNotAttackPlayerEffect
- AS-IS: `public static IEnumerator GainCanNotAttackPlayerEffect(Func<Permanent, bool> attackerCondition, Func<Permanent, bool> defenderCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotAttack.cs:10
- Card-call count: 14
- Mirror: `public static bool GainCanNotAttackPlayerEffect(Func<Permanent, bool>? attackerCondition, Func<Permanent, bool>? defenderCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't attack")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3244 (bool, delegates `GainToPlayerScope` with `counterpartPredicate: defenderPredicate`)
- activateClass usage: real — `AttackerCondition` closure gates `CanNotBeAffected` per candidate; not UI.

### GainCanNotBeAttacked
- AS-IS: `public static IEnumerator GainCanNotBeAttacked(Permanent targetPermanent, Func<Permanent, bool> attackerCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeAttacked.cs:10
- Card-call count: 1
- Mirror: `public static bool GainCanNotBeAttacked(Permanent? targetPermanent, Func<Permanent, bool>? attackerCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be attacked") => GainRestrictionToPermanent(..., RestrictionHelpers.CannotBeAttackedKey, "gainCanNotBeAttacked", attackerCondition);` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3082 (bool)
- activateClass usage: real — `CanNotBeAffected` gate; tail buff visual is UI.

### GainCanNotBeBlocked
- AS-IS: `public static IEnumerator GainCanNotBeBlocked(Permanent targetPermanent, Func<Permanent, bool> defenderCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeBlocked.cs:10
- Card-call count: 7
- Mirror: `public static bool GainCanNotBeBlocked(Permanent? targetPermanent, Func<Permanent, bool>? defenderCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be blocked") => GainRestrictionToPermanent(..., RestrictionHelpers.CannotBeBlockedKey, "gainCanNotBeBlocked", defenderCondition);` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3089 (bool)
- activateClass usage: real — same pattern; tail buff visual is UI.

### GainCanNotBeDeletedByBattle
- AS-IS: `public static IEnumerator GainCanNotBeDeletedByBattle(Permanent targetPermanent, Func<Permanent,Permanent,Permanent,CardSource,bool> canNotBeDestroyedByBattleCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByBattle.cs:11
- Card-call count: 16
- Mirror: `public static bool GainCanNotBeDeletedByBattle(Permanent targetPermanent, Func<Permanent,Permanent,Permanent,CardSource,bool>? canNotBeDestroyedByBattleCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:47 (bool, bespoke path not through GainRestrictionToPermanent)
- activateClass usage: real — `EffectSourceCard` + `CanNotBeAffected` gates both grant-time short-circuit and the live re-check condition (`ContinuousImmunityGate.BlocksOpponentEffect`).

### GainCanNotBeDeletedByEffect
- AS-IS: `public static IEnumerator GainCanNotBeDeletedByEffect(Permanent targetPermanent, Func<ICardEffect, bool> cardEffectCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByEffect.cs:10
- Card-call count: 11
- Mirror: `public static bool GainCanNotBeDeletedByEffect(Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be deleted by effects")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3345 (bool, via `GainRestrictionToPermanent`)
- ⚠️ AS-IS 4th param tests the causing *effect instance* (`Func<ICardEffect,bool>`); mirror tests the causing *source card* (`Func<CardSource,bool>`) — wrapper needs an adapter closure, not a straight pass-through.
- activateClass usage: real — same `EffectSourceCard`/`CanNotBeAffected` guard.

### GainCanNotBeDeletedPlayerEffect
- AS-IS: `public static IEnumerator GainCanNotBeDeletedPlayerEffect(Func<Permanent, bool> permanentCondition, Func<Permanent,Permanent,Permanent,CardSource,bool> canNotBeDestroyedByBattleCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotBeDeletedByBattle.cs:10
- Card-call count: 2
- Mirror: `public static bool GainCanNotBeDeletedPlayerEffect(Func<Permanent, bool>? permanentCondition, Func<Permanent,Permanent,Permanent,CardSource,bool>? canNotBeDestroyedByBattleCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be deleted in battle")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3271 (bool, via `GainToPlayerScope`)
- activateClass usage: real — `EffectSourceCard`/`CanNotBeAffected` inside the per-permanent condition closure.

### GainCanNotBlock
- AS-IS: `public static IEnumerator GainCanNotBlock(Permanent targetPermanent, Func<Permanent, bool> attackerCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBlock.cs:10
- Card-call count: 22
- Mirror: `public static bool GainCanNotBlock(Permanent? targetPermanent, Func<Permanent, bool>? attackerCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't block")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3075 (bool, via `GainRestrictionToPermanent` `CannotBlockKey`)
- activateClass usage: real — `EffectSourceCard`/`CanNotBeAffected` grant-time/live guard.

### GainCanNotBlockPlayerEffect
- AS-IS: `public static IEnumerator GainCanNotBlockPlayerEffect(Func<Permanent, bool> attackerCondition, Func<Permanent, bool> defenderCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotBlock.cs:10
- Card-call count: 2
- Mirror: `public static bool GainCanNotBlockPlayerEffect(Func<Permanent, bool>? attackerCondition, Func<Permanent, bool>? defenderCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't block")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3256 (bool, via `GainToPlayerScope`)
- activateClass usage: real — `EffectSourceCard`/`CanNotBeAffected` inside `AttackerCondition` closure.

### GainCanNotReturnToDeck
- AS-IS: `public static IEnumerator GainCanNotReturnToDeck(Permanent targetPermanent, Func<ICardEffect, bool> cardEffectCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNoReturnToDeck.cs:10
- Card-call count: 23
- Mirror: `public static bool GainCanNotReturnToDeck(Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to deck")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3296 (bool, via `GainRestrictionToPermanent` `CannotReturnToDeckKey`)
- ⚠️ Same `Func<ICardEffect,bool>` vs `Func<CardSource,bool>` mismatch as GainCanNotBeDeletedByEffect.
- activateClass usage: real — grant-time/live guard.

### GainCanNotReturnToDeckPlayerEffect
- AS-IS: `public static IEnumerator GainCanNotReturnToDeckPlayerEffect(Func<Permanent, bool> permanentCondition, Func<ICardEffect, bool> cardEffectCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNoReturnToDeck.cs:10
- Card-call count: 3
- Mirror: `public static bool GainCanNotReturnToDeckPlayerEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to deck")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3312 (bool, via `GainToPlayerScope`)
- activateClass usage: real — same pattern inside `PermanentCondition`.

### GainCanNotReturnToHand
- AS-IS: `public static IEnumerator GainCanNotReturnToHand(Permanent targetPermanent, Func<ICardEffect, bool> cardEffectCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotReturnToHand.cs:10
- Card-call count: 23
- Mirror: `public static bool GainCanNotReturnToHand(Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to hand")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3288 (bool, via `GainRestrictionToPermanent`)
- activateClass usage: real — grant-time/live guard.

### GainCanNotReturnToHandPlayerEffect
- AS-IS: `public static IEnumerator GainCanNotReturnToHandPlayerEffect(Func<Permanent, bool> permanentCondition, Func<ICardEffect, bool> cardEffectCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotReturnToHand.cs:10
- Card-call count: 3
- Mirror: `public static bool GainCanNotReturnToHandPlayerEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to hand")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3304 (bool, via `GainToPlayerScope`)
- activateClass usage: real — same pattern.

### GainCanNotSuspend
- AS-IS: `public static IEnumerator GainCanNotSuspend(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass, Func<bool> condition, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotSuspend.cs:34
- Card-call count: 3
- Mirror: `public static bool GainCanNotSuspend(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard, Func<bool>? condition = null, string effectName = "Can't suspend")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3096 (bool, via `GainRestrictionToPermanent` `CannotSuspendKey`/`extraCondition`)
- activateClass usage: real — `EffectSourceCard`/`CanNotBeAffected` combined with caller `condition`.

### GainCanNotSuspendPlayerEffect
- AS-IS: `public static IEnumerator GainCanNotSuspendPlayerEffect(Func<Permanent, bool> permanentCondition, EffectDuration effectDuration, ICardEffect activateClass, bool isOnlyActivePhase, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotSuspend.cs:10
- Card-call count: 10
- Mirror: `public static bool GainCanNotSuspendPlayerEffect(Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard, bool isOnlyActivePhase = false, string effectName = "Can't suspend")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3230 (bool)
- activateClass usage: real — `EffectSourceCard`/`CanNotBeAffected` guard PLUS `isOnlyActivePhase` gates turn-player/turn-phase; mirror reproduces via `TurnController.Current.TurnPlayerId`.

### GainCanNotUnsuspend
- AS-IS: `public static IEnumerator GainCanNotUnsuspend(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass, Func<bool> condition, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotUnsuspend.cs:69
- Card-call count: 25
- Mirror: `public static bool GainCanNotUnsuspend(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard, Func<bool>? condition = null, string effectName = "Can't unsuspend")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3107 (bool, via `GainRestrictionToPermanent` `CannotUnsuspendKey`)
- activateClass usage: real — same pattern as GainCanNotSuspend.

### GainCanNotUnsuspendPlayerEffect
- AS-IS: `public static IEnumerator GainCanNotUnsuspendPlayerEffect(Func<Permanent, bool> permanentCondition, EffectDuration effectDuration, ICardEffect activateClass, bool isOnlyActivePhase, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotUnsuspend.cs:10
- Card-call count: 18
- Mirror: `public static bool GainCanNotUnsuspendPlayerEffect(Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard, bool isOnlyActivePhase = false, string effectName = "Can't unsuspend")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3217 (bool)
- activateClass usage: real — same pattern as GainCanNotSuspendPlayerEffect.

### GainCantSuspendUntilOpponentTurnEnd
- AS-IS: `public static IEnumerator GainCantSuspendUntilOpponentTurnEnd(Permanent targetPermanent, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotSuspend.cs:8
- Card-call count: 2
- Mirror: `public static bool GainCantSuspendUntilOpponentTurnEnd(Permanent? targetPermanent, CardSource sourceCard) => GainCanNotSuspend(targetPermanent, EffectDuration.UntilOpponentTurnEnd, sourceCard);` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3103 (bool, one-liner delegate)
- activateClass usage: real (thin, load-bearing) — forwards `activateClass` unchanged into `GainCanNotSuspend`; mirror's one-liner is an equivalent shape minus redundant null-guards already covered downstream.

### GainCantUnsuspendNextActivePhase
- AS-IS: `public static IEnumerator GainCantUnsuspendNextActivePhase(Permanent targetPermanent, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotUnsuspend.cs:10
- Card-call count: 23
- Mirror: `public static bool GainCantUnsuspendNextActivePhase(Permanent? targetPermanent, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3121 (bool, delegates `GainCanNotUnsuspend(..., EffectDuration.UntilNextUntap, sourceCard)`)
- activateClass usage: real — `EffectSourceCard` extraction; `CanUseCondition` gates opponent-turn+active-phase (mirror's `UntilNextUntap` duration argued equivalent).

### GainCantUnsuspendUntilOpponentTurnEnd
- AS-IS: `public static IEnumerator GainCantUnsuspendUntilOpponentTurnEnd(Permanent targetPermanent, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotUnsuspend.cs:45
- Card-call count: 33
- Mirror: `public static bool GainCantUnsuspendUntilOpponentTurnEnd(Permanent? targetPermanent, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3114 (bool, delegates `GainCanNotUnsuspend(..., EffectDuration.UntilOpponentTurnEnd, sourceCard)`)
- activateClass usage: real — same pattern.

### GainCollision
- AS-IS: `public static IEnumerator GainCollision(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Collision.cs:10
- Card-call count: 6
- Mirror: `public static bool GainCollision(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3421 (bool, via `GainKeywordToPermanent`, `ContinuousKeywordGate.Collision`)
- activateClass usage: real — `rootCardEffect` into `CollisionEffect` + `CanNotBeAffected` gate; VFX is UI.

### GainEvade
- AS-IS: `public static IEnumerator GainEvade(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Evade.cs:53
- Card-call count: 1
- Mirror: `public static bool GainEvade(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3437 (bool, via `GainKeywordToPermanent`, `ContinuousKeywordGate.Evade`)
- activateClass usage: real — `rootCardEffect` chaining + live gate.

### GainImmuneFromDPMinus
- AS-IS: `public static IEnumerator GainImmuneFromDPMinus(Permanent targetPermanent, Func<ICardEffect, bool> cardEffectCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/ImmuneFromDPMinus.cs:10
- Card-call count: 13
- Mirror: `public static bool GainImmuneFromDPMinus(Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Immune from DP minus")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3321 (bool, via `GainRestrictionToPermanent` `ImmuneFromDpMinusKey`)
- ⚠️ Same `Func<ICardEffect,bool>` vs `Func<CardSource,bool>` adapter need.
- activateClass usage: real — `EffectSourceCard`/`CanNotBeAffected` feeding `ImmuneFromDPMinusStaticEffect`.

### GainImmuneFromDPMinusPlayerEffect
- AS-IS: `public static IEnumerator GainImmuneFromDPMinusPlayerEffect(Func<Permanent, bool> permanentCondition, Func<ICardEffect, bool> cardEffectCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ImmuneFromDPMinus.cs:10
- Card-call count: 1
- Mirror: `public static bool GainImmuneFromDPMinusPlayerEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Immune from DP minus")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3329 (bool, via `GainToPlayerScope`)
- ⚠️ Same adapter need as above.
- activateClass usage: real — `permanentCondition` closure wraps `CanNotBeAffected` per-permanent.

### GainJamming
- AS-IS: `public static IEnumerator GainJamming(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Jamming.cs:10
- Card-call count: 22
- Mirror: `public static bool GainJamming(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3425 (bool, via `GainKeywordToPermanent`, `ContinuousKeywordGate.Jamming`)
- activateClass usage: real — `EffectSourceCard`/`CanNotBeAffected` live gate feeding `JammingStaticEffect`.

### GainPierce
- AS-IS: `public static IEnumerator GainPierce(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Pierce.cs:54
- Card-call count: 44
- Mirror: `public static bool GainPierce(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3413 (bool, via `GainKeywordToPermanent`, `ContinuousKeywordGate.Piercing`)
- activateClass usage: real — `rootCardEffect` chaining. Note: same AS-IS file has sibling helpers `CanTriggerPierce`/`CanActivatePierce`/`PierceProcess` not in this 91-set (worth a follow-up mapping pass).

### GainRaid
- AS-IS: `public static IEnumerator GainRaid(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Raid.cs:81
- Card-call count: 21
- Mirror: `public static bool GainRaid(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3441 (bool, via `GainKeywordToPermanent`, `ContinuousKeywordGate.Raid`)
- activateClass usage: real — `rootCardEffect` chaining. Sibling `RaidProcess`/`CanActivateRaid` (same file, not in this batch) thread `activateClass` further into `SelectPermanentEffect.SetUp`/`attackProcess.SwitchDefender` — worth a follow-up pass.

### GainReboot
- AS-IS: `public static IEnumerator GainReboot(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Reboot.cs:10
- Card-call count: 22
- Mirror: `public static bool GainReboot(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3429 (bool, via `GainKeywordToPermanent`, `ContinuousKeywordGate.Reboot`)
- activateClass usage: real — standard grant-source + live-gate pattern.

### GainRetaliation
- AS-IS: `public static IEnumerator GainRetaliation(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Retaliation.cs:136
- Card-call count: 23
- Mirror: `public static bool GainRetaliation(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3417 (bool, via `GainKeywordToPermanent`, `ContinuousKeywordGate.Retaliation`)
- activateClass usage: real — `rootCardEffect` chaining. Sibling `RetaliationProcess` (same file, not in this batch) threads `activateClass` into `DestroyPermanentsClass` for destroy-cause attribution.

### GainRush
- AS-IS: `public static IEnumerator GainRush(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Rush.cs:10
- Card-call count: 44
- Mirror: `public static bool GainRush(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3409 (bool, via `GainKeywordToPermanent`, `ContinuousKeywordGate.Rush`)
- activateClass usage: real — standard pattern.

### PlaceDelayOptionCards
- AS-IS: `public static IEnumerator PlaceDelayOptionCards(CardSource card, ICardEffect cardEffect, SelectCardEffect.Root root = SelectCardEffect.Root.Execution)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:113
- Card-call count: 182
- Mirror: `public static async Task<bool> PlaceDelayOptionCards(CardSource card, ICardEffect? cardEffect = null, ChoiceZone root = ChoiceZone.Execution)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:494 (Task<bool>)
- Only real diff is return type — param list is already AS-IS-shaped.
- ⚠️ activateClass usage: AS-IS threads `cardEffect` into `CanPlayAsNewPermanent(..., cardEffect: cardEffect, isPlayOption: true)` (real gate input), but the mirror body currently does `_ = cardEffect;` then calls `CanPlayAsNewPermanent(card, payCost:false, null, isPlayOption:true)` — **the substrate itself drops `cardEffect`**, a pre-existing gap independent of the wrapper.

### PlacePermanentInSecurityAndProcessAccordingToResult
- AS-IS: `public static IEnumerator PlacePermanentInSecurityAndProcessAccordingToResult(Permanent targetPermanent, ICardEffect activateClass, bool toTop, Func<CardSource, IEnumerator> successProcess, Func<IEnumerator> failureProcess = null, bool isFaceUp = false)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:644
- Card-call count: 6
- Mirror: `public static async Task PlacePermanentInSecurityAndProcessAccordingToResult(Permanent? targetPermanent, bool toTop, CardSource sourceCard, Func<CardSource, Task>? successProcess, Func<Task>? failureProcess, bool isFaceUp = false)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:440 (Task)
- ⚠️ Param order differs (`activateClass`/`sourceCard` position moved) and delegate types need IEnumerator→Task adapters.
- activateClass usage: real — AS-IS gates on `EffectSourceCard.Owner.CanAddSecurity(activateClass)` before attempting; mirror doc comment claims this is honored via the sink route but no explicit `CanAddSecurity`-equivalent call is visible in the body itself — worth double-checking before treating as fully covered.

### PlayAmonToken
- AS-IS: `public static IEnumerator PlayAmonToken(ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:197 (delegates to generic `PlayToken`)
- Card-call count: 2
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayAmonToken(CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2485 (delegates to generic `PlayToken(TokenSpecs["Amon"], sourceCard, isOwnerPermanent:true, isTapped:false)`)
- activateClass usage: pass-through only — real logic lives in generic `PlayToken` (see note below).

### PlayAthoRenePorToken
- AS-IS: `public static IEnumerator PlayAthoRenePorToken(ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:381
- Card-call count: 3
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayAthoRenePorToken(CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2537
- activateClass usage: pass-through only.

### PlayDiaboromonToken
- AS-IS: `public static IEnumerator PlayDiaboromonToken(ICardEffect activateClass, int quantity = 1)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:182
- Card-call count: 18
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayDiaboromonToken(CardSource sourceCard, int quantity = 1)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2481
- activateClass usage: pass-through only; multi-copy loop logic lives in generic `PlayToken`.

### PlayFamiliarToken
- AS-IS: `public static IEnumerator PlayFamiliarToken(ICardEffect activateClass, int quantity = 1)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:267
- Card-call count: 7
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayFamiliarToken(CardSource sourceCard, int quantity = 1)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2505
- activateClass usage: pass-through only.

### PlayFujitsumonToken
- AS-IS: `public static IEnumerator PlayFujitsumonToken(ICardEffect activateClass, bool isOwnerPermanent)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:225
- Card-call count: 2
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayFujitsumonToken(CardSource sourceCard, bool isOwnerPermanent)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2493
- activateClass usage: pass-through only.

### PlayGyuukimonToken
- AS-IS: `public static IEnumerator PlayGyuukimonToken(ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:239
- Card-call count: 1
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayGyuukimonToken(CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2497
- activateClass usage: pass-through only.

### PlayHinukamuyToken
- AS-IS: `public static IEnumerator PlayHinukamuyToken(ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:395
- Card-call count: 1
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayHinukamuyToken(CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2541
- activateClass usage: pass-through only.

### PlayKoHagurumonToken
- AS-IS: `public static IEnumerator PlayKoHagurumonToken(ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:253
- Card-call count: 1
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayKoHagurumonToken(CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2501
- activateClass usage: pass-through only.

### PlayPermanentCards
- AS-IS: `public static IEnumerator PlayPermanentCards(List<CardSource> cardSources, ICardEffect activateClass, bool payCost, bool isTapped, SelectCardEffect.Root root, bool activateETB, bool isBreedingArea = false, int fixedCost = -1)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:23
- Card-call count: 977 (highest raw count in the whole set)
- Mirror: `public static async Task PlayPermanentCards(IReadOnlyList<CardSource> cardSources, CardSource sourceCard, bool payCost, bool isTapped, ChoiceZone root, bool activateETB, bool isBreedingArea = false, int fixedCost = -1)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:1880 (Task)
- Param-for-param match except `ICardEffect`→`CardSource`, `SelectCardEffect.Root`→`ChoiceZone`.
- ⚠️ activateClass usage: real (feeds `CanPlayAsNewPermanent(cardEffect: activateClass)` → `CanPlayCardTargetFrame`, and `CardEffectHashtable(activateClass)` for root-cause tracking) but **mirror's `CanPlayAsNewPermanent` receives `null` for `cardEffect` today** (`_ = cardEffect;` unused stub) — the AS-IS per-target-frame `cardEffect`-gated filtering is not wired through in the mirror's play-permanent path. Flag for whoever builds the wrapper — highest-traffic helper in the whole set.

### PlayPetrificationToken
- AS-IS: `public static IEnumerator PlayPetrificationToken(ICardEffect activateClass, int quantity = 1)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:409
- Card-call count: 3
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayPetrificationToken(CardSource sourceCard, int quantity = 1)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2545
- activateClass usage: pass-through only.

### PlayPipeFox
- AS-IS: `public static IEnumerator PlayPipeFox(ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:367
- Card-call count: 3
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayPipeFox(CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2533
- activateClass usage: pass-through only.

### PlayRapidmonToken
- AS-IS: `public static IEnumerator PlayRapidmonToken(ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:353
- Card-call count: 1
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayRapidmonToken(CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2529
- activateClass usage: pass-through only.

### PlaySelfDeleteFamiliarToken
- AS-IS: `public static IEnumerator PlaySelfDeleteFamiliarToken(ICardEffect activateClass, int quantity = 1)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:282
- Card-call count: 2
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlaySelfDeleteFamiliarToken(CardSource sourceCard, int quantity = 1)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2509
- activateClass usage: pass-through only.

### PlayTaomonToken
- AS-IS: `public static IEnumerator PlayTaomonToken(ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:339
- Card-call count: 1
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayTaomonToken(CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2525
- activateClass usage: pass-through only.

### PlayUkaNoMitama
- AS-IS: `public static IEnumerator PlayUkaNoMitama(ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:311
- Card-call count: 1
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayUkaNoMitama(CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2517
- activateClass usage: pass-through only.

### PlayUmonToken
- AS-IS: `public static IEnumerator PlayUmonToken(ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:211
- Card-call count: 2
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayUmonToken(CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2489
- activateClass usage: pass-through only.

### PlayVoleeZerdrucken
- AS-IS: `public static IEnumerator PlayVoleeZerdrucken(ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:297
- Card-call count: 2
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayVoleeZerdrucken(CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2513
- activateClass usage: pass-through only.

### PlayWarGrowlmonToken
- AS-IS: `public static IEnumerator PlayWarGrowlmonToken(ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:325
- Card-call count: 1
- Mirror: `public static Task<IReadOnlyList<HeadlessEntityId>> PlayWarGrowlmonToken(CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2521
- activateClass usage: pass-through only.

> **Underlying primitive for all 14 tokens above**: AS-IS `PlayToken(CEntity_Base tokenData, ICardEffect activateClass, bool isOwnerPermanent, bool isTapped, int quantity = 1)` — CardEffectCommons.cs:140 — uses `activateClass.EffectSourceCard` for owner resolution, a `fieldCardFrames` empty-slot capacity check (`>= quantity`), and `CanPlayAsNewPermanent(..., cardEffect: activateClass)` gate. Mirror equivalent `PlayToken(TokenSpec, CardSource sourceCard, bool isOwnerPermanent, bool isTapped, int quantity = 1)` at CardEffectCommons.cs:2404 is a **documented intentional deviation**: doc comment states "the AS-IS empty-frame count has no port model — no field-size limit is modeled anywhere," and it never calls `CanPlayAsNewPermanent` (unconditional token creation). Flag if strict fidelity to the capacity check matters.

### ReturnRevealedCardsToLibraryBottom
- AS-IS: `public static IEnumerator ReturnRevealedCardsToLibraryBottom(List<CardSource> remainingCards, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/RevealLibrary.cs:469
- Card-call count: 7
- Mirror: `public static async Task ReturnRevealedCardsToLibraryBottom(IReadOnlyList<CardSource> remainingCards, CardSource sourceCard, CancellationToken cancellationToken = default)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2763 (Task)
- Near-trivial diff (`List`→`IReadOnlyList`, added `CancellationToken`); ordering logic (1-card→straight to bottom, 2+→order-pick) identical.
- activateClass usage: real — `effectSourceCard`/`selectPlayer` derive whose deck/ordering-select this is; `activateClass` threaded into `selectCardEffect.SetUp(cardEffect: activateClass)` for root-effect chaining.

### RevealDeckTopCardsAndSelect
- AS-IS: `public static IEnumerator RevealDeckTopCardsAndSelect(int revealCount, SelectCardConditionClass[] selectCardConditions, RemainingCardsPlace remainingCardsPlace, ICardEffect activateClass, bool canNoAction = false, bool isSendAllCardsToSamePlace = false, bool isOpponentDeck = false, Func<List<CardSource>, IEnumerator> revealedCardsCoroutine = null, bool mutualConditions = false)` — DCGO/Assets/Scripts/Script/CardEffectCommons/RevealLibrary.cs:229
- Card-call count: 25
- Mirror: `public static IActivatedCardEffect RevealDeckTopCardsAndSelect(CardSource card, int revealCount, IReadOnlyList<RevealSelectPass> selectCardConditions, RevealDestination remainingCardsPlace, string description, bool canNoAction = false, bool isOpponentDeck = false, bool mutualConditions = false)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectFactory.cs:1188, backed by class `RevealMultiSelectEffect` (CardEffectCommons/ActivatedEffects.cs:1443) (returns `IActivatedCardEffect`, not Task)
- ⚠️ Mirror is unusually thorough (per-pass sequential select, `canNoAction` opt-out, `mutualConditions` relaxation matching AS-IS RevealLibrary.cs:302-308 verbatim, DeckTopOrBottom flow) but **`isSendAllCardsToSamePlace` and `revealedCardsCoroutine` are missing entirely** — need to be added or accepted as a known gap.
- activateClass usage: real — `selectPlayer`/`revealPlayer` resolution; passed as `cardEffect: activateClass` into every per-pass `selectCardEffect.SetUp(...)`.

### SaveProcess
- AS-IS: `public static IEnumerator SaveProcess(Hashtable hashtable, ICardEffect activateClass, CardSource card, Func<Permanent, bool> CanSelectPermanentCondition)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Save.cs:25
- Card-call count: 17
- Mirror: `public static async Task SaveProcess(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? CanSelectPermanentCondition, CancellationToken cancellationToken = default)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2556 (Task)
- ⚠️ AS-IS `Hashtable hashtable` → mirror `CardEffectResolveContext ctx` is a structural swap, not a rename; AS-IS `activateClass` param is dropped (mirror derives owner/gates from `card` directly).
- activateClass usage: real — passed to `selectedPermanent.AddDigivolutionCardsBottom(..., activateClass)`, records the digivolve-attach source; not UI.

### SelectTrashDigivolutionCards
- AS-IS: `public static IEnumerator SelectTrashDigivolutionCards(Func<Permanent, bool> permanentCondition, Func<CardSource, bool> cardCondition, int maxCount, bool canNoTrash, bool isFromOnly1Permanent, ICardEffect activateClass, string selectString = "Digimon", Func<Permanent, List<CardSource>, IEnumerator> afterSelectionCoroutine = null, bool canEndNotMax = false)` — DCGO/Assets/Scripts/Script/CardEffectCommons/TrashDigivolutionCards.cs:11
- Card-call count: 67
- Mirror: `public static async Task SelectTrashDigivolutionCards(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardCondition, int maxCount, bool canNoTrash, bool isFromOnly1Permanent, CardSource sourceCard, string selectString = "Digimon", Func<Permanent, IReadOnlyList<CardSource>, Task>? afterSelectionCoroutine = null, bool canEndNotMax = false, CancellationToken cancellationToken = default)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2126 (Task)
- Near 1:1; uses `DigivolutionStackHelpers.TrashSpecificSourcesAsync` internally (the correct substrate primitive for arbitrary-list trashing — see also TrashDigivolutionCardsAndProcessAccordingToResult below).
- activateClass usage: real — feeds `IsTrashProtectedSource`/`CanNotTrashFromDigivolutionCards` gating and is the cause id for the trash helper.

### SimplifiedRevealDeckTopCardsAndSelect
- AS-IS: `public static IEnumerator SimplifiedRevealDeckTopCardsAndSelect(int revealCount, SimplifiedSelectCardConditionClass[] simplifiedSelectCardConditions, RemainingCardsPlace remainingCardsPlace, ICardEffect activateClass, Func<List<CardSource>, CardSource, bool> canTargetCondition_ByPreSelecetedList = null, Func<List<CardSource>, bool> canEndSelectCondition = null, bool canNoSelect = false, bool canEndNotMax = false, bool isSendAllCardsToSamePlace = false, bool isOpponentDeck = false, Func<List<CardSource>, IEnumerator> revealedCardsCoroutine = null, bool mutualConditions = false)` — DCGO/Assets/Scripts/Script/CardEffectCommons/RevealLibrary.cs:179
- Card-call count: 409 (**highest call count of any helper in the 91-set**)
- Mirror: `public static IActivatedCardEffect SimplifiedRevealDeckTopCardsAndSelect(CardSource card, int revealCount, IReadOnlyList<SimplifiedSelectCardConditionClass> conditions, RevealDestination remainingTo, string description)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectFactory.cs:1177 (returns `IActivatedCardEffect`, a declarative registration object, NOT a `Task`)
- ⚠️⚠️ **Highest-priority finding in the whole survey.** Architecture mismatch: AS-IS is imperative inline coroutine; mirror is a declarative factory whose real logic lives in `SimplifiedRevealAndSelectEffect.ResolveAsync(sink, ct)` (ActivatedEffects.cs:1326) — a delegating wrapper needs a sink, not a direct call. **6 of 11 AS-IS params are silently unmodeled**: `canTargetCondition_ByPreSelecetedList` (used by 160/362 caller files), `canEndSelectCondition` (164), `canNoSelect` (183 — but `ResolveAsync` hardcodes `canSkip:true`/`minCount:0` always, i.e. behaves as if `canNoSelect=true` unconditionally), `canEndNotMax` (164), `mutualConditions` (54), `isSendAllCardsToSamePlace` (3), `isOpponentDeck` (1), `revealedCardsCoroutine` (6). A naive thin wrapper would silently drop must-select semantics and per-card follow-up coroutines for the majority of the 409 call sites — needs real design work, not a thin wrapper.
- activateClass usage: real — `EffectSourceCard`/`.Owner` drive which player's deck is revealed and gate the whole call (`yield break` if null); passed straight through, not UI.

### StartOfMainAttack
- AS-IS: `public static IEnumerator StartOfMainAttack(Permanent targetPermanent, ICardEffect cardEffect)` — DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/StartOfMainAttack.cs:5
- Card-call count: 3
- Mirror: `public static void StartOfMainAttack(Permanent? targetPermanent, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:1481 (void, synchronous)
- Mirror registers an `EffectBinding` on `OnStartMainPhase` immediately instead of yielding — functionally equivalent, wrapper can call then return `Task.CompletedTask`. AS-IS tail `CreateDebuffEffect` (visual overlay) has no mirror counterpart — appears UI-only, not independently verified.
- activateClass usage: real — used inside registered `CanActivateCondition1` closure for `CanNotBeAffected(cardEffect)`, gating whether the forced-attack offer can trigger.

### SuspendPeremanentAndProcessAccordingToResult
- (see EXISTS-AS-IS-COMPAT section below)

### TrashDigivolutionCardsAndProcessAccordingToResult
- AS-IS: `public static IEnumerator TrashDigivolutionCardsAndProcessAccordingToResult(Permanent targetPermanent, List<CardSource> targetDigivolutionCards, ICardEffect activateClass, Func<List<CardSource>, IEnumerator> successProcess, Func<IEnumerator> failureProcess)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:541
- Card-call count: 9
- Mirror (same name, **WRONG shape**): `public static async Task TrashDigivolutionCardsAndProcessAccordingToResult(Permanent? targetPermanent, int trashCount, bool isFromTop, CardSource sourceCard, Func<int, Task>? successProcess, Func<Task>? failureProcess)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:278 (Task) — this is a top/bottom-**count** shape, not the AS-IS arbitrary-`List<CardSource>` shape.
- ⚠️⚠️ **Name collision, not a valid delegation target.** AS-IS trashes an arbitrary caller-supplied `List<CardSource>` (specific pre-selected sources); the existing same-named mirror method only supports positional top/bottom N-count trash. The correct substrate for the true AS-IS shape is `Headless.Runtime.DigivolutionStackHelpers.TrashSpecificSourcesAsync(repository, zoneMover, hostId, IReadOnlyList<HeadlessEntityId> cardIds, ...)` (DigivolutionStackHelpers.cs:245, explicitly commented "AS-IS `ITrashDigivolutionCards(permanent, selectedCards, …)`") — the same primitive `SelectTrashDigivolutionCards` already uses internally. A new AS-IS-signature overload should wrap `TrashSpecificSourcesAsync` directly, NOT this same-named method.
- activateClass usage: real — passed to `ITrashDigivolutionCards` constructor (source of trash + protection-gate evaluation).

### TrashDigivolutionCardsFromTopOrBottom
- AS-IS: `public static IEnumerator TrashDigivolutionCardsFromTopOrBottom(Permanent targetPermanent, int trashCount, bool isFromTop, ICardEffect activateClass, Func<CardSource, bool> cardCondition = null)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:675
- Card-call count: 121
- Mirror: `public static Task<int> TrashDigivolutionCardsFromTopOrBottom(Permanent? targetPermanent, int trashCount, bool isFromTop, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:312 (Task<int>)
- ⚠️ **Missing the optional `cardCondition` parameter entirely.** Confirmed by grep: NOT a rarely-used default — call sites in ST24_06/ST24_10/ST24_11/ST24_12 (and others) pass a real non-null `cardCondition` (e.g. `CanSelectTrashSourceCardCondition`) restricting WHICH digivolution sources are eligible from the top/bottom scan before the count-cap applies. The mirror method has no way to honor a per-call custom source filter beyond the blanket `CanNotTrashFromDigivolutionCards`/`ImmuneFromStackTrashing` gates already baked into `IsHostStackTrashGated`/`TrashSourcesAsync`. Second-highest-priority gap in the whole set (121 calls, real dropped semantics for a meaningful subset).
- activateClass usage: real — pre-gates on `DigivolutionCards.Count(c => !c.CanNotTrashFromDigivolutionCards(activateClass)) == 0` and `TopCard.CanNotBeAffected(activateClass)`, both real protection checks (mirrored via `IsHostStackTrashGated`).

### TrashHandAndProcessAccordingToResult
- AS-IS: `public static IEnumerator TrashHandAndProcessAccordingToResult(Player player, Hashtable hashtable, CardSource cardToTrash, ActivateClass activateClass, Func<CardSource, IEnumerator> successProcess, Func<IEnumerator> failureProcess)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:619
- Card-call count: 1
- Mirror: `public static async Task TrashHandAndProcessAccordingToResult(CardSource? handCard, CardSource sourceCard, Func<Task>? successProcess, Func<Task>? failureProcess)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:412 (Task)
- AS-IS `Player player` is dead/unused in the body — safe to drop. AS-IS types `activateClass` as the concrete `ActivateClass` (not the usual `ICardEffect`) — minor outlier, no behavioral implication found.
- activateClass usage: real — passed to `IDiscardHands`/`IDiscardHand` ctors as discard cause; success/failure branches on actual post-mutation `HasDiscarded` state.

### TrashLinkCardsAndProcessAccordingToResult
- AS-IS: `public static IEnumerator TrashLinkCardsAndProcessAccordingToResult(Permanent targetPermanent, List<CardSource> targetLinkCards, ICardEffect activateClass, Func<List<CardSource>, IEnumerator> successProcess, Func<IEnumerator> failureProcess)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:567
- Card-call count: 11
- Mirror: `public static async Task TrashLinkCardsAndProcessAccordingToResult(Permanent? hostPermanent, IReadOnlyList<HeadlessEntityId> linkCardIds, CardSource sourceCard, Func<int, Task>? successProcess, Func<Task>? failureProcess)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:342 (Task)
- ⚠️ Success payload is `int` count vs AS-IS's `List<CardSource>` of trashed cards — wrapper needs to reconstruct `CardSource` views from ids/count if the AS-IS callback body inspects actual cards.
- activateClass usage: real — passed to `ITrashLinkCards` ctor; mirror uses `LinkHelpers.RemoveLinkCardAsync` per card.

### TrashSecurityAndProcessAccordingToResult
- AS-IS: `public static IEnumerator TrashSecurityAndProcessAccordingToResult(Player player, int trashAmount, ICardEffect activateClass, bool fromTop, Func<List<CardSource>, IEnumerator> successProcess, Func<IEnumerator> failureProcess)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:593
- Card-call count: 7
- Mirror: `public static async Task TrashSecurityAndProcessAccordingToResult(HeadlessPlayerId player, int trashAmount, bool fromTop, CardSource sourceCard, Func<int, Task>? successProcess, Func<Task>? failureProcess)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:378 (Task)
- ⚠️ Success payload is `int` trashed count vs AS-IS `List<CardSource>` of destroyed security — mirror computes success via before/after zone-count diff, not by tracking which specific cards.
- activateClass usage: real — passed to `IDestroySecurity` ctor as cause; success/failure branches on actual `DestroyedSecurity.Any()` post-mutation.

---

## EXISTS-AS-IS-COMPAT (3)

### AddThisCardToHand
- AS-IS: `public static IEnumerator AddThisCardToHand(CardSource card1, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:424
- Card-call count: 127
- Mirror: `public static async Task AddThisCardToHand(CardSource card1, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:1860 (Task)
- Param count/order identical, only `ICardEffect`→`CardSource` and `IEnumerator`→`Task`; AS-IS's `WaitForSeconds`/`CloseBrainstrorm` are UI-only, correctly elided.
- activateClass usage: real — forwarded as cause into `AddHandCards` sink mutation.

### GainRushPlayerEffect
- AS-IS: `public static IEnumerator GainRushPlayerEffect(Func<Permanent, bool> permanentCondition, EffectDuration effectDuration, ICardEffect activateClass)` — DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Rush.cs:46
- Card-call count: 4
- Mirror: `public static bool GainRushPlayerEffect(Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:3202 (bool, via `GainToPlayerScope(..., keyword: ContinuousKeywordGate.Rush)`)
- Signature essentially identical modulo `activateClass`→`sourceCard`.
- activateClass usage: real — `EffectSourceCard` extraction; `PermanentCondition` closure calls `CanNotBeAffected(activateClass)` per-permanent.

### SuspendPeremanentAndProcessAccordingToResult
- AS-IS: `public static IEnumerator SuspendPeremanentAndProcessAccordingToResult(List<Permanent> targetPermanents, ICardEffect activateClass, Func<List<Permanent>, IEnumerator> successProcess, Func<IEnumerator> failureProcess)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:437
- Card-call count: 7
- Mirror: `public static async Task SuspendPeremanentAndProcessAccordingToResult(IReadOnlyList<Permanent> targetPermanents, CardSource sourceCard, Func<IReadOnlyList<Permanent>, Task>? successProcess, Func<Task>? failureProcess)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:174 (Task)
- Near 1:1 shape (only `ICardEffect`→`CardSource`, `List<T>`→`IReadOnlyList<T>`, delegate `IEnumerator`→`Task`).
- activateClass usage: real — used to construct the mutation-sink cause id; success/failure computed from actual post-mutation suspended state.

---

## NO-MIRROR (3)

### DNADigivolveWithHandOrTrashCardIntoHandOrTrash
- AS-IS: `public static IEnumerator DNADigivolveWithHandOrTrashCardIntoHandOrTrash(Func<CardSource, bool> targetCardCondition, Func<Permanent, bool> permanentCondition, Func<CardSource, bool> digivolutionCardCondition, bool payCost, bool isWithHandCard, bool isIntoHandCard, ICardEffect activateClass, IEnumerator successProcess, bool ignoreSelection = false, IEnumerator failedProcess = null, bool isOptional = true)` — DCGO/Assets/Scripts/Script/CardEffectCommons/DNADigivolveEffects.cs:256
- Card-call count: 2
- Mirror: `public static Task DNADigivolveWithHandOrTrashCardIntoHandOrTrash(CardSource sourceCard) => throw new NotSupportedException("DNA-with-temporary-material is not modeled — STOP (strong model).")` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:2844
- Gap class: NO-MIRROR — explicit throw-stub, not a real implementation. AS-IS body selects a DNA-capable card from hand/trash to be one root, temporarily plays it as a real (throwaway) permanent via `PlayTempPermanent`/`CardObjectController.CreateNewPermanent` so it can be jointly evaluated with a field permanent, resolves ordering via a selection UI, then executes the jogress play via `PlayCardClass`; on failure un-plays the temp card back. This "play a temp permanent mid-effect-resolution" mechanic has no headless substrate equivalent, per the mirror's own doc comment — matches the "no simplification" stance (STOP, not silently degraded).
- activateClass usage: real and extensive — `EffectSourceCard.Owner` for the acting player, threaded as `cardEffect:` into selection setups and into `CardEffectHashtable(activateClass)` fed to `PlayCardClass` — load-bearing, not UI.

### PlayOptionCards
- AS-IS: `public static IEnumerator PlayOptionCards(List<CardSource> cardSources, ICardEffect activateClass, bool payCost, SelectCardEffect.Root root, bool setAddSecurityEndOption = false)` — DCGO/Assets/Scripts/Script/CardEffectCommons.cs:59
- Card-call count: 43
- Mirror: NONE with this name. Closest candidate: `public static ICardEffect PlayOptionCardEffect(CardSource card, ChoiceZone sourceZone, Func<HeadlessEntityId,bool> optionPredicate, int maxCount, bool canEndNotMax, string description)` — src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectFactory.cs:1114, class `PlayOptionCardEffect` at CardEffectCommons/ActivatedEffects.cs:2266 (returns `IActivatedCardEffect`, resolved via `ActivatedEffectResolver.cs:579`)
- Gap class: NO-MIRROR — AS-IS body filters a **pre-given** `cardSources` list by `!CanNotPlayThisOption`, builds a `PlayCardClass` (optional cost payment, no root/target), and optionally registers an until-turn-end hook that places the effect source card to top-of-security when the timing fires (`setAddSecurityEndOption`). The mirror's `PlayOptionCardEffect` does its OWN zone-select (not given a pre-chosen list) and resolves trash→OnUseOption→resolve-[Main] rather than "play cost-optionally as if a permanent-ish thing." No `setAddSecurityEndOption` equivalent found.
- activateClass usage: real — `CardEffectHashtable(activateClass)`, `playCard.SetShowEffect()` gated on `activateClass != null`, and `EffectSourceCard.Owner.UntilEachTurnEndEffects.Add/Remove` for the security-end-option hook — real per-turn-effect registration, not UI.

### RevealDeckTopCardsAndProcessForAll
- AS-IS: `public static IEnumerator RevealDeckTopCardsAndProcessForAll(int revealCount, SimplifiedSelectCardConditionClass simplifiedSelectCardCondition, RemainingCardsPlace remainingCardsPlace, ICardEffect activateClass, Func<List<CardSource>, IEnumerator> revealedCardsCoroutine = null, List<CardSource> refSelectedCards = null, bool isOpponentDeck = false)` — DCGO/Assets/Scripts/Script/CardEffectCommons/RevealLibrary.cs:10
- Card-call count: 34
- Mirror: NONE with this name. Closest candidate: `SimplifiedRevealAndSelectEffect` class (CardEffectCommons/ActivatedEffects.cs:1295, factory `SimplifiedRevealDeckTopCardsAndSelect` at CardEffectFactory.cs:1177), which special-cases a condition's `MaxCount < 0` as "route EVERY matching revealed card automatically, no player selection" — explicitly documented (ActivatedEffects.cs:1355-1360) as this shape.
- Gap class: NO-MIRROR by name/call-shape — AS-IS reveals `revealCount` cards, partitions ALL of them by a single predicate (matched → routed per `Mode`: AddHand/Discard/Custom-with-per-card-coroutine; unmatched → `remainingCardsPlace` routing: DeckBottom/DeckTop/Trash/AddHand/DeckTopOrBottom), optionally exposes `refSelectedCards` out-param and a post-hoc `revealedCardsCoroutine` over ALL revealed cards. The mirror's declarative shape covers the "one MaxCount=-1 condition" case but is a card-authoring-time factory object, not an imperative call taking a pre-built condition + callbacks; no equivalent for `revealedCardsCoroutine` or `refSelectedCards` was found.
- activateClass usage: real — `effectSourceCard`/`selectPlayer`/`revealPlayer` (via `isOpponentDeck`) derived from it, threaded through to `AddHandCards`, `AddTrashCards`/`TrashRevealedCards`, and `ReturnRevealedCardsToLibraryBottom/Top` as the effect-source for nested mutations — not UI-only (`PlayLog.OnAddLog` calls are separate cosmetic logging using card names, not `activateClass` itself).

---

## UI-ONLY (1)

### ShowReducedCost
- AS-IS: `public static IEnumerator ShowReducedCost(Hashtable hashtable)` — DCGO/Assets/Scripts/Script/CardEffectCommons/ShowReducedCost.cs:9
- Card-call count: 132
- Mirror: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/ShowReducedCost.cs` is an empty skeleton stub ("TODO: Skeleton only") — no implementation exists, consistent with UI-only classification.
- Gap class: UI-ONLY — body only calls `GManager.instance.memoryObject.ShowMemoryPredictionLine(...)` (a cost-preview overlay) then `WaitForSeconds(0.2f)`; no game-state mutation.
- activateClass usage: N/A — this helper has no `activateClass`/`cardEffect` parameter at all (only `Hashtable hashtable`, used purely to fetch UI context).

---

## Notes for whoever builds the delegation-wrapper layer

1. **Universal type-swap pattern** (covers the vast majority of the 84 `SAME-NAME-DIFF-SIG` rows): `ICardEffect activateClass` → `CardSource sourceCard` (via `activateClass.EffectSourceCard`), `IEnumerator` → `Task`/`Task<T>`/`bool`/`void`, `List<T>` → `IReadOnlyList<T>`. A single generic wrapper-generation strategy likely covers ~70 of the 91 rows mechanically.
2. **`Func<ICardEffect, bool>` → `Func<CardSource, bool>` adapter** needed for: `GainCanNotBeDeletedByEffect`, `GainCanNotReturnToDeck(PlayerEffect)`, `GainCanNotReturnToHand(PlayerEffect)`, `GainImmuneFromDPMinus(PlayerEffect)` — 5 helpers, same fix pattern (`ce => predicate(ce.EffectSourceCard)`).
3. **IEnumerator-instance → Func\<Task\>-factory adapter** needed for the `*AndProcessAccordingToResult` family (Bounce/DeckBounce/Delete/Suspend/TrashLink/TrashSecurity/TrashDigivolutionCards...) since AS-IS passes already-started coroutines while the mirror wants factory delegates.
4. **Genuine name collisions requiring care, not blind delegation**: `TrashDigivolutionCardsAndProcessAccordingToResult` (mirror same-name method is a different, incompatible shape — real substrate is `DigivolutionStackHelpers.TrashSpecificSourcesAsync`).
5. **Params silently dropped by the substrate that carry real AS-IS logic** (candidates for either wrapper-level STOP/design-item or substrate follow-up): `ActivateMainOfOptionSide` (afterMainEffect/asEffectOfThisDigimon), `BlitzProcess` (activateClass/beforeOnAttackCoroutine gate), `DrawAndDiscardCards` (4 params), `PlaceDelayOptionCards`/`PlayPermanentCards` (cardEffect dropped before `CanPlayAsNewPermanent`), `TrashDigivolutionCardsFromTopOrBottom` (cardCondition, 121 calls — high priority), `RevealDeckTopCardsAndSelect` (isSendAllCardsToSamePlace/revealedCardsCoroutine), `SimplifiedRevealDeckTopCardsAndSelect` (6 of 11 params, 409 calls — highest priority), all `PlayXToken` helpers (field-capacity check).
6. **Highest call-count helpers, in order**: `PlayPermanentCards` (977) > `ChangeDigimonDP` (584) > `SimplifiedRevealDeckTopCardsAndSelect` (409) > `DigivolveIntoHandOrTrashCard` (342) > `DeletePeremanentAndProcessAccordingToResult` (322). These 5 dominate call volume and should be prioritized/most-carefully-reviewed regardless of gap class.

No helper's AS-IS definition was un-locatable; all 91 rows are backed by a confirmed AS-IS source location.
