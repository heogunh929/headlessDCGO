// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_108.cs
// 1:1 headless mirror of the original BT1_108 (BT1/Green) — an Option.
//   [Main]     1 of your Digimon gets +3000 DP for the turn.
//   AS-IS: ActivateClass on EffectTiming.OptionSkill, CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1
//   (maxCountPerTurn:null), ISOPTIONAL=false. ActivateCoroutine (gated on
//   HasMatchConditionPermanent(CanSelectPermanentCondition), CanSelectPermanentCondition =
//   IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)): SelectPermanentEffect(Mode.Custom) with
//   maxCount = Min(1, MatchConditionPermanentCount), canNoSelect:false, canEndNotMax:false, whose per-target
//   coroutine calls CardEffectCommons.ChangeDigimonDP(+3000, EffectDuration.UntilEachTurnEnd).
//   -> CardEffectFactory.SelectAndBuffDpEffect (AS-IS SelectPermanentEffect Mode.Custom + ChangeDigimonDP) with
//   maxCount:1, changeValue:3000, duration:UntilEachTurnEnd — same shape as BT1_092/BT1_096/BT1_055/ST1_13.
//   CanTriggerOptionMainEffect is subsumed by the OptionSkill activation gate (same as BT1_090/BT1_092/BT1_096);
//   HasMatchConditionPermanent + Min(1,count) are subsumed by SelectAndBuffDpEffect's own "select up to
//   maxCount matching permanents" behaviour (no-op when nothing matches).
//
//   [Security] Suspend 1 of your opponent's Digimon. Then add this card to its owner's hand.
//   AS-IS: ActivateClass on EffectTiming.SecuritySkill, CanUseCondition = CanTriggerSecurityEffect, ORDER=-1,
//   ISOPTIONAL=false, IsSecurityEffect=true. ActivateCoroutine (gated on
//   HasMatchConditionPermanent(CanSelectPermanentCondition), CanSelectPermanentCondition =
//   IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card)): SelectPermanentEffect(Mode.Tap) with
//   maxCount = Min(1, MatchConditionPermanentCount), canNoSelect:false, canEndNotMax:false — no per-target
//   follow-up coroutine (Mode.Tap suspends directly) — THEN, unconditionally, CardEffectCommons.AddThisCardToHand.
//   This is an independent Security action (its own CanSelectPermanentCondition targeting the OPPONENT's battle
//   area, distinct from [Main]'s owner-scoped DP buff) — NOT a reuse of [Main] (unlike ST4_15, whose [Main] and
//   [Security] are both "suspend 1 opponent Digimon" and therefore share one AddActivateMainOptionSecurityEffect
//   call). Mirrored as two independent registrations in AS-IS order — CardEffectFactory.SelectAndSuspendEffect
//   (AS-IS SelectPermanentEffect Mode.Tap) with maxCount:1, canEndNotMax:false, followed unconditionally by
//   CardEffectFactory.AddThisCardToHandEffect — same two-step "conditional select-action then unconditional
//   add-to-hand" ordering as BT1_096's [Security] (DrawCardsEffect + AddThisCardToHandEffect). CanTriggerSecurityEffect
//   is subsumed by the SecuritySkill activation gate (same as BT1_070/ST4_15); HasMatchConditionPermanent +
//   Min(1,count) are subsumed by SelectAndSuspendEffect's own "select up to maxCount matching permanents"
//   behaviour (no-op when nothing matches).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_108 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBuffDpEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                changeValue: 3000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[Main] 1 of your Digimon gets +3000 DP for the turn."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            bool CanSelectSecurityPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndSuspendEffect(
                card: card,
                canTarget: CanSelectSecurityPermanentCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: "[Security] Suspend 1 of your opponent's Digimon. Then add this card its owner's hand."));

            cardEffects.Add(CardEffectFactory.AddThisCardToHandEffect(card));
        }

        return cardEffects;
    }
}
