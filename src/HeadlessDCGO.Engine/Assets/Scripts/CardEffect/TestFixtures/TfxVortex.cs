// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [OnEndTurn] returns a
// self-static <Vortex> (CardEffectFactory.VortexSelfEffect), mirroring EX8_074's "Vortex" region. Used by
// tests/G9-008 to prove that EX8-3 (OnEndTurn added to CardEffectRegistrar.AllTimings) registers the
// keyword at enter-play so GR-006's EndOfTurnEffectAttack sees it live. No real card has the number
// "TfxVortex", so this is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxVortex : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEndTurn)
        {
            cardEffects.Add(CardEffectFactory.VortexSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        return cardEffects;
    }
}
