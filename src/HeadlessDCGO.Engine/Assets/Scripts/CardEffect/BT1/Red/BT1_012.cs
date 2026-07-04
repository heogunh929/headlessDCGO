using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Headless.Effects;

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// GENERATED FROM porting/data/ir/BT1.Red/BT1_012.json — DO NOT EDIT (pipeline-v3 codegen).
public sealed class BT1_012 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();
        if (timing == EffectTiming.OnBlockAnyone)
        {
            bool Condition()
            {
                return (CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card));
            }

            cardEffects.Add(CardEffectFactory.SelfDpBuffTriggerEffect(
                timing: EffectTiming.OnBlockAnyone,
                changeValue: 2000,
                duration: EffectDuration.UntilEachTurnEnd,
                card: card,
                condition: Condition,
                description: "[Your Turn] When this Digimon is blocked, it gets +2000 DP."));
        }
        return cardEffects;
    }
}
