// TEST FIXTURE. "[Your Turn][Once Per Turn] when this unsuspends, draw 1" — TRIGGERED ACTIVATED at
// OnUnTappedAnyone (subject-scoped, MULTI-fire per turn). Exercises the v3 once-per-turn cap.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxUnsuspendDraw : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnUnTappedAnyone && CardEffectCommons.IsOwnerTurn(card))
        {
            effects.Add(CardEffectFactory.DrawCardsEffect(card, 1));
        }
        return effects;
    }
}
