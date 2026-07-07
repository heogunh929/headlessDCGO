// 1:1 headless mirror of the original BT1_023 (BT1/Red) — a Digimon.
//   [On Play] Delete 1 of your opponent's Digimon with <Blocker>.
//   AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay,
//   CanActivateCondition = IsExistOnBattleArea(card) && HasMatchConditionPermanent(CanSelectPermanentCondition),
//   CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) && permanent.HasBlocker,
//   ORDER=-1 (maxCountPerTurn:null), ISOPTIONAL=false, ActivateCoroutine = SelectPermanentEffect(Mode.Destroy)
//   with maxCount = Min(1, MatchConditionPermanentCount), canNoSelect:false, canEndNotMax:false.
//   Headless mirror: CardEffectFactory.SelectAndDestroyEffect (AS-IS SelectPermanentEffect Mode.Destroy) with
//   maxCount:1, canEndNotMax:false. The [On Play] play path (PlayCardAction) resolves this card's own
//   OnEnterFieldAnyone effects directly (subject = this card), so CanTriggerOnPlay / IsExistOnBattleArea are
//   structurally satisfied and folded (same as BT1_010/ST4_03). HasMatchConditionPermanent + Min(1,count) are
//   subsumed by SelectAndDestroyEffect's own "select up to maxCount matching permanents" behaviour (no-op when
//   nothing matches). AS-IS permanent.HasBlocker is mirrored via the self-static keyword gate.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_023 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                    && ContinuousKeywordGate.HasKeyword(card.Context, id, ContinuousKeywordGate.Blocker);
            }

            cardEffects.Add(CardEffectFactory.SelectAndDestroyEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: "[On Play] Delete 1 of your opponent's Digimon with <Blocker>."));
        }

        return cardEffects;
    }
}
