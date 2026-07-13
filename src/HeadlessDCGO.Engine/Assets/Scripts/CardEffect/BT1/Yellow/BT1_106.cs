// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_106.cs
// 1:1 headless mirror of the original BT1_106 (BT1/Yellow) — an Option.
//   [Main] 1 of your opponent's Digimon gets -7000 DP for the turn.
//   AS-IS: ActivateClass on EffectTiming.OptionSkill, CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1
//   (maxCountPerTurn:null), ISOPTIONAL=false, CanActivateCondition = null. ActivateCoroutine: (only if
//   HasMatchConditionPermanent(CanSelectPermanentCondition)) a SelectPermanentEffect(Mode.Custom) with
//   maxCount = Min(1, MatchConditionPermanentCount), canNoSelect:false, canEndNotMax:false, whose per-target
//   coroutine calls ChangeDigimonDP(-7000, UntilEachTurnEnd). CanSelectPermanentCondition =
//   IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card).
//   No [Security] block exists in AS-IS for this card (Option has no security-check effect) — not ported.
//   Headless mirror: CardEffectFactory.SelectAndBuffDpEffect (AS-IS SelectPermanentEffect Mode.Custom +
//   ChangeDigimonDP) with maxCount:1, -7000, UntilEachTurnEnd, canTarget = opponent battle-area Digimon
//   (same predicate idiom as BT1_062's [When Digivolving] -8000 DP sibling). CanTriggerOptionMainEffect is
//   subsumed by the OptionSkill activation gate (same as ST1_13/BT1_090/BT1_092/BT1_096); HasMatchCondition-
//   Permanent + Min(1,count) are subsumed by SelectAndBuffDpEffect's own "select up to maxCount matching
//   permanents" behaviour (no-op when nothing matches, same as BT1_023/BT1_094).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_106 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBuffDpEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                changeValue: -7000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[Main] 1 of your opponent's Digimon gets -7000 DP for the turn."));
        }

        return cardEffects;
    }
}
