using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// GENERATED FROM porting/data/ir/BT1.Red/BT1_018.json — DO NOT EDIT (pipeline-v3 codegen).
public sealed class BT1_018 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return (CardEffectCommons.IsExistOnBattleArea(card) && (CardEffectCommons.IsOwnerTurn(card) && (CardEffectCommons.MemoryForPlayer(card) >= 3)));
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(
                changeValue: 1,
                isInheritedEffect: false,
                card: card,
                condition: Condition));
        }
        return cardEffects;
    }
}
