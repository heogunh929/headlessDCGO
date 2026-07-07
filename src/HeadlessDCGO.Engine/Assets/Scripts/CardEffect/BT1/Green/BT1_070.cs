// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_070.cs
// 1:1 headless mirror of the original BT1_070 (BT1/Green) — a Digimon.
//   [On Play] Suspend 1 of your opponent's Digimon.
//   AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay,
//   CanActivateCondition = IsExistOnBattleArea(card) && HasMatchConditionPermanent(CanSelectPermanentCondition),
//   CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card),
//   ORDER=-1 (maxCountPerTurn:null), ISOPTIONAL=false, ActivateCoroutine = SelectPermanentEffect(Mode.Tap)
//   with maxCount = Min(1, MatchConditionPermanentCount), canNoSelect:false, canEndNotMax:false.
//   Headless mirror: CardEffectFactory.SelectAndSuspendEffect (AS-IS SelectPermanentEffect Mode.Tap) with
//   maxCount:1, canEndNotMax:false — same shape as BT1_023 (Mode.Destroy sibling) and ST4_15 [Main]. The
//   [On Play] play path (PlayCardAction) resolves this card's own OnEnterFieldAnyone effects directly
//   (subject = this card), so CanTriggerOnPlay / IsExistOnBattleArea are structurally satisfied and folded
//   (same as BT1_010/BT1_023/ST4_03). HasMatchConditionPermanent + Min(1,count) are subsumed by
//   SelectAndSuspendEffect's own "select up to maxCount matching permanents" behaviour (no-op when nothing
//   matches).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_070 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndSuspendEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: "[On Play] Suspend 1 of your opponent's Digimon."));
        }

        return cardEffects;
    }
}
