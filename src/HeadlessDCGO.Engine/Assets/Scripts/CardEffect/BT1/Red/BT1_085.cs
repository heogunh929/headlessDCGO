// Source: Assets/Scripts/CardEffect/BT1/Red/BT1_085.cs
// Decision: PORT
// Category: CardEffect
// Priority: HIGH
// Migration: Ported per-card effect source
//
// 1:1 mirror of the original BT1_085 (Red Tamer):
//   [Start of Your Turn] Set your memory to 3 (if 2 or less).      -> SetMemoryTo3TamerEffect
//   [Your Turn] Your Red Digimon with 4 or more digivolution cards
//   get +1 security attacks.                                        -> ChangeSAttackStaticEffect
//   [Security] Play this Tamer.                                     -> PlaySelfTamerSecurityEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_085 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
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

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent.TopCard.HasCardColor("Red"))
                    {
                        if (permanent.DigivolutionCards.Count >= 4)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSAttackStaticEffect(
                permanentCondition: PermanentCondition,
                changeValue: 1,
                isInheritedEffect: false,
                card: card,
                condition: Condition));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
