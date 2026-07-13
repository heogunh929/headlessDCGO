// 1:1 headless mirror of the original BT1_055 (BT1/Yellow) — a Digimon.
//   [On Play] 1 of your opponent's Digimon gets -3000 DP for the turn.
//   AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay,
//   CanActivateCondition = IsExistOnBattleArea(card) && HasMatchConditionPermanent(CanSelectPermanentCondition),
//   CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card),
//   ORDER=-1 (maxCountPerTurn:null), ISOPTIONAL=false, ActivateCoroutine = SelectPermanentEffect(Mode.Custom)
//   with maxCount = Min(1, MatchConditionPermanentCount), canNoSelect:false, canEndNotMax:false, then
//   ChangeDigimonDP(changeValue:-3000, EffectDuration.UntilEachTurnEnd).
//   Headless mirror: CardEffectFactory.SelectAndBuffDpEffect (AS-IS SelectPermanentEffect + ChangeDigimonDP) with
//   maxCount:1, changeValue:-3000, duration:UntilEachTurnEnd. The [On Play] path (PlayCardAction) resolves this
//   card's own OnEnterFieldAnyone effects directly (subject = this card), so CanTriggerOnPlay / IsExistOnBattleArea
//   are structurally satisfied and folded (same as BT1_010/BT1_023). HasMatchConditionPermanent + Min(1,count) are
//   subsumed by SelectAndBuffDpEffect's own "select up to maxCount matching permanents" behaviour (no-op when
//   nothing matches).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_055 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBuffDpEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                changeValue: -3000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[On Play] 1 of your opponent's Digimon gets -3000 DP for the turn."));
        }

        return cardEffects;
    }
}
