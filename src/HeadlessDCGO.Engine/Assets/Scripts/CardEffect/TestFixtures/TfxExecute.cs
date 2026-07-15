// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [OnEndTurn] returns a self-static
// <Execute> (CardEffectFactory.ExecuteSelfEffect), mirroring the printed Execute region of real cards (e.g.
// BT23_069/BT24_081: `cardEffects.Add(CardEffectFactory.ExecuteSelfEffect(isInheritedEffect: false, card: card,
// condition: null))` at EffectTiming.OnEndTurn). Used by the A군 4단계 witness to prove that the printed
// OnEndTurn <Execute> resolves through the AS-IS OnEndTurn window (GetSkillInfos → MultipleSkills →
// ExecuteProcess), NOT the retired EndOfTurnEffectAttack gate. No real card has the number "TfxExecute", so this
// is inert in actual play. (Mirror of TfxVortex/TfxOverclock.)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxExecute : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEndTurn)
        {
            cardEffects.Add(CardEffectFactory.ExecuteSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        return cardEffects;
    }
}
