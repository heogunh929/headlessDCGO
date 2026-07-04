using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Headless.Effects;

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// GENERATED FROM porting/data/ir/BT1.Red/BT1_015.json — DO NOT EDIT (pipeline-v3 codegen).
public sealed class BT1_015 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return CardEffectCommons.IsOwnerTurn(card);
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(
                changeValue: 2000,
                isInheritedEffect: true,
                card: card,
                condition: Condition));
        }
        return cardEffects;
    }
}
