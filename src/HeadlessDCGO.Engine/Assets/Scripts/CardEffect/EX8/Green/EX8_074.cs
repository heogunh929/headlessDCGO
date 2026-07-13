// Source: Assets/Scripts/CardEffect/EX8/Green/EX8_074.cs
// 1:1 headless mirror of the original EX8_074 (DCGO/Assets/Scripts/CardEffect/EX8/Green/EX8_074.cs), region
// for region. The original keys [When Digivolving] / [All Turns] under OnEnterFieldAnyone with gates; the
// headless idiom uses the dedicated WhenDigivolving timing (for the [When Digivolving] body) and
// OnEnterFieldAnyone (for the [All Turns] re-trigger), consistent with existing ports (e.g. ST1_08).
//
// Region map (original -> headless):
//   #1 [When Would be Played] suspend 2 Digimon -> Play Cost -4   -> BeforePayCost / SuspendCostReductionEffect
//   #2 [None] isCheckAvailability "-4 not shown"                  -> SUBSUMED: PlayCardAction's BeforePayCost
//        availability reduction (Stage 3 brick 3) reads #1 directly, so the separate availability-only
//        ChangeCostClass is unnecessary in the single-cost model — no behavioural gap.
//   #3 [All Turns] <Alliance>                                     -> OnAllyAttack / AllianceSelfEffect
//   #4 [End of Your Turn] <Vortex>                                -> OnEndTurn / VortexSelfEffect
//   #5 [When Digivolving] suspend 1 -> delete 1 opp <=8000(+3000/suspended) -> WhenDigivolving / ActivatedSelect x2
//   #6 [All Turns] (Once Per Turn) on Digimon played: activate this Digimon's [When Digivolving] effects
//        -> OnEnterFieldAnyone / ReuseWhenDigivolvingEffect
//
// Live coverage: #1 (cost reduction + availability) and #4 (Vortex end-of-turn attack) resolve LIVE. #3
// Alliance registers as a live keyword binding. #5/#6 are activated effects resolved via the activation
// flow (ActivatedEffectResolver) — the same bar as every existing [When Digivolving] port (DigivolveAction
// emits the trigger; full self-play auto-resolution of WhenDigivolving activated effects is a general
// live-activation gap, not an EX8_074-specific one).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;

public sealed class EX8_074 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region When Would be Played

        // "When this card would be played, by suspending 2 Digimon, reduce the play cost by 4."
        // (B.O.4 #1) This is a "[When Would be Played]" reduction — gate to the PLAY root so it does NOT fire
        // when EX8_074 is DIGIVOLVED (AS-IS rootCondition; the BeforePayCost timing is shared by play/digivolve).
        if (timing == EffectTiming.BeforePayCost && CardEffectCommons.IsPayCostRoot(card, Headless.Bridge.PayCostRoot.Play))
        {
            // AS-IS CanSelectPermanentCondition (original EX8_074.cs):
            //   IsPermanentExistsOnBattleAreaDigimon(p) && p.TopCard && !p.TopCard.CanNotBeAffected(activateClass)
            //   && !p.IsSuspended && p.CanSuspend
            // The original predicate is NOT owner-scoped — IsPermanentExistsOnBattleAreaDigimon has no owner
            // filter (GameContextDeterminarion.cs:499 = IsPermanentExistsOnBattleArea && IsDigimon), so EITHER
            // player's Digimon may be suspended to pay this reduction. Mirror it with IsBattleAreaDigimon
            // (any-owner; the documented mirror of IsPermanentExistsOnBattleAreaDigimon — CardPortingFramework
            // :1709). !IsSuspended is a genuine selection-time guard (a suspended Digimon can't pay the cost).
            // The other two original guards are NOT re-checked here, by headless design: CanNotBeAffected
            // (effect immunity) is centralised at the mutation sink (ContinuousImmunityGate, S2), and CanSuspend
            // ("cannot be suspended") is not modeled engine-wide (CanSuspend.cs is an unported skeleton — no
            // card sets it false), so adding either to the predicate would be redundant / invented.
            bool CanSelectPermanentCondition(HeadlessEntityId id) =>
                CardEffectCommons.IsBattleAreaDigimon(card, id) && !CardEffectCommons.IsSuspended(card, id);

            if (CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition) >= 2)
            {
                const string desc = "When this card would be played, by suspending 2 Digimon, reduce the play cost by 4.";
                cardEffects.Add(new ActivatedEffect(
                    card, EffectTiming.None, canUse: null, canActivate: null,
                    body: new SuspendCostReductionEffect(card, CanSelectPermanentCondition, suspendCount: 2, costReduction: 4, description: desc),
                    maxCountPerTurn: null, isOptional: false, desc));
            }
        }

        #endregion

        #region Alliance

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.AllianceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        #endregion

        #region Vortex

        if (timing == EffectTiming.OnEndTurn)
        {
            cardEffects.Add(CardEffectFactory.VortexSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        #endregion

        #region When Digivolving

        // "[When Digivolving] You may suspend 1 Digimon. Then, you may delete 1 of your opponent's 8000 DP or
        // lower Digimon. For each other suspended Digimon, add 3000 to this DP deletion effect's maximum."
        if (timing == EffectTiming.WhenDigivolving)
        {
            bool CanSelectSuspendPermanentCondition(HeadlessEntityId id) =>
                CardEffectCommons.IsBattleAreaDigimon(card, id);

            // Both effects below carry the ONE AS-IS ActivateClass's gates (original EX8_074.cs:243-245):
            //   CanUseCondition (:254-257) = CanTriggerWhenDigivolving — subsumed by the dedicated
            //   WhenDigivolving timing dispatch (DigivolveAction emits exactly this trigger; same convention as
            //   ST4_10 / BT1_074), so canUse stays null here.
            //   CanActivateCondition (:275-278) = IsExistOnBattleAreaDigimon(card) — the per-pass half, mirrored
            //   on canActivate.
            cardEffects.Add(new ActivatedEffect(
                card, EffectTiming.WhenDigivolving,
                canUse: null,
                canActivate: () => CardEffectCommons.IsExistOnBattleAreaDigimon(card),
                body: new ActivatedSelectEffect(card, CanSelectSuspendPermanentCondition, maxCount: 1, canNoSelect: true, canEndNotMax: false,
                    SelectPermanentEffect.Mode.Tap, "[When Digivolving] You may suspend 1 Digimon."),
                maxCountPerTurn: null, isOptional: false, "[When Digivolving] You may suspend 1 Digimon."));

            int DeletionMaxDP() => CardEffectCommons.MaxDpDeleteThreshold(card,
                8000 + 3000 * CardEffectCommons.MatchConditionPermanentCount(card,
                    id => CardEffectCommons.IsBattleAreaDigimon(card, id)
                        && CardEffectCommons.IsSuspended(card, id)
                        && id != card.InstanceId));

            bool CanSelectDeletePermanentCondition(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id) && CardEffectCommons.CurrentDp(card, id) <= DeletionMaxDP();

            cardEffects.Add(new ActivatedEffect(
                card, EffectTiming.WhenDigivolving,
                canUse: null,
                canActivate: () => CardEffectCommons.IsExistOnBattleAreaDigimon(card), // AS-IS CanActivateCondition (:275-278)
                body: new ActivatedSelectEffect(card, CanSelectDeletePermanentCondition, maxCount: 1, canNoSelect: true, canEndNotMax: false,
                    SelectPermanentEffect.Mode.Destroy,
                    "Then, you may delete 1 of your opponent's 8000 DP or lower Digimon (+3000 per other suspended Digimon)."),
                maxCountPerTurn: null, isOptional: false,
                "Then, you may delete 1 of your opponent's 8000 DP or lower Digimon (+3000 per other suspended Digimon)."));
        }

        #endregion

        #region All Turns

        // "[All Turns] (Once Per Turn) When Digimon are played, you may activate 1 of this Digimon's
        // [When Digivolving] effects."
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            cardEffects.Add(new ReuseWhenDigivolvingEffect(
                "[All Turns] (Once Per Turn) When Digimon are played, you may activate 1 of this Digimon's [When Digivolving] effects."));
        }

        #endregion

        return cardEffects;
    }
}
