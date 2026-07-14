// P1-1 (C2r) witness — the deletion-window cause predicates IsByBattle / IsByEffect return the AS-IS truth table
// for the decision-4 transport payload (which has no live IBattle/ICardEffect object at the sink). Before the fix
// the mirror IsByBattle/IsByEffect read ONLY the AS-IS "battle"/"CardEffect" hashtable keys, which the transport
// left null — so a ported deletion reactor keying on IsByBattle/IsByEffect got a CONSTANT wrong answer
// (IsByBattle->false, IsByEffect->false) for battle AND effect deletions. The transport now carries a DERIVED
// boolean cause (byBattle from the loser's MarkDeletedByBattle flag, byEffect from the sink mutation's non-DPZero
// cause-id presence), and the predicates read it as a fallback with the AS-IS truth table:
//   battle deletion  -> IsByBattle=true,  IsByEffect=false, IsDPZeroDelete=false
//   effect deletion  -> IsByBattle=false, IsByEffect=true,  IsDPZeroDelete=false
//   DP-zero sweep    -> IsByBattle=false, IsByEffect=false, IsDPZeroDelete=true   (AS-IS DPZero-only hashtable)
// The AS-IS live-object path (a real ICardEffect/IBattle in the payload) is unchanged and still wins; the markers
// are consulted only when the object is absent (the transport path).

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using Commons = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;

var failures = new List<string>();
void Check(bool ok, string label)
{
    if (!ok) { failures.Add(label); Console.Error.WriteLine($"FAIL {label}"); }
    else { Console.WriteLine($"PASS {label}"); }
}

// A synthetic deletion reactor reads the window payload via the shared predicates — exactly what a ported
// [When deleted in battle] / [When deleted by effect] / DP-zero-aware ActivateClass CanActivate does.
bool ReactorSeesByBattle(Hashtable ht) => Commons.IsByBattle(ht);
bool ReactorSeesByEffect(Hashtable ht) => Commons.IsByEffect(ht, cardEffectCondition: null!);
bool ReactorSeesDpZero(Hashtable ht) => Commons.IsDPZeroDelete(ht);

var noPerms = new List<Permanent>();   // the top-level cause keys are what these predicates read

// --- 1. BATTLE deletion (BattleResolver transport: byBattleCause=true) ---
{
    Hashtable ht = Commons.OnDeletionHashtable(noPerms, byEffectCause: false, byBattleCause: true, isDPZero: false);
    Check(ReactorSeesByBattle(ht), "battle deletion: IsByBattle = true");
    Check(!ReactorSeesByEffect(ht), "battle deletion: IsByEffect = false");
    Check(!ReactorSeesDpZero(ht), "battle deletion: IsDPZeroDelete = false");
}

// --- 2. EFFECT deletion (sink / deferred transport: byEffectCause=true) ---
{
    Hashtable ht = Commons.OnDeletionHashtable(noPerms, byEffectCause: true, byBattleCause: false, isDPZero: false);
    Check(!ReactorSeesByBattle(ht), "effect deletion: IsByBattle = false");
    Check(ReactorSeesByEffect(ht), "effect deletion: IsByEffect = true (null condition — deleted by ANY effect)");
    Check(!ReactorSeesDpZero(ht), "effect deletion: IsDPZeroDelete = false");
}

// --- 3. DP-ZERO sweep (GameFlowProcessor DP<=0: byEffect/byBattle both false, isDPZero=true) — AS-IS's DPZero-only
//        DestroyPermanentsClass hashtable: NEITHER cause, only the DPZero flag. ---
{
    Hashtable ht = Commons.OnDeletionHashtable(noPerms, byEffectCause: false, byBattleCause: false, isDPZero: true);
    Check(!ReactorSeesByBattle(ht), "DP-zero sweep: IsByBattle = false");
    Check(!ReactorSeesByEffect(ht), "DP-zero sweep: IsByEffect = false");
    Check(ReactorSeesDpZero(ht), "DP-zero sweep: IsDPZeroDelete = true");
}

// --- 4. Regression guard: a payload with NO cause marker (neither present) reports both predicates false — the
//        markers are strictly additive (absence never spuriously flips a predicate true). ---
{
    Hashtable ht = Commons.OnDeletionHashtable(noPerms, byEffectCause: false, byBattleCause: false, isDPZero: false);
    Check(!ReactorSeesByBattle(ht) && !ReactorSeesByEffect(ht) && !ReactorSeesDpZero(ht),
        "no-cause payload: all three predicates false (markers are additive)");
}

// --- 5. AS-IS faithful path unchanged: the (List<Permanent>, ICardEffect, IBattle, bool) builder with BOTH cause
//        objects null (the pre-transport call shape) still reports both predicates false — the marker fallback did
//        not alter the object-reading path. ---
{
    Hashtable ht = Commons.OnDeletionHashtable(noPerms, cardEffect: null!, battle: null!, isDPZero: false);
    Check(!ReactorSeesByBattle(ht) && !ReactorSeesByEffect(ht),
        "faithful builder, null objects: IsByBattle/IsByEffect both false (unchanged AS-IS behaviour)");
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"\n{failures.Count} FAILURE(S)");
    return 1;
}
Console.WriteLine("\nALL PASS");
return 0;
