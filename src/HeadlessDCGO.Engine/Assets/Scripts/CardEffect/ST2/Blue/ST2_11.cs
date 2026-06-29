// 1:1 mirror of the original ST2_11 (ST2/Blue).
//   [When Attacking][Once Per Turn] Unsuspend this Digimon.  -> UnsuspendSelfTriggerEffect (OnAllyAttack)
// The original wraps an inline ActivateClass + IUnsuspendPermanents; the headless emits an Unsuspend
// mutation on self when the OnAllyAttack trigger resolves. The [Once Per Turn] gate maps to the once-flag
// subsystem (not yet enforced here — a 1:1 relaxation; see docs/audit/card_porting_recipe.md §5).

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
                description: "[When Attacking][Once Per Turn] Unsuspend this Digimon."));
        }

        return cardEffects;
    }
}
