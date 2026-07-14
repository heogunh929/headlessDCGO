// Source: Assets/Scripts/CardEffect/BT2/BT2_057.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;

public sealed class BT2_057 : CEntity_Effect
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
                        if (new Permanent(card.Context, card.InstanceId).HasReboot)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.JammingSelfStaticEffect(
                isInheritedEffect: true,
                card: card,
                condition: Condition));
        }

        return cardEffects;
    }
}