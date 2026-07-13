// Source: DCGO/Assets/Scripts/Script/CardController.cs (public class IBattle, :4427-4773)
// (EFFECT-MODEL REBUILD / P2 "Hashtable layer") 1:1 mirror of the AS-IS `IBattle` helper TYPE — the per-battle
// context object the Hashtable builders/accessors (HashtableSetting.cs / GetFromHashtable.cs) construct, stash
// under the "battle" key and read back (enemyPermanent / CompareStats / .hashtable). Ported members: the ctor,
// the AttackingPermanent/DefendingPermanent/DefendingCard/IsWithoutAttack state, the `hashtable` payload, and the
// two pure helpers `enemyPermanent()` and `CompareStats()`.
//
// SCOPE NOTE — `IEnumerator Battle()` (AS-IS :4474-4772) is NOT re-ported here. Its combat-pipeline logic
// (StartBattle/EndBattle StackSkillInfos, DestroyPermanentsClass.Destroy, OnDetermineDoSecurityCheck, per-turn
// UntilEndBattleEffects reset) is already migrated into the mirror substrate at
// Headless/Runtime/BattleResolver.cs + SecurityResolver.cs (see those files' `IBattle.Battle` anchors) and
// driven by the mirror AttackProcess.cs. Re-housing the coroutine in this type would duplicate that migrated
// pipeline. IBattle is listed as a remaining CardController.cs migration slice (CardController.cs:19); when that
// slice lands, Battle() rehouses here. Recorded as design item RD-P2-01 in docs/audit/rebuild_p2_hashtable_missing.md.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;

/// <summary>AS-IS <c>IBattle</c> (CardController.cs:4427) — the per-battle context holder.</summary>
public class IBattle
{
    public IBattle(Permanent AttackingPermanent, Permanent DefendingPermanent, CardSource DefendingCard, bool IsWithoutAttack = false)
    {
        this.AttackingPermanent = AttackingPermanent;
        this.DefendingPermanent = DefendingPermanent;
        this.DefendingCard = DefendingCard;
        this.IsWithoutAttack = IsWithoutAttack;
    }

    public Permanent AttackingPermanent { get; private set; } = null;
    public Permanent DefendingPermanent { get; private set; } = null;
    CardSource DefendingCard { get; set; } = null;
    bool IsWithoutAttack { get; set; } = false;
    public Hashtable hashtable { get; set; } = new Hashtable();

    public Permanent enemyPermanent(Permanent permanent)
    {
        if (permanent != null)
        {
            if (permanent == AttackingPermanent)
            {
                return DefendingPermanent;
            }
            else if (permanent == DefendingPermanent)
            {
                return AttackingPermanent;
            }
        }

        return null;
    }

    public int CompareStats()
    {
        int statCheck = 0;

        if (AttackingPermanent.HasIceclad || DefendingPermanent.HasIceclad)
            statCheck = AttackingPermanent.DigivolutionCards.Count - DefendingPermanent.DigivolutionCards.Count;
        else
            statCheck = AttackingPermanent.DP - DefendingPermanent.DP;

        // ADAPTATION: AS-IS `Mathf.Clamp(int, -1, 1)` (UnityEngine) — System.Math.Clamp is the established
        // mirror substitute (identical int semantics; Player.cs MaxMemoryCost precedent).
        statCheck = Math.Clamp(statCheck, -1, 1);

        return statCheck;
    }
}
