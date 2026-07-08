// Source: Assets/Scripts/CardEffect/BT3/Purple/BT3_080.cs
// 1:1 mirror of the original BT3_080 (BT3/Purple).
//   [On Deletion] Grant this Digimon <Retaliation> (self, inherited — the same shape as ST7_10's Piercing
//   grant, just on the OnDestroyedAnyone timing instead of OnDetermineDoSecurityCheck).
//   -> CardEffectFactory.RetaliationSelfEffect(isInheritedEffect: true, card, condition: null)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_080 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(CardEffectFactory.RetaliationSelfEffect(isInheritedEffect: true, card: card, condition: null));
        }

        return cardEffects;
    }
}
