// TEST FIXTURE (not a real card). Returns a player-scope "your Digimon get +2000 DP" continuous static at
// EffectTiming.None, used by tests/SEC-FaceUpSecuritySource to prove a FACE-UP security card is scanned as a
// continuous-effect SOURCE for the owner's field Digimon (AS-IS `player.SecurityCards where !IsFlipped`
// population, uniform across getters). No real card has the number "TfxSecurityDpBuff", so this is inert in play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxSecurityDpBuff : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            effects.Add(CardEffectFactory.ChangeDPStaticEffect(
                permanentCondition: null, changeValue: 2000, isInheritedEffect: false, card, condition: null, effectName: null));
        }

        return effects;
    }
}
