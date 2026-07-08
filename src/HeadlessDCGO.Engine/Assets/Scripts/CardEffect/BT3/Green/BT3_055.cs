// Source: Assets/Scripts/CardEffect/BT3/Green/BT3_055.cs — a Digimon.
// 1:1 mirror of the original BT3_055:
//   <Security Attack +1> (This Digimon checks 1 additional security card.)
//   <Jamming> (When attacking, this Digimon's opponent cannot activate <Blocker>.)
//   AS-IS: CardEffectFactory.PierceSelfEffect(isInheritedEffect:false, card, condition:null) on
//   EffectTiming.OnDetermineDoSecurityCheck, and CardEffectFactory.JammingSelfStaticEffect
//   (isInheritedEffect:false, card, condition:null) on EffectTiming.None — both verbatim factory matches.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_055 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.JammingSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        return cardEffects;
    }
}
