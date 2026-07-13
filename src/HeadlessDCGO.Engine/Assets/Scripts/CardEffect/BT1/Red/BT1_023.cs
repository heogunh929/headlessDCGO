// 1:1 mirror of the original BT1_023 (BT1/Red) — a Digimon.
//   [On Play] Delete 1 of your opponent's Digimon with <Blocker>.
//   AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay,
//   CanActivateCondition = IsExistOnBattleArea(card) && HasMatchConditionPermanent(CanSelectPermanentCondition),
//   CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) && permanent.HasBlocker,
//   ORDER=-1 (maxCountPerTurn:null), ISOPTIONAL=false, ActivateCoroutine = SelectPermanentEffect(Mode.Destroy)
//   with maxCount = Min(1, MatchConditionPermanentCount), canNoSelect:false, canEndNotMax:false.
//   Headless mirror: the uniform ActivatedEffect (= AS-IS ActivateClass) with explicit CanUse/CanActivate gates
//   (CanTriggerOnPlay / IsExistOnBattleArea / HasMatchConditionPermanent, not folded away) and
//   body=ActivatedSelectEffect (AS-IS SelectPermanentEffect Mode.Destroy). AS-IS permanent.HasBlocker is
//   mirrored via the self-static keyword gate (ContinuousKeywordGate).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_023 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            const string description = "[On Play] Delete 1 of your opponent's Digimon with <Blocker>.";

            // AS-IS CanSelectPermanentCondition: IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card)
            // && permanent.HasBlocker.
            bool CanSelect(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                && ContinuousKeywordGate.HasKeyword(card.Context, id, ContinuousKeywordGate.Blocker);

            // AS-IS CanUseCondition: CanTriggerOnPlay(hashtable, card).
            bool CanUse(CardEffectResolveContext ctx) => CardEffectCommons.CanTriggerOnPlay(ctx, card);

            // AS-IS CanActivateCondition: IsExistOnBattleArea(card) && HasMatchConditionPermanent(CanSelect).
            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.HasMatchConditionPermanent(card, CanSelect);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new ActivatedSelectEffect(
                    card, CanSelect, maxCount: 1, canNoSelect: false, canEndNotMax: false,
                    SelectPermanentEffect.Mode.Destroy, description),
                maxCountPerTurn: null,
                isOptional: false,
                description: description));
        }

        return cardEffects;
    }
}
