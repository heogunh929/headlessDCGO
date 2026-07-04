using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_043 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            // STOP: ActivateClass — 강모델
            // STOP: SelectPermanentEffect — 강모델
            // STOP: GManager.instance.GetComponent<SelectPermanentEffect> — 강모델
            // STOP: ContinuousController.instance.StartCoroutine — 강모델
            // STOP: CardEffectCommons.TrashDigivolutionCardsFromTopOrBottom — 강모델
        }

        return cardEffects;
    }
}