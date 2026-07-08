// Source: Assets/Scripts/CardEffect/BT1/White/BT1_084.cs (a White Digimon, two ActivateClass branches)
// AS-IS:
//   Branch 1 — declared under EffectTiming.OnEnterFieldAnyone but CanUseCondition =
//   CanTriggerWhenDigivolving(hashtable, card) (description "[When Digivolving] Choose 1 of your opponent's
//   Digimon. Delete all of your opponent's Digimon that share a name with it."): the headless mirror of this
//   idiom is to declare it under EffectTiming.WhenDigivolving instead (BT1_074 / ST1_08 / BT1_017 precedent —
//   the bridge routes activated selects declared under OnEnterFieldAnyone nowhere live, so the
//   CanTriggerWhenDigivolving-gated ActivateClass is ported under the WhenDigivolving branch). CanActivateCondition
//   = IsExistOnBattleArea(card) && HasMatchConditionPermanent(CanSelectPermanentCondition), where
//   CanSelectPermanentCondition(permanent) = IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) (no
//   further narrowing — ANY opponent Digimon is a legal reference target). ORDER=-1, ISOPTIONAL=false.
//   ActivateCoroutine: SelectPermanentEffect(selectPlayer: card.Owner, mode: Custom, maxCount = Min(1,
//   MatchConditionPermanentCount), canNoSelect:false, canEndNotMax:false) picking exactly 1 reference Digimon,
//   THEN (SelectPermanentCoroutine, run once the reference is picked): destroyTargetPermanents =
//   card.Owner.Enemy.GetBattleAreaDigimons().Filter(p => p.TopCard.HasSameCardName(referencePermanent.TopCard)) —
//   i.e. delete EVERY opponent battle-area Digimon (including the reference itself, since HasSameCardName is
//   reflexive) whose top card's name overlaps the reference's name set — via
//   new DestroyPermanentsClass(destroyTargetPermanents, hashtable).Destroy().
//
//   Branch 2 — EffectTiming.OnAllyAttack (description "[When Attacking] You can unsuspend this Digimon by
//   returning 1 of this Digimon's level 6 digivolution cards to your hand."). CanUseCondition =
//   CanTriggerOnAttack(hashtable, card) [ported: CardEffectCommons.CanTriggerOnAttack]. CanActivateCondition =
//   IsExistOnBattleArea(card) && card.PermanentOfThisCard().DigivolutionCards.Count(CanSelectCardCondition) >= 1,
//   where CanSelectCardCondition(cardSource) = cardSource.IsDigimon && cardSource.Level == 6 && cardSource.HasLevel
//   [ported: CardSource.IsDigimon / .Level / .HasLevel all exist]. ORDER=-1, ISOPTIONAL=true ("you can").
//   ActivateCoroutine: SelectCardEffect(root: Custom over selectedPermanent.DigivolutionCards, mode: AddHand,
//   maxCount = Min(1, matching count), canNoSelect: () => false — mandatory pick of exactly 1 once activated)
//   THEN new IUnsuspendPermanents(new List<Permanent> { selectedPermanent }, activateClass).Unsuspend() — i.e.
//   return exactly 1 matching digivolution card from THIS card's OWN permanent (not a zone-wide search) to the
//   owner's hand, then unsuspend this Digimon.
//
// Both branches are now ported (the branch-2 primitive was added in the STOP-remainder pass):
//
// Branch 1 (WhenDigivolving): uniform ActivatedEffect + SelectBody with an onEachSelectedWithSink follow-up
// that, from the picked reference id, derives the same-named opponent set and deletes each (see the branch below).
//
// Branch 2 (OnAllyAttack): SelectDigivolutionSourceToHandThenUnsuspendSelfEffect — "select 1 predicate-matching
// digivolution card from THIS permanent's own stack -> return to hand (DigivolutionStackHelpers.PlaySpecificSourceAsync,
// destination Hand) -> self-unsuspend (sink mutation)". The "you can" optionality is a skippable pick in the
// auto-firing subject-scoped bridge. — 강모델
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.White;

using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_084 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // [When Digivolving] "Choose 1 of your opponent's Digimon. Delete all of your opponent's Digimon that
        // share a name with it." AS-IS: ActivateClass gated by CanTriggerWhenDigivolving (ported under the
        // WhenDigivolving timing per the BT1_074/ST1_08/BT1_017 idiom — the bridge routes OnEnterFieldAnyone
        // activated selects nowhere live). CanActivateCondition = IsExistOnBattleArea && HasMatchConditionPermanent
        // (opponent battle-area Digimon). ActivateCoroutine: SelectPermanentEffect(Mode.Custom, maxCount=Min(1,
        // count)) picks 1 reference, THEN destroyTargets = opponent battle-area Digimon whose TopCard.HasSameCardName
        // (reference.TopCard) -> DestroyPermanentsClass(destroyTargets).Destroy() (reflexive: includes the reference).
        // Headless mirror: uniform ActivatedEffect + SelectBody with a sink-scoped follow-up that, from the picked
        // reference id, derives the same-named opponent set (MatchConditionPermanentIds + CardNames overlap) and
        // deletes each (DestroyPermanent). The sink's immunity/deletion-prevention gates filter.
        if (timing == EffectTiming.WhenDigivolving)
        {
            bool CanSelect(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.WhenDigivolving,
                canUse: null,
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.HasMatchConditionPermanent(card, CanSelect),
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelect,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Custom,
                    description: "[When Digivolving] Choose 1 of your opponent's Digimon. Delete all of your opponent's Digimon that share a name with it.",
                    onEachSelectedWithSink: (c, sink, refId) =>
                    {
                        IReadOnlyList<string> refNames = new Permanent(c.Context, refId, c.Owner).TopCard.CardNames;
                        foreach (HeadlessEntityId id in CardEffectCommons.MatchConditionPermanentIds(
                            c, x => CardEffectCommons.IsOpponentBattleAreaDigimon(c, x)))
                        {
                            CardSource top = new Permanent(c.Context, id, c.Owner).TopCard;
                            if (refNames.Any(n => top.EqualsCardName(n)))
                            {
                                CardEffectCommons.DestroyPermanent(sink, c, id);
                            }
                        }
                    }),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[When Digivolving] Choose 1 of your opponent's Digimon. Delete all of your opponent's Digimon that share a name with it."));
        }

        // [When Attacking] "You can unsuspend this Digimon by returning 1 of this Digimon's level 6
        // digivolution cards to your hand." AS-IS: ActivateClass on OnAllyAttack, CanUseCondition =
        // CanTriggerOnAttack, CanActivateCondition = IsExistOnBattleArea && this permanent's
        // DigivolutionCards.Count(level 6 Digimon w/ HasLevel) >= 1, ORDER=-1, ISOPTIONAL=true. ActivateCoroutine:
        // SelectCardEffect(root: Custom over selectedPermanent.DigivolutionCards, mode: AddHand, maxCount:
        // Min(1, matching), canNoSelect:()=>false) THEN IUnsuspendPermanents(self).Unsuspend().
        // Headless mirror: SelectDigivolutionSourceToHandThenSelfFollowUpEffect — select 1 matching source from
        // THIS card's own stack, return it to hand (DigivolutionStackHelpers.PlaySpecificSourceAsync), then run the
        // self follow-up (here: CardEffectCommons.UnsuspendSelf). isOptional (the "you can") is modeled as a skippable pick in the auto-firing
        // subject-scoped bridge (skipping = declining to activate). Self-guards on matching-count>=1 (CanActivate).
        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanSelectCardCondition(CardSource cardSource) =>
                cardSource.IsDigimon && cardSource.Level == 6 && cardSource.HasLevel;

            cardEffects.Add(new SelectDigivolutionSourceToHandThenSelfFollowUpEffect(
                card,
                canSelect: CanSelectCardCondition,
                isOptional: true,
                onSelected: sink => CardEffectCommons.UnsuspendSelf(sink, card),
                description: "[When Attacking] You can unsuspend this Digimon by returning 1 of this Digimon's level 6 digivolution cards to your hand."));
        }

        return cardEffects;
    }
}
