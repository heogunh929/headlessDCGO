// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_079.cs
// 1:1 mirror of the original BT1_079 (BT1/Green).
//   [When Attacking] Suspend 1 of your opponent's Digimon without <Blocker>.
//   AS-IS: ActivateClass on EffectTiming.OnAllyAttack, CanUseCondition = CanTriggerOnAttack(hashtable, card),
//   CanActivateCondition = IsExistOnBattleArea(card) && HasMatchConditionPermanent(CanSelectPermanentCondition),
//   CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) && !permanent.HasBlocker,
//   ORDER=-1 (maxCountPerTurn:null), ISOPTIONAL=false, SetIsInheritedEffect(true),
//   ActivateCoroutine = SelectPermanentEffect(Mode.Tap) with maxCount = Min(1, MatchConditionPermanentCount),
//   canNoSelect:false, canEndNotMax:false, mode:Tap.
// Headless mirror: the uniform ActivatedEffect (= AS-IS ActivateClass) with body=SelectBody(Mode.Tap), same
// shape as BT1_003/BT1_022/BT1_023. HasMatchConditionPermanent + Min(1,count) are subsumed by SelectBody's own
// "select up to maxCount matching permanents" behaviour (no-op when nothing matches, clamped to live candidate
// count — same rationale as BT1_023). AS-IS permanent.HasBlocker is mirrored via the self-static keyword gate
// (ContinuousKeywordGate.HasKeyword), negated for "without <Blocker>".
// NOTE: AS-IS SetIsInheritedEffect(true) (whether this buried-source's trigger still fires post-digivolution)
// has no equivalent parameter on the uniform ActivatedEffect — same accepted, documented gap as BT1_022's
// identical flag (not modeled per-card; a systemic activated-effect inheritance gap, not specific to this card).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_079 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                    && !ContinuousKeywordGate.HasKeyword(card.Context, id, ContinuousKeywordGate.Blocker);

            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAllyAttack,
                canUse: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card),
                canActivate: CanActivate,
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelectPermanentCondition,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Tap,
                    description: "[When Attacking] Suspend 1 of your opponent's Digimon without <Blocker>."),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[When Attacking] Suspend 1 of your opponent's Digimon without <Blocker>."));
        }

        return cardEffects;
    }
}
