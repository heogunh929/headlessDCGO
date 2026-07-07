// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_068.cs
// Decision: PORT
// Category: CardEffect
//
// 1:1 mirror of the original BT1_068: inherited [All Turns] while it is the owner's turn, this card's
// permanent (top of its own stack) is Lv6+ and its top card carries a printed level, Security Attack +1.
// -> ChangeSelfSAttackStaticEffect (inherited, conditional). Same shape as ST4_01 (DP +1000 analog).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_068 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
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

            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(
                changeValue: 1,
                isInheritedEffect: true,
                card: card,
                condition: Condition));
        }

        return cardEffects;
    }
}
