// Source: Assets/Scripts/CardEffect/ST4/Green/ST4_01.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect.
//
// 1:1 mirror of the original ST4_01: inherited [All Turns] while it is the owner's turn and this card's
// permanent (own turn only, Lv6+, top card carries a printed level), DP +1000.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST4_01 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        HeadlessEntityId topId = card.PermanentOfThisCard().TopInstanceId;
                        if (CardEffectCommons.LevelOf(card, topId) >= 6)
                        {
                            if (CardEffectCommons.TopCardHasLevel(card, topId))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(changeValue: 1000, isInheritedEffect: true, card: card, condition: Condition));
        }

        return cardEffects;
    }
}
