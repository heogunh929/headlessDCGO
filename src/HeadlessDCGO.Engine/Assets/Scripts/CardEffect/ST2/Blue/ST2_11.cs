// 1:1 mirror of the original ST2_11 (ST2/Blue).
//   [When Attacking][Once Per Turn] Unsuspend this Digimon.  -> UnsuspendSelfTriggerEffect (OnAllyAttack)
// The original wraps an inline ActivateClass + IUnsuspendPermanents with activatedOrder=1 (once per turn)
// and SetHashString("Unsuspend_ST2_11"); the headless maps these to maxCountPerTurn=1 + hash, enforced by
// the live trigger loop via OnceFlagController (G10-001).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST2_11 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.UnsuspendSelfTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                card: card,
                description: "[When Attacking][Once Per Turn] Unsuspend this Digimon.",
                maxCountPerTurn: 1,
                hash: "Unsuspend_ST2_11"));
        }

        return cardEffects;
    }
}
