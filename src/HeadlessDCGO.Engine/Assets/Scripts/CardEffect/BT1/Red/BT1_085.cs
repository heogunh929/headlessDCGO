using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Headless.Effects;

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// GENERATED FROM porting/data/ir/BT1.Red/BT1_085.json — DO NOT EDIT (pipeline-v3 codegen).
public sealed class BT1_085 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();
        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(
                card: card));
        }

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return (CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card));
            }

            bool PermanentCondition(Permanent permanent)
            {
                return (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) && (permanent.TopCard.HasCardColor("Red") && (permanent.DigivolutionCards.Count >= 4)));
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
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(
                card: card));
        }
        return cardEffects;
    }
}
