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
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
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

        // STOP: [Security] "Your opponent's Digimon don't unsuspend during their next unsuspend phase."
        // (SecuritySkill). AS-IS: ActivateClass(CanUseCondition = CanTriggerSecurityEffect, ORDER=-1,
        // ISOPTIONAL=false, IsSecurityEffect=true). ActivateCoroutine has NO SelectPermanentEffect — it directly
        // calls `CardEffectCommons.GainCanNotUnsuspendPlayerEffect(permanentCondition: opponent-battle-area-
        // Digimon-not-already-CanNotBeAffected, effectDuration: UntilOwnerActivePhase, activateClass,
        // isOnlyActivePhase: true, effectName: "Your Digimon can't unsuspend")` — a broad, no-select, player-scope
        // grant to EVERY currently/future-qualifying opponent Digimon for one unsuspend phase.
        //
        // The exact AS-IS primitive this needs IS already ported verbatim: CardEffectCommons.
        // GainCanNotUnsuspendPlayerEffect(permanentCondition, effectDuration, sourceCard, isOnlyActivePhase,
        // effectName) — CardPortingFramework.cs ~8725, documented "AS-IS GainCanNotUnsuspendPlayerEffect
        // (GiveEffectToPlayer/CanNotUnsuspend.cs:10, verbatim)". It self-registers a duration-tagged player-scope
        // binding directly via EffectRegistry.Register (GainToPlayerScope), so — unlike
        // ContinuousPlayerScopeRestrictionEffect's ToBinding-hardcoded-null gap flagged in BT1_100 — the
        // duration/registration half is NOT the problem here.
        //
        // But there is no composition that reaches this call from an Option/Security activation without a player
        // selection — the identical gap class already identified and left unregistered in BT1_100 [Main]/
        // [Security] ("Until the end of your opponent's next turn, their Digimon with no digivolution cards can't
        // attack" / its Security twin), just for CanNotUnsuspendKey instead of CannotAttackKey:
        //   - CardEffectFactory.SelectAndRestrictEffect / ActivatedTargetRestrictionEffect (ST2_14/this card's own
        //     [Main] shape) forces a SelectPermanentEffect step — wrong here, AS-IS has no select at all; forcing
        //     one would add player agency the original doesn't have (fidelity fail).
        //   - ActivatedPlayerScopeBuffEffect (ActivatedEffectResolver dispatch) is the one broad-scope, no-select,
        //     activation-dispatched class, but it only carries a numeric delta under a ModifierHelpers key
        //     (DP/SAttack); no restriction-key/predicate support, so it cannot express CannotUnsuspendKey +
        //     an isOnlyActivePhase-gated opponent-battle-area filter.
        //   - GrantContinuousBody (ActivatedEffect.cs:255) is the one uniform body that registers a continuous
        //     ICardEffect's ToBinding() from within an activation — but it requires an ICardEffect instance
        //     (calls `_continuous.ToBinding(id)`); GainCanNotUnsuspendPlayerEffect is a self-registering static
        //     procedure (bool-returning, calls EffectRegistry.Register itself, no ToBinding/ICardEffect wrapper
        //     around it exists), so there is nothing of the right shape to hand GrantContinuousBody.
        // Grepped every IEffectBody impl in ActivatedEffect.cs and every IActivatedCardEffect class in
        // CardPortingFramework.cs (twice) — confirmed gap, not a naming miss: no existing body/class composes
        // "call a Gain*PlayerEffect commons procedure directly from an Option/Security activation, with no target
        // selection." Same primitive gap already deferred for BT1_100 (강모델 primitive pass); this card needs the
        // identical composition (e.g. a "GrantPlayerScopeRestrictionBody" wrapping the Gain*PlayerEffect commons
        // calls) — engine-side primitive development, out of scope for a single-card porting pass and outside the
        // no-engine-edit constraint for this task. — 강모델
        // if (timing == EffectTiming.SecuritySkill) { ... }

        return cardEffects;
    }
}
