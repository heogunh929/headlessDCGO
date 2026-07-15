// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [OnEndTurn] returns a
// self-static <Overclock> (CardEffectFactory.OverclockSelfEffect, trait "Puppet"), mirroring the printed
// Overclock region of real cards (e.g. BT22_036/BT22_040). Used by the C-EoT-2 witness to prove that the
// printed OnEndTurn <Overclock> resolves through the AS-IS OnEndTurn window (GetSkillInfos → MultipleSkills
// → OverclockProcess), NOT the retired EndOfTurnEffectAttack gate. No real card has the number "TfxOverclock",
// so this is inert in actual play. (Mirror of TfxVortex.)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxOverclock : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEndTurn)
        {
            cardEffects.Add(CardEffectFactory.OverclockSelfEffect(trait: "Puppet", isInheritedEffect: false, card: card, condition: null));
        }

        return cardEffects;
    }
}
