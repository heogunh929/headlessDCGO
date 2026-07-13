// TEST FIXTURE (not a real card). Returns a player-scope "your Digimon get +2000 DP" continuous static at
// EffectTiming.None, used by tests/SEC-FaceUpSecuritySource to prove a FACE-UP security card is scanned as a
// continuous-effect SOURCE for the owner's field Digimon (AS-IS `player.SecurityCards where !IsFlipped`
// population, uniform across getters). No real card has the number "TfxSecurityDpBuff", so this is inert in play.
//
// (P7 stage-B fix) The permanentCondition was `null`, but AS-IS ChangeDP.cs:128 treats a null permanentCondition
// as "apply to ANY battle-area Digimon" (`permanentCondition == null || permanentCondition(permanent)`) — of
// BOTH players, exactly like the field-permanent scan (Permanent.DP has no owner filter on the effect source).
// So the fixture's "your Digimon" intent (the test asserts the P1-scoped buff does NOT reach the opponent) must
// be spelled with an explicit owner predicate; `null` would AS-IS-correctly buff the opponent too. Owner-scoped
// to the security card's controller (`card.Owner`) to match a real "your Digimon get +DP" card.

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
                permanentCondition: permanent => permanent.OwnerId == card.Owner,
                changeValue: 2000, isInheritedEffect: false, card, condition: null, effectName: null));
        }

        return effects;
    }
}
