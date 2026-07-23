# Match check — part 07/13

Scope: `docs/audit/manifest/both_part_07.txt` (9 files). AS-IS = `DCGO/Assets/Scripts/<relpath>`, TO-BE =
`src/HeadlessDCGO.Engine/Assets/Scripts/<relpath>`. Full-text read both sides for every file; verdicts are my own,
formed from source observation only (existing audit verdicts/comments not used as evidence).

## 1. Script/EditDeck.cs — SCOPE EXCLUSION (legitimate), 0% ported

AS-IS: 1355-line `MonoBehaviour` — the deck-editor screen (drag/drop card pool, `ScrollRect`, `InputField`,
`Draggable_Card`, filter/paging UI, "clear deck"/"set icon" command dialogs). Read in full: every method is UI
wiring (`CheckButtonEnabled`, `SetUpCreateDeck`, `MatchCondition` = card-pool search filter, `OnEndDrag`, etc.); no
game-rule computation (deck legality, cost math) lives here — it only calls into already-ported `CEntity_Base`/
`CardSource` accessors.

TO-BE: 7-line skeleton stub (`// TODO: Skeleton only.`), Category `UnityMixedLogic`.

Verdict: legitimate — pure Unity editor-screen logic, no engine-rule content, zero live references from the ported
engine (`grep` confirms no TO-BE file outside this stub mentions `EditDeck`). Flagged as an unported gap for
completeness (manifest requires "no omissions"), not a fidelity mismatch.

## 2. Script/OfficialCardListUtility.cs — SCOPE EXCLUSION (legitimate), 0% ported

AS-IS: 1328-line static class — an HTML scraper/parser for the official Bandai card-list website
(`System.Net`/`WebClient`, regex-free string splitting on `<liclass="image_lists_itemdatapage-N">` markers,
JPN/ENG text-asset diffing) used to build the local card database at authoring/build time. Not runtime game logic.

TO-BE: 7-line skeleton stub, Category `DataLoader`.

Verdict: legitimate — build-time data-import tooling, not consulted during a match. Zero references from TO-BE
elsewhere (`grep` confirms). Unported gap noted, not a mismatch.

## 3. Script/CardEffectInterfaces.cs — MATCHES (1 stale-comment finding)

AS-IS: 547 lines, 74 marker interfaces (`IDisableCardEffect` … `ICollisionEffect`), one per `#region`, purely
declarative (method signatures only).

TO-BE: 700 lines. Compared every interface 1:1 — all 74 are present, same name, same method signatures, each
tagged with its AS-IS line range in a comment. The extra length vs. AS-IS is the `CEntity_Effect` abstract base
class (AS-IS's *separate* file `CEntity_Effect.cs`), deliberately relocated here per the file's own header to
avoid a namespace collision with an older mirror `ICardEffect`/`CEntity_Effect` trio that used to live at
`CardEffectCommons/CardEffectInterfaces.cs` — verified that file's current header (read separately) documents the
same removal, consistent both ways. Only intentional signature change: `IOptionResolutionEffect.Resolve`
`IEnumerator` → `Task` (declared and explained, consistent with the project-wide coroutine→Task translation used
everywhere else).

Finding (stale doc, not a functional bug): the file header lists `CardColor`, `JogressCondition`,
`DigiXrosCondition`, `BurstDigivolutionCondition` as "MISSING TYPES … not defined anywhere on the mirror yet".
Checked source: all four now exist —
`CardColor` is an `enum` in `Assets/Scripts/Script/CEntity_Base.cs:15` (used by `DataBase.CardColorNameDictionary`
and elsewhere), and `JogressCondition`/`DigiXrosCondition`/`BurstDigivolutionCondition` are classes in
`Assets/Scripts/Script/CardSource.cs:2755/2797/2821`, each with a live implementer (`AddJogressConditionClass.cs`,
`AddDigiXrosConditionClass.cs`, `AddBurstDigivolutionConditionClass.cs`). The header comment was written at an
earlier FOUNDATION pass (file last touched 2026-07-13, commit `615ab26b`) and never refreshed after those types
landed — cosmetic documentation drift only; the interfaces themselves compile and are used correctly today.

## 4. Script/CardEffectCommons/DNADigivolveEffects.cs — MATCHES

AS-IS: 673 lines — DNA-digivolve-with-a-hand/trash-card helpers (`PlayTempPermanent`, `CardFulfillsRequirement`,
`SelectHandCard`/`SelectTrashCard`/`SelectPermanent`, `CanJogressWithHandOrTrash`,
`DNADigivolveWithHandOrTrashCardIntoHandOrTrash`, `DNADigivolvePermanentsIntoHandOrTrashCard`,
`GetJogressConditions`, plus a private Photon `SetJogressEvoRootsController` RPC relay).

TO-BE: 567 lines. Walked every method against AS-IS body, branch by branch:
- `DNADigivolvePermanentsIntoHandOrTrashCard`: AS-IS-signature wrapper here delegates to the substrate overload in
  `CardEffectCommons.cs:2178` (confirmed it exists) — matches the file's own "bridge W3" note.
- `TempMaterialView` (mirrors `PlayTempPermanent(card, finalCard:false)`) replaces the literal
  `FieldPermanents[frameID]=tempPermanent` / null-back slot write with a read-only `Permanent` snapshot view; every
  call site that relied on the AS-IS null-back is annotated "no-op (read-only view)" at the correct spot.
- `CardFulfillsRequirement`/`PermanentFulfillsRequirement`/`SelectHandCard`/`SelectTrashCard`/`SelectPermanent`/
  `CanJogressWithHandOrTrash`: line-for-line logic match (only `.Filter`/`.Some`→`.Where`/`.Any`, `IEnumerator`→
  `Task`, `Player`→`Player(ctx,id)` substrate translations).
- `DNADigivolveWithHandOrTrashCardIntoHandOrTrash`: full 200-line control flow (dual-select-source UI branch,
  permanent-first vs. card-first ordering, rollback on abort) reproduced 1:1; confirmed by re-reading the AS-IS
  body that `successProcess`/`failedProcess` are genuinely never invoked there (only declared), matching the TO-BE
  comment that also declares-but-drops them — not a hidden behavior loss.
- `GetJogressConditions`: verbatim, including the AS-IS dead-code quirk (`PermanentCondition`/
  `FullPermanentCondition{1,2}` locals computed but never referenced by the returned `JogressCondition`) — kept
  intentionally per the project's no-simplification rule.
- The private Photon `SetJogressEvoRootsController` RPC relay class is absent in TO-BE — legitimate: it only existed
  to shuttle a player's selection across the network via `[PunRPC]`, replaced project-wide by the async choice-port
  pattern (consistent with how every other Select* file in this codebase drops Photon RPC plumbing).

No mismatch found.

## 5. Script/DataBase.cs — PARTIAL MIRROR, self-consistent (legitimate)

AS-IS: 1018 lines — large static text/data service: `CardColorNameDictionary`, `CardColor_ColorDarkDictionary`/
`ColorLightDictionary`, `SelectColor_Blue/Orange/Green`, ~20 `*EffectDiscription`/`*EffectDescription` keyword-text
helpers, `ReplaceToASCII`, `IsXAntibodyString`, `DictionaryUtility` reverse-lookup helpers, plus large HTML-parsing
helper blocks feeding `OfficialCardListUtility`.

TO-BE: 195 lines, explicitly documented as "Minimal mirror … grown member-by-member as ports require (not ported
wholesale)". Verified this claim rather than taking the comment at face value: grepped every `DataBase.<Member>` /
`DictionaryUtility.<Member>` call site across the whole TO-BE tree — every member actually *referenced* anywhere
(`ReplaceToASCII`, `IsXAntibodyString`, `CardColorNameDictionary`, the ~17 `*EffectDiscription` helpers that are
called, `DictionaryUtility.GetCardColor`) is defined in this file; nothing dangles. The un-mirrored members
(`CardColor_ColorDarkDictionary`/`ColorLightDictionary`, `SelectColor_Blue/Orange/Green`, the HTML-parsing block)
are only consumed, in AS-IS, by `HandCard.cs`/`EditDeck.cs`/`OfficialCardListUtility.cs` — the three UI/tooling
files in this same batch that are themselves legitimately unported (#1, #2, #6 above). Partial port is
self-consistent and correctly scoped, not a mismatch.

## 6. Script/HandCard.cs — SCOPE EXCLUSION (legitimate), 0% ported

AS-IS: 1024-line `MonoBehaviour` — the hand-card visual prefab controller: `Image`/`Text`/`TextMeshProUGUI` field
wiring, sprite loading (`GetCardSprite`), cost/level/evo-cost icon layout math (`ShowPlayCost`/`ShowCostLevel`),
click/drag target registration, right-click card-detail popup. Read in full: every game-state read
(`cardSource.PayingCost(...)`, `cardSource.IsDigimon`, `cardSource.BaseEvoCostsFromEntity`, etc.) delegates to
`CardSource`, which is ported elsewhere — no rule logic is defined in this file itself.

TO-BE: 7-line skeleton stub, Category `UnityMixedLogic`, Priority HIGH.

Verdict: legitimate — pure Unity UI presentation component. Confirmed zero live TO-BE references to the `HandCard`
class (the only `HandCard` hits elsewhere are the unrelated `ChoiceType.HandCard` enum member and prose comments in
`SelectHandEffect.cs`/`ICardEffect.cs`). Unported gap noted (Priority HIGH in the stub's own metadata suggests this
is scoped for a later UI-facing pass, out of this engine-fidelity batch), not a fidelity mismatch.

## 7. Script/SelectAttackEffect.cs — MATCHES (1 doc-only finding)

AS-IS: 569 lines, `MonoBehaviourPunCallbacks` — attack-target selection flow (`SetUp`/setters/`CanTarget`/
`CanAttackDigimon`/`CanAttackPlayer`/`active`/`CanEndSelect`/`Activate`/`[PunRPC] SetAttackTarget`).

TO-BE: 414 lines. All AS-IS public/private members present with matching logic:
`SetUp`/`SetUpCustomMessage`/`SetCanNotSelectNotAttack`/`SetWithoutTap`/`SetIsVortex`/
`SetBeforeOnAttackCoroutine`/`SetAfterOnAttackCoroutine`/`CanTarget`/`CanAttackDigimon`/`CanAttackPlayer`/`active`/
`CanEndSelect`/`Activate` all reproduce the AS-IS conditional nesting verbatim. The AS-IS UI + Photon-RPC/
`WaitUntil` selection transport collapses into one `ChoiceProvider.ChooseAsync` call (candidates = every
`CanTarget` field permanent + a synthetic "attack the player" candidate when `CanAttackPlayer()`); confirmed this
`IChoiceProvider` abstraction is an established project-wide pattern (`PolicyChoiceProvider`,
`ScriptedChoiceProvider`, `DeferredChoiceProvider` all implement it), not invented ad hoc for this file. Attack
initiation (`attackProcess.Attack(...)`) is delegated to `AttackDeclarationCommons.Declare`/`DeclareAsync` with an
explicit, reasoned note (RD-W3-7) about why `_beforeOnAttackCoroutine` only takes the async path.

Finding (documentation only): several TO-BE comments cite AS-IS line numbers that don't exist in the current
569-line AS-IS file — e.g. `":1013-1017"` for the `attackProcess.Attack(...)` call (actually AS-IS line 543),
`":227-723"` and `":725-746"` for UI/cleanup regions, `":1019-1020"` for the after-attack coroutine (actually AS-IS
line ~545). The *content* described at each citation was independently verified against the real AS-IS source and
matches; only the cited line numbers are wrong (consistently offset by roughly +450-470, suggesting the comments
were written against a different/older line count and never refreshed). No functional impact — flagged for
awareness, not a matching defect.

Also verified: AS-IS's explicit `isYou` (human) vs. opponent/`IsAI` branch — including a weighted-random AI target
pick (AS-IS :418-458, `RandomUtility.IsSucceedProbability(0.5f)` when security ≥ 3) — is fully absent as separate
code in TO-BE, collapsed into the single `ChoiceProvider.ChooseAsync` call. This is the same uniform-provider
substrate pattern used by every other Select* file in the codebase (human/AI/scripted decision routing lives in the
`IChoiceProvider` implementation, not duplicated per selection site), not a novel simplification introduced here.

## 8. Script/MultipleSkills.cs — MATCHES (1 minor finding)

AS-IS: 437 lines, `MonoBehaviourPunCallbacks` — the triggered/cut-in "skill window" resolution loop
(`ActivateMultipleSkills`/`ActivateMultipleSkills_OnePlayer`, order-selection Blast-vs-normal branch logic,
register-before-body use-count bookkeeping, `RuleProcess`→terminal-check→recursive `TriggeredSkillProcess` tail).

TO-BE: 543 lines. Every AS-IS conditional and field-flag (`IsOnlyHandEffectStacked`/`IsOnlyOptionalEffectStacked`/
`IsEachStackedEffectHasDistinctSourceCard`, the digimon/tamer-effect-flag-setting block, the
`CanActivate`/`skipCondition`/`ChainActivations`/cut-in-eligibility filter chain, the AS-IS bounds-check quirk
`skillInfos_active.Count < _skillIndex` kept verbatim with a comment calling it out) is present and logically
identical, just restructured (`RunPhasesAsync`/`RunOnePlayerAsync`/`PassLoopAsync`/`ActivatePickAsync`/
`RunPickBodyAsync`/`PassTailAsync`) so an async suspend (agent choice mid-window) can resume from the exact AS-IS
cursor via `SkillWindowContinuation`. This extra structure is resumability plumbing around, not a departure from,
the AS-IS algorithm — confirmed each hoisted method's doc comment cites the exact AS-IS line range it replaces, and
those ranges check out against the real AS-IS file. Blast-branch vs. normal-branch order-selection UI (AS-IS
`SelectHandEffect` custom mode vs. `selectCardPanel`) collapses to one `ISkillWindowChoicePort.ChooseOrderAsync`
call — consistent with the same uniform-provider pattern noted in file #7.

Finding (cosmetic): AS-IS has five `Debug.Log(...)` calls inside the activation-eligibility loop (AS-IS lines 124,
132, 142, 150, 154 — "Can't Activate" / "has been skipped" / "has exceeded its use" / "is Cut In effect" / "has
been used"). TO-BE drops all five silently; unlike the file's other UI/Photon strips, which are each individually
called out in the header's "SUBSTRATE TRANSLATION" list, these five aren't mentioned anywhere. Diagnostic-only,
zero effect on game state or control flow — not a fidelity defect, just an uncommented strip.

## 9. Script/CEntity_EffectController.cs — MATCHES

AS-IS: 286 lines, `MonoBehaviour` — per-card-instance effect list (`GetCardEffects_ExceptAddedEffects`/
`GetCardEffects`, the nested security/field/player `IAddSkillEffect` scan), per-turn use-count bookkeeping
(`InitUseCountThisTurn`/`GetUseCountThisTurn`/`isOverMaxCountPerTurn`/`RegisterUseEffectThisTurn`/
`RemoveUseEffectThisTurn`), reflection-based `AddCardEffect(ID, ClassName)`, and the `EmptyEffectClass` fallback.

TO-BE: 763 lines. The seven AS-IS public methods plus `EmptyEffectClass` are ported verbatim — read the entire
`GetCardEffects` nested-scan (permanents-in-play / security / player-added `IAddSkillEffect` branches, the
`EffectTiming.None`-only "added by me" branch including its AS-IS dead line
`//GetCardEffects = ((IAddSkillEffect)cardEffect).GetCardEffect(...)`) side by side with AS-IS: identical branch
structure and identical dead code preserved. `AddCardEffect`'s reflection-based `gameObject.AddComponent(Type.
GetType(ClassName))` is deliberately not ported — the header explains `CardEffectDispatch` (relocated into this
same file from a separate `CardEffectCommons/CardEffectDispatch.cs`, per its own comment) already performs the
equivalent card-number → `CEntity_Effect`-subclass lookup; confirmed no orphaned duplicate `CardEffectDispatch.cs`
exists elsewhere (`grep` for the class name across the whole TO-BE tree returns only this file).

The remaining ~450 extra lines (`CEntity_EffectControllerStore`, `CEntityUseCycle`) are FOUNDATION substrate, not
AS-IS content, and are labeled as such in their own doc comments: AS-IS attaches one persistent `Component` per
card `GameObject` for the component's lifetime, but the headless `CardSource` is a stateless view reconstructed on
every access, so a per-(match, card-instance) store is needed just to make `UseEffectsThisTurn` actually accumulate
across calls the way the AS-IS Component's field naturally would — a structural necessity of the substrate, not
scope creep. `CEntityUseCycle`'s register-before-body/mutation-journal transaction exists to keep the `[Once Per
Turn]` cap check correct across the headless engine's replay-from-top resume model (vs. AS-IS's single, never-
re-entered coroutine) — reasoned and documented, not invented logic beyond what AS-IS's cap semantics require.

No AS-IS symbol missing, no AS-IS behavior altered; no mismatch found.

## Summary

| # | File | Verdict |
|---|------|---------|
| 1 | Script/EditDeck.cs | Legitimate scope exclusion, 0% ported (Unity deck-editor UI, no engine callers) |
| 2 | Script/OfficialCardListUtility.cs | Legitimate scope exclusion, 0% ported (build-time HTML scraper) |
| 3 | Script/CardEffectInterfaces.cs | Matches; 1 stale-comment finding (MISSING TYPES note outdated) |
| 4 | Script/CardEffectCommons/DNADigivolveEffects.cs | Matches, no findings |
| 5 | Script/DataBase.cs | Legitimate partial mirror, internally self-consistent (no dangling references) |
| 6 | Script/HandCard.cs | Legitimate scope exclusion, 0% ported (Unity hand-card UI, no engine callers) |
| 7 | Script/SelectAttackEffect.cs | Matches; 1 doc-only finding (AS-IS line-number citations off by ~450) |
| 8 | Script/MultipleSkills.cs | Matches; 1 cosmetic finding (5 Debug.Log calls dropped uncommented) |
| 9 | Script/CEntity_EffectController.cs | Matches, no findings |

No functional AS-IS↔TO-BE mismatches (renamed/relocated/lost symbols, altered logic) found in any of the 9 files.
All findings are either (a) legitimate, verifiably-zero-dependency scope exclusions of Unity-UI/build-tooling
files, or (b) documentation-only issues (stale comments, wrong line-number citations, uncommented diagnostic-log
strips) with no runtime behavioral effect.
