// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_113.cs
// 1:1 mirror of the original BT1_113 (BT1/Green) — an Option.
//   [Main] Until the end of your opponent's next turn, 1 of your opponent's Digimon can't attack or block.
//   AS-IS (OptionSkill): ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false).
//   ActivateCoroutine: guarded by maxCount = Min(1, MatchConditionPermanentCount(CanSelectPermanentCondition));
//   CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) (NO
//   HasNoDigivolutionCards filter, unlike ST2_14/BT1_100 — any opponent battle-area Digimon qualifies).
//   SelectPermanentEffect(mode: Custom, maxCount, canNoSelect:false, canEndNotMax:false); its per-target
//   coroutine calls BOTH CardEffectCommons.GainCanNotAttack(selectedPermanent, defenderCondition:null,
//   EffectDuration.UntilOpponentTurnEnd, activateClass) AND GainCanNotBlock(selectedPermanent,
//   attackerCondition:null, EffectDuration.UntilOpponentTurnEnd, activateClass) on the ONE selected permanent —
//   same "select 1 -> can't-attack + can't-block, until opponent turn end" shape as ST2_14/BT1_100 [Main], minus
//   the HasNoDigivolutionCards narrowing.
//   Headless mirror: CardEffectFactory.SelectAndRestrictEffect (ActivatedTargetRestrictionEffect) — the exact
//   ST2_14 shape — with canTarget = IsOpponentBattleAreaDigimon only (no digivolution-card filter, matching
//   AS-IS's narrower CanSelectPermanentCondition), maxCount:1, cannotAttack:true, cannotBlock:true,
//   duration:UntilOpponentTurnEnd. CanTriggerOptionMainEffect + HasMatchConditionPermanent/Min(1,count) are
//   subsumed by the OptionSkill activation gate + SelectAndRestrictEffect's own "select up to maxCount matching
//   permanents" behaviour (no-op when nothing matches), same as ST2_14/BT1_092/BT1_094.
//
//   [Security] "Your opponent's Digimon don't unsuspend during their next unsuspend phase." -> STOP (see below).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_113 : CEntity_Effect
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

            cardEffects.Add(CardEffectFactory.SelectAndRestrictEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                duration: EffectDuration.UntilOpponentTurnEnd,
                cannotAttack: true,
                cannotBlock: true,
                description: "[Main] Until the end of your opponent's next turn, 1 of your opponent's Digimon can't attack or block."));
        }

        // [Security] "Your opponent's Digimon don't unsuspend during their next unsuspend phase." (SecuritySkill)
        // AS-IS: ActivateClass(CanUseCondition = CanTriggerSecurityEffect, ORDER=-1, ISOPTIONAL=false,
        // IsSecurityEffect=true). ActivateCoroutine has NO SelectPermanentEffect — it directly calls
        // CardEffectCommons.GainCanNotUnsuspendPlayerEffect(permanentCondition = opponent-battle-area-Digimon
        // (the AS-IS !CanNotBeAffected guard is folded into GainToPlayerScope's live CanUse), effectDuration:
        // UntilOwnerActivePhase, isOnlyActivePhase: true) — a broad, no-select, player-scope grant to EVERY
        // currently/future-qualifying opponent Digimon for one unsuspend phase.
        // Headless mirror: uniform ActivatedEffect whose body is GrantPlayerScopeRestrictionBody invoking
        // GainCanNotUnsuspendPlayerEffect (verbatim AS-IS mirror) directly — same no-select player-scope grant
        // shape as BT1_100, for CannotUnsuspendKey instead of CannotAttackKey.
        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.SecuritySkill,
                canUse: ctx => CardEffectCommons.CanTriggerSecurityEffect(ctx, card),
                canActivate: null,
                body: new GrantPlayerScopeRestrictionBody(c => CardEffectCommons.GainCanNotUnsuspendPlayerEffect(
                    permanentCondition: p => CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(p, c),
                    effectDuration: EffectDuration.UntilOwnerActivePhase,
                    sourceCard: c,
                    isOnlyActivePhase: true,
                    effectName: "Your Digimon can't unsuspend")),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Security] Your opponent's Digimon don't unsuspend during their next unsuspend phase."));
        }

        return cardEffects;
    }
}
