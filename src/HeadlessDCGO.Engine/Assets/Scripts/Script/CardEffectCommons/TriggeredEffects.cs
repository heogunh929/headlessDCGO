namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Aliased (not a namespace import) to avoid pulling the sibling `...Script.CardEffectFactory` namespace
// into scope, which would clash with the CardEffectFactory type below.
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;


// (③-A) StartOfMainAttackEffect DELETED — the invented IHeadlessCardEffect registry payload for
// CardEffectCommons.StartOfMainAttack (the retired EffectRegistry.Register producer seat at CardEffectCommons.cs
// ~:1507). It opened an OnStartMainPhase attack offer via EffectDrivenAttack.RequestChoice off a duration-tagged
// trigger binding — NOT the AS-IS shape (an inline ActivateClass mandatory SelectAttackEffect offer stored in
// Permanent.UntilOwnerTurnEndEffects). With the substrate method STOP-guarded (design item RD-3A-01) it had zero
// producers, so it is removed with its registry seat; port the AS-IS ActivateClass body 1:1 when a caller appears.


// (R3-C2b-2 fold) TriggeredMemoryEffect DELETED — the invented old-model "[When …] gain/lose N memory" effect
// (registry-lowering, its own scheduler resolution) is retired along with its sole factory
// CardEffectFactory.AddMemoryTriggerEffect. Every former caller (ST1_06/09, ST3_04/05, ST2_12, BT2_010/073,
// BT1_114, TfxOnDeleteGainMemory, TfxOnPlayGainMemory, and the BT1_090 EoT reversal) is now the AS-IS 1:1
// new-model inline ActivateClass memory recipe (card.Owner.AddMemory(N, activateClass) + the AS-IS Hashtable
// CanUse gate).


// (R3-F1b fold) TriggeredSetMemoryEffect DELETED — its sole factory (CardEffectFactory.SetMemoryTo3TamerEffect)
// is now the AS-IS 1:1 ActivateClass port (DCGO CardEffectFactory.cs:11). Zero remaining constructions in src or
// tests (G9-026 references the class name only in a stale comment, not a construction).


// (R3-C2b-2 fold) TriggeredGainMemoryEffect DELETED — the invented old-model "gain N memory" effect (the Tamer
// memory-gain family / the EoTLose3Memory backing) is retired. CardEffectFactory.EoTLose3Memory and the
// Gain1MemoryTamer* factories are now AS-IS 1:1 new-model ActivateClass ports (card.Owner.AddMemory(N,
// activateClass), owner-turn gate inline). Zero remaining constructions in src (tests retargeted in R3-C2b-2).


// (R6-Db D4 EXHAUSTED) The mirror-invented one-shot `PlaySelfAtEndOfBattleTriggerEffect` is DELETED together
// with its `PlaySelfAtEndOfBattleSecurityEffect` producer (ActivatedEffects.cs). It was an EffectRegistry
// OnEndBattle-trigger substitute for the AS-IS `Player.UntilEndBattleEffects` bucket + a DeleteAtTurnEnd metadata
// marker for the delete branch; the real AS-IS idiom (UntilEndBattleEffects.Add + PlayPermanentCards, and
// UntilOpponentTurnEndEffects.Add + DestroyPermanentsClass for the delete branch) is now landed directly in
// CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect. RD-P6C3-B2 RESOLVED.

