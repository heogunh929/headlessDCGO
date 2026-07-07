// 1:1 headless mirror of the original BT1_092 (BT1/Red) — an Option.
//   [Main] Trigger <Draw 2> (Draw 2 cards from your deck). Then 1 of your Digimon gets +2000 DP for the turn.
//   AS-IS: ActivateClass on EffectTiming.OptionSkill, CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1
//   (maxCountPerTurn:null), ISOPTIONAL=false. ActivateCoroutine: `yield return new DrawClass(card.Owner, 2,
//   activateClass).Draw()`, THEN (only if HasMatchConditionPermanent(CanSelectPermanentCondition)) a
//   SelectPermanentEffect(Mode.Custom) with maxCount = Min(1, MatchConditionPermanentCount), canNoSelect:false,
//   canEndNotMax:false, whose per-target coroutine calls ChangeDigimonDP(+2000, UntilEachTurnEnd).
//   CanSelectPermanentCondition = IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card).
//   No [Security] block exists in AS-IS for this card (Option has no security-check effect) — not ported.
//   Headless mirror: CardEffectFactory.DrawCardsEffect(2) followed by CardEffectFactory.SelectAndBuffDpEffect
//   (AS-IS SelectPermanentEffect Mode.Custom + ChangeDigimonDP) with maxCount:1, +2000, UntilEachTurnEnd — same
//   two-step "draw then optional select-buff" ordering as the AS-IS coroutine (BT1_096 establishes that
//   multiple cardEffects.Add() calls under one timing resolve in registration order). CanTriggerOptionMainEffect
//   is subsumed by the OptionSkill activation gate (same as ST1_13/ST1_15/BT1_090/BT1_094); HasMatchCondition-
//   Permanent + Min(1,count) are subsumed by SelectAndBuffDpEffect's own "select up to maxCount matching
//   permanents" behaviour (no-op when nothing matches, same as BT1_023/BT1_094).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_092 : CEntity_Effect
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

            cardEffects.Add(CardEffectFactory.DrawCardsEffect(card, 2));

            cardEffects.Add(CardEffectFactory.SelectAndBuffDpEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                changeValue: 2000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[Main] Trigger <Draw 2> (Draw 2 cards from your deck). Then 1 of your Digimon gets +2000 DP for the turn."));
        }

        return cardEffects;
    }
}
