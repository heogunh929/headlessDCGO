using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Headless.Effects;

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// GENERATED FROM porting/data/ir/BT1.Red/BT1_001.json — DO NOT EDIT (pipeline-v3 codegen).
public sealed class BT1_001 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();
        if (timing == EffectTiming.OnAllyAttack)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            cardEffects.Add(CardEffectFactory.SelfDpBuffTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                changeValue: 1000,
                duration: EffectDuration.UntilEachTurnEnd,
                card: card,
                condition: Condition,
                description: "[When Attacking] If you attack an opponent's Digimon, this Digimon gets +1000 DP for the turn."));
        }
        return cardEffects;
    }
}
