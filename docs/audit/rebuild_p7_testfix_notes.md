# Rebuild P7 — test-code API migration notes (2026-07-14)

Scope: the effect-model rebuild made the ENGINE (`src/HeadlessDCGO.Engine`) build green as an
AS-IS 1:1 mirror, but ~80 behavioral smoke-test harnesses under `tests/*.Tests/` (each a
hand-written `Program.cs`, no test framework) still called the OLD pre-migration API and failed to
BUILD. This pass adapted ONLY the test code (`tests/<proj>/Program.cs`) to the new surface —
engine files were NOT changed by this pass. Behavioral intent preserved: no assertion was weakened,
deleted, or commented out. All 80 previously-build-failing projects now build with 0 `error CS...`.

## API-migration patterns applied (build fixes)

1. **Factory gained a required `effectName` (or other) parameter (CS7036 / CS8323).** Many
   `CardEffectFactory.*StaticEffect(...)` methods and kind-class `SetUp*` methods took new
   required params during the rebuild (`effectName`, sometimes `condition`, `permanentCondition`,
   `isLinkedEffect`, reordered `isInheritedEffect`). Fix: pass a faithful argument (a descriptive
   string literal for `effectName` — never asserted on; `null` for an absent `condition`, matching
   the AS-IS `condition == null || condition()` convention) and convert mixed named/positional
   calls to the current signature order.

2. **`ICardEffect.Owner` → `EffectSourceCard?.Owner` (CS1061).** The abstract `ICardEffect` carries
   `EffectSourceCard` (a `CardSource`); `.Owner` lives on `CardSource`. Adapted `cardEffect.Owner`
   call sites (e.g. `src => src.EffectSourceCard.Owner != P1`).

3. **`PartitionCondition` / collection-arity drift (CS1503).** `PartitionSelfEffect` etc. now take a
   `List<PartitionCondition>` where tests passed an array/params — wrapped in a `List<...>`.

4. **`CardColor` is now an enum, not a string (CS1503/CS0029/CS1593/CS1662).** Colour literals and
   `List<string>` colour lists updated to `CardColor.X` / `List<CardColor>`; zero-arg lambdas
   passed to `Func<Hashtable,bool>` given the required parameter.

5. **Keyword marker namespace/type moves (CS0234 / CS0117).** `CardEffectFactory.KeyWordEffects.*`
   nested-type references and constants (e.g. `AddLinkConditionClass.GetLinkConditionKey`) that were
   retired/moved were re-pointed to their current home or replaced with the live idiom.

6. **`LegacyBindingBridge.TryToBinding` for interface-typed effects (CS1061 `ToBinding`).** Where a
   factory returns a concrete OLD-model effect through an `ICardEffect`-typed signature (the concrete
   class *does* still have `ToBinding`), the reflective helper
   `LegacyBindingBridge.TryToBinding(effect, id, out binding)` (or a concrete cast such as
   `((ContinuousImmunityEffect)factory(...)).ToBinding(id)`) restores the byte-identical
   registration path with no behavior change. This is the preferred, non-lossy fix.

7. **`CEntity_Effect.CardEffects` override return type (CS0508).** Test-local probe subclasses that
   override `CEntity_Effect.CardEffects(EffectTiming, CardSource)` corrected their return type from
   `IReadOnlyList<ICardEffect>` to `List<ICardEffect>` to match the base. These use the correct live
   live-scan idiom and pass once the signature matches.

8. **`Func` arity drift (CS1503).** e.g. `PRIM.JogressLevels` `getLevels` adapted from
   `Func<CardSource, IReadOnlyList<int>>` to `Func<CardSource, Permanent, List<int>>`.

9. **Broken new-model factory stubs re-pointed to old-model classes (CS0030).** `CardEffectFactory.LinkEffect`
   / `ArtsDigivolveEffect` were re-pointed by the rebuild to not-yet-working new-model kind-classes
   (`ActivateClass`/`OptionResolutionClass`); tests that need the still-functional behavior construct
   the old-model `LinkSelfEffect` / `ArtsDigivolveSelfEffect` directly (engine design items
   RD-P6C2-7 / RD-P6C2-10).

## MIGRATION-NOTE entries (build-preserving; assertion UNCHANGED and expected to fail until stage B)

These are the sanctioned exception: a "new-model" kind-class (namespace
`...Script.CardEffects`, e.g. `TreatAsDigimonClass`, `CollisionClass`, `CanNotSwitchAttackTargetClass`,
`AddLinkConditionClass`, `AddAppFusionConditionClass`, `AddDigivolutionRequirementClass`,
`ChangeCardLevelClass`, `ChangeDPClass`, `ImmuneFromDPMinusClass`, `CanNotBeDestroyedBySkillClass`,
the keyword `ActivateClass` shapes, …) has **no `ToBinding`/EffectRegistry bridge yet** (documented
stage-B RED, `docs/audit/rebuild_p6_stageA_notes.md`: "NEW-model kind-class effects register
NOTHING … documented RED until stage B"). The gate each such test checks reads only the substrate
`EffectRegistry` (or, for a second distinct family, scans the AS-IS live `CardSource.EffectList` →
`CEntity_Effect.GetCardEffects`, which `CardEffectDispatch` populates only for real ported cards by
reflecting over the engine assembly — no test-facing injection hook). So from test code alone there
is currently **no buildable way** to make these specific grants observable.

For each, the fix was: keep the factory call (still exercises the factory), remove ONLY the
now-uncompilable `.ToBinding(...)`/`EffectRegistry.Register(...)` wrapper, add an inline
`// MIGRATION-NOTE (P7 test-fix): …` explaining the stage-B gap, and leave the assertion byte-identical.
The assertion is EXPECTED TO FAIL at runtime until the stage-B live interface-scan bridge lands — it
is tracked here, not silently weakened.

Two distinct root-cause families are called out in the notes:
- **(A) EffectRegistry-only gates** — the gate reads `EffectRegistry` and the kind-class registers no
  binding (e.g. keyword `ContinuousKeywordGate.HasKeyword`, restriction/deletion gates, DP/SA folds).
- **(B) live-scan-only gates** — the gate scans `CardSource.EffectList`/`cEntity_Effect` (card-level
  props: `CardSource.Level`/`BaseCardColors`/`CardColors`/traits,
  `LinkConditionOf`/`AppFusionConditionOf`/`AssemblyConditionOf`, `AttackTargetSwitchGate.IsLocked`,
  added-digivolution-requirement scan). Even a future `ToBinding` bridge would not help these — they
  need a test-facing hook to inject a synthetic `ICardEffect` into a fixture card's effect list.

Projects carrying at least one MIGRATION-NOTE (46):

C1-DecodePartitionPre, FAILa-01.CanNotBeDestroyedBySkillCause, FAILa-02.ImmuneFromDPMinusCause,
FAILa-04.CannotReturnToDeckCause, FAILa-05.CannotReduceCostScope, FAILb-01.InvertSAttack,
FAILb-verify.KeywordGrants, FAILd-02.CanNotBeRemoved, G9-002.SelfStaticKeywordFactory,
G9-003.PlayCostFactory, G9-021.ChangeDigivolutionCost, G9-022.CanNotDigivolve,
G9-023.CanNotDigivolveScoped, G9-024.AddDigivolutionRequirement, G9-025.KeywordBatch2W2,
G9-027.W2Reuse, G9-028.BlockerStatic, G9-032.KeywordW3, G9-033.W3StaticNonSelf,
G9-035.W3Restrictions, G9-036.W3Reuse, G9-037.W3LinkColor, G9-038.W4Batch1a, G9-039.W4Batch1b,
G9-040.W4Batch1d, G9-041.W4Specials, G9-043.ViewLayer, G9-044.AddSelfDigivolveReq,
G9-050.PlayerScopePredicate, G9-052.M1PerCardPredicate, G9-053.M2CausingEffect,
G9-054.BattleImmunityScope, G9-055.M4DecoyUnseal, G9-056.M4LinkUnseal, G9-058.S3DeletionReplUnseal,
G9-060.K5MindLink, G9-061.A3ViewLayerFolds, G9-062.P1LeavePlayCleanup, G9-063.C7DualC9Linked,
G9-064.SwitchTargetLock, G9-065.Assembly, G9-070.W6LinkCondition, G9-071.W6AppFusion,
GR-006.EndOfTurnEffectAttack, PRIM-P0.AddSkillLiveSet, PRIM.CannotBeBlockedDefender.

(The exact class + gate for each is in the inline `MIGRATION-NOTE` comment at the call site;
`grep -rn "MIGRATION-NOTE" tests` enumerates all 111 annotated sites.)

## Stage B landed DURING this pass (important context for the inline "until stage B lands" notes)

While this test-fix pass ran, the coordinator committed the engine's stage-B continuous-effect flip
(`d527f052`: "P6 flip stage B — 74-interface is-scan UNION old-binding-gate + new-model scan +
RegisterOnEnterPlay attaches cEntity_Effect"). So the engine-side live is-scan the inline
`// MIGRATION-NOTE` comments anticipate ("until stage B lands") now **exists and is committed**.

That does NOT retroactively weaken these notes: the MIGRATION-NOTE tests fail for the **family-(B)**
reason, which stage B does not remove — they build a *synthetic* fixture card (a bare
`CardInstanceRecord`/definition id) and call a factory, but never attach the resulting effect to that
card's `CEntity_Effect`. Stage B's is-scan reads `CardSource.EffectList` →
`CEntity_Effect.GetCardEffects`, populated only by `CardEffectDispatch` reflecting over the ENGINE
assembly for a REAL ported card; there is no test-facing hook to inject an ad-hoc `ICardEffect` into a
synthetic card's effect list. The coordinator's own stage-B validation (G6-001 / G8-002 / GR-005)
correctly uses a real ported card (ST7_10) placed via `RegisterOnEnterPlay`, which is why those pass.

**Remaining engine gap for these tests (successor to "stage B"): a test-facing effect-injection seam**
— a supported way for a harness to attach a constructed new-model `ICardEffect` to a synthetic card's
`CEntity_Effect` so the live is-scan observes it. Until that exists, the MIGRATION-NOTE assertions
remain UNCHANGED and expected-to-fail — tracked here and inline, never silently weakened. (The literal
inline shorthand "until stage B lands" should be read as "until the new-model grant is observable
end-to-end from test code", i.e. this injection seam; the engine half, stage B, is already in.)

## Engine files: NOT modified by this pass

This pass changed only `tests/*/Program.cs` (+ this notes file). The stage-B engine changes present in
the worktree (`NewModelContinuousScan.cs`, the `Continuous{Keyword,Modifier,Dp}Gate` unions,
`CardEffectRegistrar` cEntity_Effect attach, `TfxKeywords`) are the **coordinator's** committed work
(`d527f052`), not this pass's — `git status src/` is clean against that HEAD. (One test-fix batch agent
independently prototyped an equivalent scan mid-run; that prototype was reverted so the coordinator's
committed version is the sole authority.)

## Not fixed / could-not-build

None. All 80 previously-build-failing projects build at 0 `error CS...`.
