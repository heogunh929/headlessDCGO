// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_097.cs
// 1:1 headless mirror (Option).
//   [Main] Trigger <Draw 1>.     -> DrawCardsEffect (OptionSkill, 1)
//   [Security] Trigger <Draw 2>. -> DrawCardsEffect (SecuritySkill, 2)
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_097 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(CardEffectFactory.DrawCardsEffect(card, 1));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.DrawCardsEffect(card, 2));
        }

        return cardEffects;
    }
}
