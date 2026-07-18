# Symbol-map porting guide (weak-model symbol resolution)

**Read this before porting any AS-IS card into the mirror.** Its job is to turn symbol
resolution from a *search* into a *lookup*, so a weak model (Haiku / local LLM) stops
falsely declaring real mirror surface "absent" and STOP-ing on cards it could finish.

Companion files (same directory):
- `symbol_map.csv` — the lookup table. One row per AS-IS symbol, every mirror surface
  **grep-verified at generation time**. Columns:
  `asis_symbol, freq_cards, asis_source, status, mirror_symbol, mirror_path, signature_delta, example_card, example_line, notes`.
- `symbol_map_coverage.md` — frequency-weighted coverage + the infra-gap list.
- `symbol_map_seed.json` — curated overrides (renames, signature deltas, examples, `absent` gaps).
- `tools/build_symbol_map.py` — regenerates the CSV + coverage from the IR DB + a fresh
  mirror grep + the seed. Re-run after porting cards or editing the seed.

## 0. The one fact that dissolves most false STOPs

**The mirror is a 1:1 *name* mirror of AS-IS.** ~99.9% of AS-IS symbol names exist in the
mirror **under the exact same name** as real callable code. The Haiku pilot failed by
declaring `AddMemory`, `Mode.Destroy`, `DestroyPermanentsClass`, `GetBattleAreaDigimons`,
`ReturnToLibraryBottom` "absent" — **all five exist.** They were not renamed; the model
just failed to locate them.

So the porting difficulty is almost never "what is this symbol called now". It is:
1. **the signature/access idiom changed** (a `Player` became a `PlayerId`; an `ICardEffect`
   arg became an `InstanceId`; a ctor gained a `Context`/`cause` param), and
2. **a handful of things genuinely have no mirror surface** (frame/slot model, some UI).

The CSV's `signature_delta` column is where the value is. `status = OK` means the surface
exists — **do not STOP on an OK row**, apply the delta and move on.

## 1. How to use the table

For every AS-IS symbol you need:
1. Look it up in `symbol_map.csv` (`asis_symbol` column).
2. `status = OK` → the mirror surface exists. Use `mirror_symbol` + apply `signature_delta`.
   Open `example_card` at `example_line` for a working call site if unsure.
3. `status = ABSENT` → **genuine infra gap, no mirror surface.** STOP (see §4). These are the
   only legitimate symbol-resolution STOPs.
4. Symbol not in the table → follow the **miss protocol** (§4).

Rows are sorted by `freq_cards` (how many of the 3,918 AS-IS cards use the symbol), so the
most common lookups are at the top.

## 2. Systematic substrate-translation rules (canonical)

These are mechanical and apply everywhere. They are *not* one row per symbol — internalize
them. (Every rule below was extracted from the 40 substitution-header exemplar/pilot cards
and cites a witness `CARDID:line`.)

### 2.1 Async / coroutine
- `IEnumerator <method>` → `async Task <method>` (every effect coroutine, every
  `SelectXCoroutine`, `SuccessProcess`, `AfterSelect...`). — EX7_014:102
- `yield return ContinuousController.instance.StartCoroutine(X)` **and** bare
  `yield return StartCoroutine(X)` → `await X`. (the "BT8_092 idiom") — BT21_030:37
- `StartCoroutine("MethodName")` (Unity string dispatch) → `await MethodName()`. — BT17_026:452
- A lone/terminal `yield return null` in a body with no real await → the method becomes a
  non-async `Task`-returning method ending in `return Task.CompletedTask;`. — ST17_13:269
- An `IEnumerator SuccessProcess()` passed **invoked** (`successProcess: SuccessProcess()`)
  → `async Task SuccessProcess()` passed as a **bare delegate** (`successProcess: SuccessProcess`);
  the mirror bridge wants `Func<Task>`. — BT14_030:174

### 2.2 Player is now a PlayerId
- `card.Owner` is a **`HeadlessPlayerId`** (a value), not a live `Player`.
- To use a `Player`-instance member, wrap: `new Player(card.Context, card.Owner).<member>`.
  Applies to `.HandCards`, `.TrashCards`, `.SecurityCards`, `.MemoryForPlayer`,
  `.GetBattleAreaPermanents()`, `.GetFieldPermanents()`, `.PermanentEffects`, `.Enemy`,
  `.UntilOwnerTurnEndEffects`, `.UntilOpponentTurnEndEffects`, `.UntilEachTurnEndEffects`,
  `.UntilEndBattleEffects`, `.UntilCalculateFixedCostEffect`, `.CanReduceCost`,
  `.MaxDP_DeleteEffect`. — BT17_026:147, P_223:105
- **Exception — extension methods** with AS-IS signatures hang directly off the `PlayerId`,
  call them on `card.Owner` with no wrapper: `card.Owner.AddMemory(n, cardEffect)`,
  `card.Owner.GetBattleAreaDigimons()`. — BT9_111:218, BT9_009:154
  (Split by receiver: `.GetBattleAreaPermanents()` is an extension on the id, but
  `.GetFieldPermanents()` needs the wrapper — BT25_104:116 vs :126.)
- `card.Owner.Enemy` → `new Player(card.Context, card.Owner).Enemy!` (null-forgiving; the
  mirror `Player.Enemy` is `Player?` but in a 2-player game it is non-null). Add `.PlayerId`
  when an id is wanted. Shorthand when you only want the opponent id:
  `CardEffectCommons.OpponentOf(card)`. — BT18_042:166, EX5_053:190
- Player equality → id equality: `player == card.Owner.Enemy` →
  `player.PlayerId == CardEffectCommons.OpponentOf(card)`. — EX8_030:74

### 2.3 Permanent-of-this-card and Permanent predicates
- `card.PermanentOfThisCard()` (returns a `PermanentView`) →
  `ICardEffect.ResolvePermanentOfThisCard(card)` (returns a mutable `Permanent`). Needed
  whenever you touch `.DigivolutionCards`, `.willBeRemoveField`, `.EffectList`, or need
  value-equality. — BT17_026:542
  - For identity comparisons against a `PermanentView`, compare `.InstanceId` /
    `.TopInstanceId` instead. — BT22_040:147
- **id-adapter pattern (critical — never collapse a predicate).** The mirror
  `SelectPermanentEffect.SetUp` `canTargetCondition`, and `MatchConditionPermanentCount`,
  take `Func<HeadlessEntityId, bool>`. AS-IS predicates are `Func<Permanent, bool>`.
  **Keep the AS-IS predicate verbatim** and bolt on this fixed adapter:
  ```csharp
  Permanent? PermanentOf(HeadlessEntityId id) =>
      card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
          ? new Permanent(card.Context, id, rec.OwnerId)
          : null;

  bool CanSelectPermanentById(HeadlessEntityId id)
  {
      Permanent? permanent = PermanentOf(id);
      return permanent is not null && CanSelectPermanentCondition(permanent);
  }
  ```
  (inline lambda form `id => PermanentOf(id) is { } p && Cond(p)` is also fine). — BT17_026:132-143
  - Asymmetry: `HasMatchConditionPermanent` **has** a `Func<Permanent,bool>` overload
    (CardEffectCommons.cs:4079) — pass the predicate directly, no adapter.
    `MatchConditionPermanentCount` is id-only — pass the adapter. — ST17_13:143 vs :145
  - `HasMatchConditionPermanent` / `MatchConditionPermanentCount` also gained a **leading
    `card` param** (the card-less overload was removed). — BT9_009:104

### 2.4 Enums, traits, colors
- `CardColor.Yellow/Green/Black/...` (enum) → **string** `"Yellow"`/`"Green"`/`"Black"`. Mirror
  `CardColors` is `IReadOnlyList<string>`. The *call form* is preserved 1:1 — `HasCardColor("X")`
  stays `HasCardColor`, `CardColors.Contains("X")` stays `CardColors.Contains`. — LM_047:62,92
  - **Exception**: when you are *building* a `List<CardColor>` (e.g. `ChangeCardColors`,
    `ChangeBaseCardColors`, `PartitionCondition`), the enum form is retained. — BT17_026:355
- Derived trait/level convenience getters that the mirror `CardSource` lacks are **inlined to
  their getter body** using 1st-order primitives (sanctioned assembly, §3):
  `HasAppmonTraits → EqualsTraits("Appmon")`, `HasStandardAppTraits → EqualsTraits("Stnd.")`,
  `HasTSTraits → EqualsTraits("TS")`, `IsLevel6 → HasLevel && Level == 6`,
  `IsLevel4 (property) → IsLevel(4) (method)`. — EX10_029:84, BT18_034:326, BT24_062:62
- `cardSource.CardID` (per-instance id) → `cardSource.InstanceId.Value`. — BT21_030:155

### 2.5 Process/effect class ctors gain substrate params
Mutation classes prepend a `Context` and/or translate the AS-IS `hashtable`/`ICardEffect`
arg into `(causeEffectSourceId = cardEffect.EffectSourceCard?.InstanceId, cardEffect)`:
- `new IRecovery(card.Owner, 1, activateClass)` → `new IRecovery(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId)`. — BT14_030:451
- `new IDiscardHand(card, hashtable).Discard()` → `new IDiscardHand(card).Discard(null, activateClass.EffectSourceCard?.InstanceId)`. — BT17_026:460
- `new IDegeneration(perm, 1, activateClass)` → `new IDegeneration(perm, 1, activateClass.EffectSourceCard?.InstanceId, cardEffect: activateClass)`. — ST17_13:182
- `new IDestroySecurity(player, ...)` / `new IReduceSecurity(...)` → prepend `card.Context`;
  IReduceSecurity's UI `ref skillInfos` param → `refCollector: null`. — BT18_034:182, BT18_042:295
- `new DrawClass(card.Owner, 1, activateClass)` → `new DrawClass(card.Context, card.Owner, 1, cause, activateClass)`. — EX10_045:439
- `new SuspendPermanentsClass(list, CardEffectHashtable(x)).Tap()` → `new SuspendPermanentsClass(list, activateClass, isBlock:false).Tap()`. — EX11_074:168
- `IUnsuspendPermanents(list, activateClass)` is **unchanged** (cause derived internally). — BT5_086:114
- `IReadOnlyList` → `List` via `.ToList()` at `customRootCardList:` / list-consuming sites
  (`selectedPermanent.DigivolutionCards.ToList()`). — BT5_086:210

### 2.6 UI is stripped; game state is kept
- **Strip entirely** (no mirror surface): `CreateDebuffEffect` / `CreateBuffEffect` (Effects),
  `ShowCardEffect2`, `PlaySE(...)` / `BuffSE` / `DebuffSE` / `ContinuousController.instance.PlaySE`,
  `HideDeleteEffect` / `HideHandBounceEffect` / `HideDeckBounceEffect`,
  `yield return new WaitForSeconds(...)`, brainStorm UI (`CloseBrainstrorm` etc.). — BT17_026:606, BT5_086:228
  - **Keep any guard predicate** that wrapped a stripped UI call, on the effect's `CanUse`
    condition, so effect validity is preserved. — BT17_026:607
  - **Exception**: `ShowReducedCost(hashtable)` is retained — the mirror has a UI-only no-op
    bridge for it. — P_223:164
- **Kept UI-decorators that DO exist**: `SetNotShowUI(true)`, `SetNotShowCard()`,
  `SetUpSkillInfos(...)`, `SetUpCustomMessage(...)`,
  `GManager.instance.userSelectionManager.SetBool/SetBoolSelection/SetIntSelection/WaitForEndSelect/SelectedBoolValue/SelectedIntValue`,
  and `GManager.instance.GetComponent<SelectXEffect>()` (Select Permanent/Card/Hand/Attack) —
  **all retained verbatim**. — BT18_034:155, ST17_13:147
- Load-bearing state flags are **kept**: `willBeRemoveField = false` (sweep survivor-fix
  reads it). — BT2_082:187

### 2.7 Timing-key dialect (trigger wiring)
- `[When Digivolving]`: AS-IS registers under `EffectTiming.OnEnterFieldAnyone` +
  `CanTriggerWhenDigivolving` gate → mirror registers under the dedicated
  `EffectTiming.WhenDigivolving` key (mirror `DigivolveAction` resolves only that key;
  **double-key registration is forbidden**, there is a STOP guard). The
  `CanTriggerWhenDigivolving(hashtable, card)` gate body is unchanged. A sibling effect that
  queries the WD effect list remaps its query key in lockstep
  (`EffectList(OnEnterFieldAnyone)` → `EffectList(WhenDigivolving)`). — AD1_011:127, BT22_040:154
- Others stay identical: `[On Play]` → `OnEnterFieldAnyone` + `CanTriggerOnPlay`;
  `[When Attacking]` → `OnAllyAttack` + `CanTriggerOnAttack`/`CanTriggerOnPermanentAttack`;
  `[Security]` → `SecuritySkill` + `SetIsSecurityEffect(true)` + `CanTriggerSecurityEffect`;
  `[Main]` option → `OptionSkill` + `CanTriggerOptionMainEffect`;
  `<Delay>` → `OnDeclaration` + `CanDeclareOptionDelayEffect`. — EX7_014:61,130

### 2.8 Frame/slot model does not exist (RD-P6C1-1 / -2)
The mirror has **no** battle-area frame/slot model (it was a Unity UI artifact; the TCG has
no battle-area count limit).
- Empty-frame capacity check `card.Owner.fieldCardFrames.Count(f => f.IsEmptyFrame() &&
  f.IsBattleAreaFrame()) >= 1` → **remove it**; keep the co-conjunct half; token-play commons
  re-checks capacity downstream. — BT19_091:99
- `card.CanPlayCardTargetFrame(perm.PermanentFrame, <bool>, effect)` (occupied-frame branch)
  → `permanent.TopCard.Owner == card.Owner && card.CanEvolve(permanent, <same bool>)`. — LM_054:154

### 2.9 Preserve AS-IS bugs and misspellings (no-simplification)
- Original card bugs are mirrored **verbatim** (e.g. BT17_026 labels an effect "blue" but adds
  `CardColor.Red` — kept). — BT17_026:355
- Misspelled AS-IS API names are kept **exactly**: `DeletePeremanentAndProcessAccordingToResult`
  / `BouncePereman...` / `SuspendPereman...` ("Pereman"),
  `EffectTiming.OnPermamemtReturnedToHand` ("Permamemt"), `EffectDiscription` ("Discription").
  Do **not** "correct" them — the corrected name does not exist. — BT14_030:168, BT18_098:58

## 3. Inline-assembly precedent (allowed)

When a convenience getter/property is genuinely absent from the mirror `CardSource`/`Player`,
you may **reconstruct it inside the card file from existing 1st-order primitives**, reproducing
the AS-IS getter body exactly. This is **not invention** — it is the same-nature move as the
id-adapter. Do **not** add it to the shared `Script/` layer, and do **not** simplify the body.

Canonical precedents (Sonnet S1 pilot):
- **EX8_037:43-58** — `CardSource.OptionCardColors` has no mirror property. Reconstructed as a
  private static `OptionCardColorsOf(CardSource)` whose body reproduces the AS-IS getter:
  `!IsOption → Array.Empty<string>()`; `IsDigimon → DualCardColors`; else `CardColors`.
- **P_223:307** — `HasOnmyoOrPluginTraits` (derived getter) inlined 1:1 as
  `cardSource.EqualsTraits("Plug-In") || cardSource.EqualsTraits("Onmyojutsu")`.
- **BT18_098:54-65** — `CardEffectCommons.OptionSecurityEffect(card)` inlined as a private
  `OptionSecurityEffectOf` from `EffectList(SecuritySkill)` + `EffectDiscription.Contains("[Security]")`.

Only assemble from primitives that themselves resolve to real mirror surface. If a *primitive*
you'd need is itself absent, that is a real gap — STOP (§4).

## 4. Miss protocol (this is how the table grows)

When a symbol is **not** in `symbol_map.csv`, or is marked `status = ABSENT`:

1. **One grep.** Search the mirror once for the bare token:
   `grep -rn --include=*.cs "\bSYMBOL\b" src` (mirror is UTF-8; no `--binary-files` flag needed).
   - Found as real (non-comment) code → it exists. Use it (apply the closest matching rule
     from §2). Consider adding a verified row to `symbol_map_seed.json` and re-running the
     build script so the next porter gets a lookup.
2. **Still nothing after one grep** → do **not** keep searching, and do **not** guess or invent
   a bridge. **STOP** with an explicit, symbol-named marker:

   `RD-SYMMAP-<SymbolName>` — e.g. `RD-SYMMAP-HatchDigiEggClass`.

   Record the AS-IS symbol, the card you were porting, and the AS-IS call signature. **The
   STOP is the input that grows the table**: a strong model later resolves the symbol (finds a
   rename, defines a real primitive, or confirms the gap), adds a seed row, and re-runs the
   build script. Never dissolve a STOP by mashing a predicate or faking a surface.

Currently-known `ABSENT` infra gaps (see `symbol_map_coverage.md` for the live list):
`HatchDigiEggClass`, `OwnerHas1OrLessTamers`. Also latent, effect-runs-but-consumer-stubbed
(port the registration arm, STOP the consumer): `SelectDigiXrosClass` substitution
(RD-R5-04), `IBattle.Battle()` (RD-EXT2B-01), Burst/Arts execution (RD-P6C1-6 / RD-P6C2-10).

## 5. Regenerating / extending

```
python3 docs/porting/tools/build_symbol_map.py
```
Re-reads the IR DB (`card_ir.sqlite`, AS-IS inventory + frequency), re-greps the mirror
(comment-stripped, so a symbol only present inside an `// AS-IS ...` note does **not** count),
merges `symbol_map_seed.json`, and rewrites `symbol_map.csv` + `symbol_map_coverage.md`.
No engine code is touched and no build is run. Add verified pairs to the seed and re-run —
the table only ever asserts a mirror surface that a live grep confirmed.
```
```
