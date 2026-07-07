// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_061.cs
//   [On Play] 2 of your opponent's Digimon get -3000 DP for the turn.
// 1:1 mirror of the AS-IS BT1_061: ActivateClass on OnEnterFieldAnyone. CanUseCondition = CanTriggerOnPlay.
//   CanActivateCondition = IsExistOnBattleArea(card) && HasMatchConditionPermanent(CanSelectPermanentCondition),
//   CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon. ORDER=-1, ISOPTIONAL=false.
//   ActivateCoroutine: maxCount = Min(2, MatchConditionPermanentCount); SelectPermanentEffect.SetUp(mode: Custom,
//   maxCount, canNoSelect:false, canEndNotMax:false) — a MANDATORY pick of exactly min(2, available) opponent
//   Digimon (canEndNotMax:false forces minCount == maxCount, i.e. never fewer than 2 when 2+ are legal). Per
//   selected permanent: CardEffectCommons.ChangeDigimonDP(changeValue: -3000, UntilEachTurnEnd) (current-DP delta).
// Headless mirror: uniform ActivatedEffect + SelectBody(Mode.Custom, maxCount:2, canEndNotMax:false — the
//   mandatory-multi-pick the DP-buff factory couldn't express) with the AS-IS SelectPermanentCoroutine follow-up
//   wired via SelectBody.onEachSelected -> ChangeDigimonDP(-3000) on each picked id.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_061 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelect(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: ctx => CardEffectCommons.CanTriggerOnPlay(ctx, card),
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.HasMatchConditionPermanent(card, CanSelect),
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelect,
                    maxCount: 2,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Custom,
                    description: "[On Play] 2 of your opponent's Digimon get -3000 DP for the turn.",
                    onEachSelected: id => CardEffectCommons.ChangeDigimonDP(
                        new Permanent(card.Context, id, card.Owner), changeValue: -3000, EffectDuration.UntilEachTurnEnd, card)),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[On Play] 2 of your opponent's Digimon get -3000 DP for the turn."));
        }

        return cardEffects;
    }
}
