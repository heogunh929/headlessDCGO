// 1:1 mirror of the original BT1_092 (BT1/Red) — an Option.
//   [Main] Trigger <Draw 2> (Draw 2 cards from your deck). Then 1 of your Digimon gets +2000 DP for the turn.
//   AS-IS: ActivateClass on EffectTiming.OptionSkill, CanUseCondition = CanTriggerOptionMainEffect,
//   CanActivateCondition = null (unconditional), ORDER=-1 (maxCountPerTurn:null), ISOPTIONAL=false.
//   ActivateCoroutine: `yield return new DrawClass(card.Owner, 2, activateClass).Draw()`, THEN (only if
//   HasMatchConditionPermanent(CanSelectPermanentCondition) — a guard INSIDE the coroutine, not a separate
//   CanActivateCondition) a SelectPermanentEffect(Mode.Custom) with maxCount = Min(1,
//   MatchConditionPermanentCount), canNoSelect:false, canEndNotMax:false, whose per-target coroutine calls
//   ChangeDigimonDP(+2000, UntilEachTurnEnd). CanSelectPermanentCondition =
//   IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card).
//   No [Security] block exists in AS-IS for this card (Option has no security-check effect) — not ported.
//   Headless mirror: two uniform ActivatedEffect registrations under OptionSkill, in AS-IS coroutine order —
//   DrawBody(2) then body=ActivatedTargetBuffEffect (AS-IS SelectPermanentEffect Mode.Custom + ChangeDigimonDP)
//   — both with explicit CanUse=CanTriggerOptionMainEffect and CanActivate=null (AS-IS passes null literally;
//   the HasMatchConditionPermanent guard lives inside the coroutine, mirrored by the select body's own
//   clamp-to-live-candidates no-op). BT1_096 establishes that multiple cardEffects.Add() calls under one timing
//   resolve in registration order, matching the AS-IS single sequential coroutine.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_092 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            const string description =
                "[Main] Trigger <Draw 2> (Draw 2 cards from your deck). Then 1 of your Digimon gets +2000 DP for the turn.";

            // AS-IS CanUseCondition: CanTriggerOptionMainEffect(hashtable, card).
            bool CanUse(CardEffectResolveContext ctx) => CardEffectCommons.CanTriggerOptionMainEffect(ctx, card);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OptionSkill,
                canUse: CanUse,
                canActivate: null, // AS-IS CanActivateCondition: null.
                body: new DrawBody(2),
                maxCountPerTurn: null,
                isOptional: false,
                description: description));

            // AS-IS CanSelectPermanentCondition: IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card).
            bool CanSelect(HeadlessEntityId id) => CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OptionSkill,
                canUse: CanUse,
                canActivate: null, // AS-IS CanActivateCondition: null (HasMatchConditionPermanent guards the coroutine step, not CanActivate).
                body: new ActivatedTargetBuffEffect(
                    card, CanSelect, maxCount: 1, ModifierHelpers.DpDeltaKey, changeValue: 2000,
                    EffectDuration.UntilEachTurnEnd, "Select 1 Digimon that will get DP +2000."),
                maxCountPerTurn: null,
                isOptional: false,
                description: description));
        }

        return cardEffects;
    }
}
