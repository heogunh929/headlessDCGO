# Effect-model rebuild — Phase 1 (FOUNDATION) missing-member work-list

Written alongside the FOUNDATION pass of `docs/audit/effect_model_rebuild_design_2026-07-13.md` step 1
("파운데이션: ICardEffect 추상클래스(A) + 74 인터페이스(B) + enums + CEntity_Base/Effect/EffectController +
ActivateClass"). Every item below is a member/type the FOUNDATION files reference **verbatim** (AS-IS-named,
per the porting brief: "reference, do NOT stub-replace") because it is not yet ported. Nothing here is fixed by
this pass — this is the work-list for the following rebuild steps (2: Hashtable layer, 3: per-kind effect
classes, 4: factory, 5: trigger/window rules, 6: engine dispatch cutover).

## Files written this pass

- `Assets/Scripts/Script/ICardEffect.cs` — abstract `ICardEffect` (1:1, AS-IS ICardEffect.cs) +
  `ActivateICardEffect` + `ActivateICardEffectExtensionClass`.
- `Assets/Scripts/Script/CardEffectInterfaces.cs` — the 74 AS-IS marker interfaces + ported `CEntity_Effect`
  (AS-IS CEntity_Effect.cs, relocated here — see that file's header).
- `Assets/Scripts/Script/CEntity_EffectController.cs` — 1:1 port + `EmptyEffectClass` + the new
  `CEntity_EffectControllerStore` (per-instance backing, not AS-IS — see "New non-AS-IS infrastructure" below).
- `Assets/Scripts/Script/CheckEffectDisabledClass.cs` — 1:1 port + `ValidateCardEffectElement`.
- Edited `Assets/Scripts/Script/CardEffectCommons/CardEffectInterfaces.cs` — removed the pre-rebuild
  `ICardEffect`/`IActivatedCardEffect`/`CEntity_Effect` trio (collided with the new abstract-class `ICardEffect`).
- Edited `Assets/Scripts/Script/CardEffectCommons/CardSource.cs` — added the `cEntity_EffectController` accessor.

Build check: `dotnet build src/HeadlessDCGO.Engine/HeadlessDCGO.Engine.csproj` — the 4 new/rewritten files
compile clean **except** the 4 documented MISSING TYPES below (expected); every other error (694 total, ~278
files: every ported card under `Assets/Scripts/CardEffect/**`, `CardEffectFactory.cs`, `ActivatedEffects.cs`,
`ActivatedEffect.cs`, every `Assets/Scripts/CardEffect/TestFixtures/Tfx*.cs`) is the **expected big-bang
cascade** from replacing the old `ICardEffect`/`CEntity_Effect` shape — out of scope for this pass per the
task brief ("DO NOT try to make the whole engine compile... that is the next phase's problem").

## MISSING types (referenced verbatim in `CardEffectInterfaces.cs`, not defined anywhere on the mirror)

| Type | AS-IS anchor | Used by |
|---|---|---|
| `CardColor` | `CardEffectInterfaces.cs:69-80` (a `List<CardColor>`) | `IChangeCardColorEffect`, `IChangeBaseCardColorEffect`. The mirror already has a DIFFERENT color model (`CardSource.CardColors`/`BaseCardColors` return `IReadOnlyList<string>`, CardEffectCommons/CardSource.cs) — reconciling `CardColor` vs `string` colors is a rebuild-step-3 decision (the per-kind `ChangeCardColorClass`/`ChangeBaseCardColorClass` port), not a FOUNDATION one. |
| `JogressCondition` | `CardEffectInterfaces.cs:395-398` | `IAddJogressConditionEffect`. |
| `DigiXrosCondition` | `CardEffectInterfaces.cs:402-405` | `IAddDigiXrosConditionEffect`. |
| `BurstDigivolutionCondition` | `CardEffectInterfaces.cs:416-419` | `IAddBurstDigivolutionConditionEffect`. |

`AssemblyCondition`/`LinkCondition`/`AppFusionCondition` (siblings of the three above) already exist at
CardEffectCommons/Conditions.cs — not listed as missing.

## MISSING members (referenced verbatim, GManager-style — "reference, don't stub")

### `GManager` (Assets/Scripts/Script/CardEffectCommons/GManager.cs)

- `GManager.instance.GetComponent<OptionalSkill>()` (AS-IS ICardEffect.cs:1054) — the player's optional-effect
  yes/no prompt provider. Game logic (not pure UI), referenced verbatim in `ActivateICardEffectExtensionClass.
  Activate_Optional`.
- `GManager.instance.autoProcessing` (AS-IS GManager.cs:84, field) — `.StackSkillInfos(ICardEffect, EffectTiming)`
  / `.RuleProcess()` (AS-IS ICardEffect.cs:1283/1285), referenced verbatim in `Activate_Effect_Execute`'s tail
  (explicitly called out by the porting brief as one of the pieces that MUST stay, not get UI-stripped).
- `GManager.instance.attackProcess` (AS-IS GManager.cs:99, field) — not referenced by this pass's files, but
  listed per the brief ("GetComponent<X>()... list each missing member") since it is the other AS-IS
  `GManager` field alongside `autoProcessing`; the mirror `GManager` (CardEffectCommons/GManager.cs) currently
  exposes neither.

### `CardEffectCommons` Hashtable builders (Assets/Scripts/Script/CardEffectCommons/CardEffectCommons.cs — the
ported file; AS-IS anchor DCGO `CardEffectCommons/HashtableSetting.cs`, rebuild step 2)

- `CardEffectCommons.OnPlayCheckHashtableOfCard(CardSource)` — AS-IS HashtableSetting.cs:213.
- `CardEffectCommons.WhenDigivolvingCheckHashtableOfCard(CardSource)` — AS-IS HashtableSetting.cs:232.
- `CardEffectCommons.OnDeletionHashtable(List<Permanent>, ICardEffect, IBattle, bool)` — AS-IS
  HashtableSetting.cs:85. Also needs the `IBattle` type (AS-IS `CardController.cs`; no mirror type at all).
- `CardEffectCommons.OnAttackCheckHashtableOfCard(CardSource, ICardEffect)` — AS-IS HashtableSetting.cs:299.

All four are used by `ICardEffect.IsOnPlay`/`IsWhenDigivolving`/`IsOnDeletion`/`IsOnAttack`.

### `CardSource` / `Permanent` / `Player` members (AS-IS anchors in DCGO `CardSource.cs`/`Permanent.cs`/`Player.cs`)

- `CardSource.EffectList(EffectTiming)` / `EffectList_ForCard` / `EffectList_ExceptAddedEffects` /
  `EffectList_ForCard_ExceptAddedEffects` — AS-IS CardSource.cs:981-1029. Used by
  `CEntity_EffectController.GetCardEffects` (`source.EffectList(EffectTiming.None)`, security-card branch).
- `Player.EffectList(EffectTiming)` — AS-IS Player.cs:830. Used by `CEntity_EffectController.GetCardEffects`
  and `CheckEffectDisabledClass.PotentiallyDisablingEffects` ("Player Effects" region). Mirror `Player`
  (CardEffectCommons/Player.cs) deliberately does NOT stub this today (its own header: "EffectList — effects
  are modeled via EffectRegistry, not per-player bag lists").
- `Player.SecurityCards` — used by `CEntity_EffectController.GetCardEffects` ("Effects from security" region).
- `Permanent.cardSources` (AS-IS Permanent.cs, the raw stack list INCLUDING the top card) — used by
  `CEntity_EffectController.GetCardEffects` and `CheckEffectDisabledClass.PotentiallyDisablingEffects`. The
  mirror `Permanent.DigivolutionCards` (CardEffectCommons/Permanent.cs) is the under-cards ONLY (excludes top) —
  not a drop-in substitute; `cardSources` needs its own accessor (`DigivolutionCards.Prepend(TopCard)` in top-
  first-vs-AS-IS-order — the exact AS-IS ordering needs checking against Permanent.cs before porting).
- `Permanent.EffectList_Added(EffectTiming)` — AS-IS Permanent.cs:1380. Used by
  `CheckEffectDisabledClass.PotentiallyDisablingEffects` ("Permanent Effects" region).
- `Permanent.LinkedCards` already exists (CardEffectCommons/Permanent.cs) — NOT missing, listed here only
  because `CheckEffectDisabledClass` also reads it (no action needed).

### Misc

- `IEnumerableExtension.Filter<T>(this List<T>, Func<T,bool>)` / `Filter<T>(this T[], Func<T,bool>)` — AS-IS
  DCGO `IEnumerableExtension.cs:44/49` (`list.Where(x => getElement(x)).ToList()`). The mirror
  `Assets/Scripts/Script/IEnumerableExtension.cs` is itself an unported 7-line skeleton — out of this goal's
  file list, referenced verbatim in the ported `CEntity_Effect.GetCardEffects` /
  `CEntity_EffectController.GetCardEffects`.

## Design items (structural gaps, not simple missing-member references)

- **PERMANENT-PERMANENTVIEW-DUALITY**: `CardSource.PermanentOfThisCard()` (CardEffectCommons/CardSource.cs)
  returns a `PermanentView` (Stack-projection read view: `.TopInstanceId`/`.IsEmpty`), but AS-IS callers
  (`ICardEffect.CanActivate`/`IsOnDeletion`) expect a `Permanent` (`.TopCard`/`.IsDigimon`/`.LinkedCards`). This
  pass bridges the two AT THE ICardEffect.cs CALL SITES ONLY (`ICardEffect.ResolvePermanentOfThisCard`,
  constructing a real `Permanent` from the `PermanentView`'s `TopInstanceId` when non-empty) — every OTHER AS-IS
  caller of `PermanentOfThisCard()` ported in later steps will need the same bridge, or the two types should be
  unified (a bigger, deliberately-deferred architectural call — see the rebuild design doc's step ordering).
- **CARDSOURCE-EQUALITY**: `ICardEffect.CanActivate`/`IsSameEffect` compare `CardSource`/`Permanent` instances
  with `==`/`!=`, assuming AS-IS's stable per-card object identity. The mirror `CardSource`/`Permanent` are
  views reconstructed fresh on every access (`Permanent.TopCard => new(...)`) with NO `Equals`/`GetHashCode`
  override, so these comparisons are reference-equality on freshly-allocated objects — they will not behave as
  AS-IS intends (e.g. `EffectSourceCard == permanentOfThisCard.TopCard` is `false` even for the "same" card)
  until `CardSource`/`Permanent` gain instance-id-based value equality. Ported verbatim (no equality override
  invented in this pass — a cross-cutting decision affecting every existing mirror consumer of these two types,
  out of the FOUNDATION file list). **High-priority pre-req for step 2+** — several of the ported gates
  (CanActivate's Inherited/Linked-effect determination) are silently wrong until this lands.
- **CARDEFFECTREGISTRAR-ACTIVATED-SKIP**: the OLD (pre-rebuild) `IActivatedCardEffect` marker (no AS-IS analog —
  told `CardEffectRegistrar` to skip auto-registering activation-flow effects on enter-play) was dropped when
  the old `ICardEffect`/`CEntity_Effect` trio was removed from CardEffectCommons/CardEffectInterfaces.cs. AS-IS
  has no equivalent concept to port instead — `CardEffectRegistrar`'s "should I auto-register this on enter-
  play" decision needs a new home (probably `ICardEffect.IsDeclarative`/`IsOptional` state, or a scan of which
  marker interface the effect implements) once the registrar itself is cut over (rebuild step 6).
- **AS-IS `CalculateOrder`/`EffectDuration` already exist on the mirror** (CardEffectCommons/ModifierHelpers.cs
  and `HeadlessDCGO.Engine.Headless.Effects.EffectDuration` respectively) with a 1:1 AS-IS value set — NOT
  redefined in ICardEffect.cs per the brief's "if it exists, reference it" rule (extended here from the
  `EffectTiming` case explicitly named in the brief). Confirmed both already carry every AS-IS enum member.

## Expected cascade (NOT fixed in this pass — rebuild steps 3/4/6's job)

Replacing the old `ICardEffect { EffectBinding ToBinding(string) }` interface + `IActivatedCardEffect` +
`abstract class CEntity_Effect { IReadOnlyList<ICardEffect> CardEffects(...) }` with the AS-IS-shaped
`abstract class ICardEffect` + 74 marker interfaces + `abstract class CEntity_Effect { virtual List<ICardEffect>
CardEffects(...) }` breaks every consumer of the old shape:

- `CardEffectCommons/CardEffectFactory.cs` (28 errors) — `IActivatedCardEffect` no longer exists.
- `CardEffectCommons/ActivatedEffects.cs` / `ActivatedEffect.cs` (102+2 errors) — same.
- `CardEffectCommons/CardEffectDispatch.cs`, `CardEffectRegistrar.cs` — pattern against the OLD `CEntity_Effect`
  shape (`IReadOnlyList<ICardEffect>` abstract override) — will fail once anything tries to instantiate a card
  through them (not yet surfaced as a build error because nothing in the built project currently forces their
  generic constraints to resolve against a broken override, but every ported card class IS such a forcing use).
- **~274 ported card classes** (`Assets/Scripts/CardEffect/**/*.cs`, e.g. `BT1_001.cs`...`ST7_10.cs`) — each
  overrides `CardEffects(EffectTiming, CardSource)` returning `IReadOnlyList<ICardEffect>` (the OLD abstract
  signature); the NEW `CEntity_Effect.CardEffects` is `virtual List<ICardEffect>` — CS0508 return-type mismatch
  on every one.
- **~85 `Assets/Scripts/CardEffect/TestFixtures/Tfx*.cs`** — same CS0508 shape.

Full measured count: 694 build errors across ~278 files (`dotnet build` on `HeadlessDCGO.Engine.csproj`,
2026-07-13). This is the documented rebuild-design "빌드/테스트 red" window between step 1 and step 6
(`docs/audit/effect_model_rebuild_design_2026-07-13.md` §3/§4) — expected, not a regression to chase down now.

## New non-AS-IS infrastructure added this pass (substrate, not a game-logic port)

- `CEntity_EffectControllerStore` (Assets/Scripts/Script/CEntity_EffectController.cs) — a
  `ConditionalWeakTable<EngineContext, ConcurrentDictionary<HeadlessEntityId, CEntity_EffectController>>`
  backing `CardSource.cEntity_EffectController`, so the same controller (and its `UseEffectsThisTurn` list)
  is returned across repeated `CardSource` view reconstructions for the same card in the same match. AS-IS has
  no analog (the original controller's lifetime is just its Unity GameObject's). Deliberately NOT promoted onto
  `EngineContext` itself (alongside `OnceFlags`/`PlayerTurnCounters`) in this pass — keeps the diff scoped to
  the FOUNDATION files; that promotion (and wiring `InitUseCountThisTurn`/turn-reset into the turn loop) is a
  later-phase call.
