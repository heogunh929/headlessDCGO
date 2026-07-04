using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_098 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            // STOP: GManager — 강모델
            // STOP: SelectPermanentEffect — 강모델
            // STOP: ContinuousController — 강모델
            // STOP: CardEffectCommons.GainJamming — 강모델
            // STOP: CardEffectCommons.MatchConditionPermanentCount — 강모델
            // STOP: CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon — 강모델
            // STOP: CardEffectCommons.CanTriggerOptionMainEffect — 강모델
            // STOP: CardEffectCommons.AddThisCardToHand — 강모델
            // STOP: CardEffectCommons.CanTriggerSecurityEffect — 강모델
        }


        if (timing == EffectTiming.SecuritySkill)
        {
            // STOP: GManager — 강모델
            // STOP: SelectPermanentEffect — 강모델
            // STOP: ContinuousController — 강모델
            // STOP: CardEffectCommons.GainJamming — 강모델
            // STOP: CardEffectCommons.MatchConditionPermanentCount — 강모델
            // STOP: CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon — 강모델
            // STOP: CardEffectCommons.CanTriggerOptionMainEffect — 강모델
            // STOP: CardEffectCommons.AddThisCardToHand — 강모델
            // STOP: CardEffectCommons.CanTriggerSecurityEffect — 강모델
        }

        return cardEffects;
    }
}