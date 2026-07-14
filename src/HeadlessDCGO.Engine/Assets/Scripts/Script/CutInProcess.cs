// Source: Assets/Scripts/Script/CutInProcess.cs
// Decision: PORT
// Category: UnityMixedLogic
// Migration: AS-IS mirror (R3-B).
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
//
// ============================================================================================================
// (R3-B) STOP — design item RD-R3-02 (CutInProcess.CutInProcessCoroutine is DEAD in AS-IS + blocked on the
// DestroyPermanentsClass mirror).
//
// AS-IS CutInProcess.cs holds exactly ONE method, CutInProcessCoroutine (CutInProcess.cs:12-109): a rule-sweep
// loop (end-game -> trash DP<=0 Digimon in breeding -> DestroyPermanentsClass on DP==0 Digimon) that is a
// near-duplicate of AutoProcessing.RuleProcess's DP stages. TWO findings make a faithful mirror inappropriate now:
//
//  1. DEAD CODE: `grep -rn CutInProcessCoroutine DCGO/Assets/Scripts --binary-files=text` finds only the
//     definition — ZERO callers in AS-IS. (AS-IS GManager.autoProcessing_CutIn, GManager.cs:112, is typed
//     `AutoProcessing`, NOT `CutInProcess` — the second AutoProcessing instance for the pay-cost cut-in stack,
//     mirrored by AutoProcessing.ForCutIn. It is unrelated to this file.) Porting a dead coroutine has zero
//     behavioural value.
//
//  2. BLOCKED PRIMITIVE: the DP==0 branch resolves through `new DestroyPermanentsClass(...).Destroy()`. No
//     DestroyPermanentsClass mirror exists (design item R2-P2-4 — the DP-zero destroy is carried by the
//     VERIFIED batch-semantics helper GameFlowProcessor.StateBasedDeletionSweepAsync, which AutoProcessing
//     .DigimonLackDPProcess delegates to). A faithful CutInProcessCoroutine mirror would have to invoke a
//     non-existent DestroyPermanentsClass or re-route to the sweep — the latter being exactly the
//     "different-mechanism substitution" the no-simplification rule forbids for a 1:1 mirror.
//
// STOP per "cannot re-derive -> STOP + RD-R3-NN (no simplification)". RE-OPEN CONDITION: port only once the
// DestroyPermanentsClass mirror lands (R2-P2-4) AND a live caller for the cut-in rule-sweep exists; then mirror
// CutInProcessCoroutine 1:1 on the R1/R2 getters (Permanent.DP/IsDigimon, Permanent.DiscardEvoRoots,
// CardObjectController.RemoveField/AddTrashCard, DestroyPermanentsClass.Destroy).
// ============================================================================================================
