// Source: Assets/Scripts/Script/MultipleSkills.cs
// Decision: PORT
// Category: CardEffect
// Migration: AS-IS mirror (R3-B) — the re-entrant trigger WINDOW loop.
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
//
// ============================================================================================================
// (R3-B) STOP — design item RD-R3-01 (window-loop rehousing BLOCKED on the trigger effect-model fold).
//
// AS-IS MultipleSkills.ActivateMultipleSkills / ActivateMultipleSkills_OnePlayer (MultipleSkills.cs:21-423) is
// the collect -> turn/non-turn split -> while(true) re-evaluate -> order-select -> optional-confirm -> resolve
// -> RuleProcess -> TriggeredSkillProcess-cut-in window loop. Its live seat today is the headless mechanism:
//   Headless/Effects/WindowResolver.cs (frame-stack continuation) driven by
//   Headless/Effects/WindowResolverWiring.CollectUnifiedSeed + BuildLiveMainLoopDeps, from
//   Headless/Runtime/GameFlowProcessor.AutoProcessAsync (the AS-IS AutoProcessCheck -> TriggeredSkillProcess drive).
//
// The RULE-ORDER + SUSPEND/RESUME quirks of that loop ARE 1:1 AS-IS already (turn-player-first RD-15, per-pass
// CanActivate re-evaluation P1-1, optional yes/no RD-13, commit-before-body F5/RD-12, HasExecutedSameEffect
// cut-in dedup A-1, RuleProcess-between-picks MultipleSkills.cs:398-403, cut-in recursion RD-17). The
// continuation/frame-stack + IWindowChoicePort ARE the ADAPTATION the task permits (async choice-wait replacing
// the coroutine that blocks inside SelectCardEffect).
//
// WHY IT CANNOT BE REHOUSED INTO THIS FILE READING LIVE (task item 3 -- "no EffectRegistry read, live
// re-enumerate"): the AS-IS window COLLECTS via AutoProcessing.GetSkillInfos, a LIVE re-enumeration of ONE
// uniform ActivateICardEffect model (player/field/trash/hand/faceup-security). The headless window instead
// collects via AutoProcessingTriggerCollector, which reads the EffectRegistry BOUND-MUTATION-REACTOR bindings
// (IHeadlessCardEffect) -- a PARALLEL effect model that has NO AS-IS equivalent and NO GetSkillInfos home --
// plus CollectActivatedBridgeTriggers (the ActivateICardEffect half). Routing this loop through the mirror
// GetSkillInfos (which returns only ActivateICardEffect) would SILENTLY DROP every bound reactor. Making the
// migrated path live-re-enumerate therefore REQUIRES first folding the bound-mutation-reactor model into
// ActivateICardEffect (the trigger effect-model fold). That fold is NOT R3-B scope -- it is the effect-model
// rebuild / registry retirement slated for R3-C. Building a mirror MultipleSkills loop here NOW would either be
// a fake shell over the still-live WindowResolver mechanism (forbidden -- memory
// result-equivalence-not-completion / no-simplification) or an invented parallel bridge (union, forbidden by
// bigbang_redesign section 0.4). STOP per the task's "cannot re-derive -> STOP + RD-R3-NN (no simplification)".
//
// RE-OPEN CONDITION: after R3-C retires EffectRegistry/EffectBinding and the bound-mutation reactors resolve as
// ActivateICardEffect through GetSkillInfos, rehouse WindowResolver's loop body here as
// ActivateMultipleSkills_OnePlayer (StackedSkillInfos = the frame stack, ActivateEffectProcess = the resolve,
// AutoProcessing.RuleProcess = the between-picks sweep, TriggeredSkillProcess = the cut-in recursion) with the
// continuation/ChoicePort retained as substrate ADAPTATION.
// ============================================================================================================
