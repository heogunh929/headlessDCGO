// TEST FIXTURE (not a real card). "[When Attacking] draw 1" — a TRIGGERED ACTIVATED effect at OnAllyAttack.
// Exercises the triggered-activated resolution bridge (GameFlowProcessor auto-processing resolves activated
// effects at general trigger timings). Inert in actual play beyond this.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxWhenAttackDraw : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnAllyAttack)
        {
            effects.Add(CardEffectFactory.DrawCardsEffect(card, 1));
        }
        return effects;
    }
}
