// Source: Assets/Scripts/CardEffect/ST1/Red/ST1_12.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1, ST1 wave 2). Tamer.
//
// 1:1 mirror of the original ST1_12:
//   [All Turns] [Your Turn] Your Digimon get +1000 DP   -> ChangeDPStaticEffect (player-scope, ported)
//   [Security]  Play this Tamer                          -> PlaySelfTamerSecurityEffect (DEFERRED, Wave 3:
//               the security-skill activation flow is not yet built; kept for source fidelity but not
//               auto-registered — SecuritySkill is excluded from CardEffectRegistrar.AllTimings.)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST1_12 : CEntity_Effect
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
                        return true;
                    }
                }

                return false;
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    return true;
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeDPStaticEffect(
                permanentCondition: PermanentCondition,
                changeValue: 1000,
                isInheritedEffect: false,
                card: card,
                condition: Condition,
                effectName: () => "Your Digimon gain DP +1000"));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
