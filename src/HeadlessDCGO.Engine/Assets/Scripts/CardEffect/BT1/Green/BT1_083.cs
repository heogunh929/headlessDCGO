// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_083.cs
// 1:1 mirror of the original BT1_083 (BT1/Green).
//   [Piercing] (self, static)
//   -> OnDetermineDoSecurityCheck: CardEffectFactory.PierceSelfEffect (same shape as BT1_081 / BT1_022 /
//      BT1_026 / ST4_13 / ST7_10 — this timing is a query-time keyword, not a trigger emission).
//   [Your Turn] DP+4000 (self, static)
//   -> None: CardEffectFactory.ChangeSelfDPStaticEffect (same shape as BT1_008, minus the extra
//      opponent-suspended-count clause; condition here is just IsExistOnBattleArea && IsOwnerTurn).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_083 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        return true;
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(
                changeValue: 4000,
                isInheritedEffect: false,
                card: card,
                condition: Condition));
        }

        return cardEffects;
    }
}
