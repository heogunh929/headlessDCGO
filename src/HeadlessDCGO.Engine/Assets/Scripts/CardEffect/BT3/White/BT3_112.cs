// Source: Assets/Scripts/CardEffect/BT3/White/BT3_112.cs (a White Digimon, two ActivateClass branches)
// AS-IS:
//   Branch 1 — declared under EffectTiming.OnEnterFieldAnyone but CanUseCondition =
//   CanTriggerWhenDigivolving(hashtable, card) (description "[When Digivolving] Trigger <De-Digivolve 1> on all
//   of your opponent's Digimon. ... Then, delete all of your opponent's Digimon with 5000 DP or less."): per the
//   established headless idiom (BT1_084/BT1_086/EX8_074 precedent — the bridge routes activated selects declared
//   under OnEnterFieldAnyone nowhere live when the AS-IS gate is really CanTriggerWhenDigivolving), this is
//   mirrored under the dedicated WhenDigivolving branch instead. CanActivateCondition = IsExistOnBattleArea(card)
//   && card.Owner.Enemy.GetBattleAreaDigimons().Count >= 1. ORDER=-1, ISOPTIONAL=false. ActivateCoroutine:
//   (1) foreach opponent battle-area Digimon (unconditional, no selection): yield return
//   IDegeneration(permanent, 1, activateClass).Degeneration() — De-Digivolve 1 each (trash the top digivolution
//   card; stops early per-permanent if it has no digivolution cards left or would regress to level 3), gated
//   inline by !permanent.TopCard.CanNotBeAffected(activateClass); (2) AFTER every de-digivolve coroutine above has
//   run to completion (sequential yield return, so this reads POST-de-digivolve state): destroyTargetPermanents =
//   card.Owner.Enemy.GetBattleAreaDigimons().Where(p => p.DP <= card.Owner.MaxDP_DeleteEffect(5000, activateClass)
//   && p.CanBeDestroyedBySkill(activateClass) && !p.TopCard.CanNotBeAffected(activateClass)).ToList(), then
//   new DestroyPermanentsClass(destroyTargetPermanents, hashtable).Destroy().
//
//   Branch 2 — EffectTiming.OnAllyAttack (description "[When Attacking] You may make this Digimon unblockable
//   for the turn by returning one of its level 6 digivolution cards to your hand."). CanUseCondition =
//   CanTriggerOnAttack(hashtable, card) [ported: CardEffectCommons.CanTriggerOnAttack]. CanActivateCondition =
//   IsExistOnBattleArea(card) && card.PermanentOfThisCard().DigivolutionCards.Count(CanSelectCardCondition) >= 1,
//   where CanSelectCardCondition(cardSource) = cardSource.IsDigimon && cardSource.Level == 6 && cardSource.Owner
//   == card.Owner && cardSource.HasLevel [ported: CardSource.IsDigimon / .Level / .Owner / .HasLevel all exist].
//   ORDER=-1, ISOPTIONAL=true ("you may"). ActivateCoroutine: SelectCardEffect(root: Custom over
//   card.PermanentOfThisCard().DigivolutionCards, mode: AddHand, maxCount = Min(1, matching count), canNoSelect:
//   () => false — mandatory pick of exactly 1 once activated, isShowOpponent:true, canLookReverseCard:true) THEN
//   CardEffectCommons.GainCanNotBeBlocked(targetPermanent: this permanent, defenderCondition: null,
//   effectDuration: UntilEachTurnEnd, activateClass, effectName: "Unblockable") — i.e. return exactly 1 matching
//   level-6 digivolution card from THIS card's OWN permanent (not a zone-wide search) to the owner's hand, then
//   grant this Digimon <Unblockable> for the turn.
//
// STOP (both branches — genuine uniform-ActivatedEffect IEffectBody primitive gaps, not per-card shortcuts;
// grepped 2x per rule 4):
//
// Branch 1: needs a body of "apply De-Digivolve 1 (no selection) to EVERY opponent battle-area Digimon, THEN —
// using the RESULTING post-de-digivolve state — compute a derived destroy-target set (DP <=
// MaxDpDeleteThreshold(5000)) and destroy them all". Grepped (2x) the uniform IEffectBody catalog
// (Assets/Scripts/Script/CardEffectCommons/ActivatedEffect.cs): ApplyToAllMatchingBody is the no-select
// apply-to-all shape (used by BT1_101/BT1_110 for exactly this kind of unconditional foreach), and it could stage
// the De-Digivolve step (DeDigivolveKind mutation, mirroring ActivatedSelectAndDeDigivolveEffect at
// CardPortingFramework.cs:3395) — but ActivatedEffectResolver.ResolveAsync (:48-76) constructs ONE
// MatchStateMutationSink shared by every registered effect for the timing and calls sink.FlushAsync EXACTLY ONCE,
// after ALL of them have resolved (:74). De-Digivolve mutations are staged into the sink's deferred async queue
// (MatchStateMutationSink.cs:393-400, DeDigivolveHelpers.DeDigivolveAsync only actually runs during that single
// end-of-resolution flush) — so a second effect (or a second predicate evaluated later in the SAME synchronous
// Apply()) that reads live DP via CardEffectCommons.CurrentDp would still see the PRE-de-digivolve values, not the
// AS-IS's genuinely sequential (yield-return-awaited) post-de-digivolve state. No IEffectBody in the catalog
// supports "stage mutation A, actually COMMIT it, then re-query state for mutation B" within one activation — the
// closest composites (SuspendSelfCostThenBody, MemoryCostThenUnsuspendSelfBody, MemoryGainThenScheduledReversalBody)
// all chain FIXED follow-up mutations, none re-evaluates a live predicate against post-mutation state. Also
// grepped the legacy per-shape factories (CardPortingFramework.cs): DestroyPermanentsEffect (:3361) takes a
// pre-computed target list at CONSTRUCTION time (no live re-query hook either). Computing the destroy list from
// the PRE-de-digivolve DP would silently change which Digimon get deleted (a materially different, weaker
// outcome than AS-IS) — an approximation the porting rules forbid. Per rule 4 this is a primitive gap requiring a
// new composed IEffectBody (a "flush-then-requery" sequencing primitive) in the shared catalog, out of scope for
// a single-card porting pass.
//
// Branch 2: needs a body of "interactively select 1 matching card (level==6 Digimon) from THIS card's OWN
// permanent's digivolution-card stack (not a global zone), move it to the owner's hand, THEN grant this permanent
// <Unblockable> for the turn". This is the SAME documented primitive gap as BT1_084's OnAllyAttack branch
// (Assets/Scripts/CardEffect/BT1/White/BT1_084.cs:53-73, grepped there 2x already): SelectTrashHandThenSelfMutationBody
// is the closest shape (select+cost then fixed self-mutation follow-up) but its select source is hard-coded to
// the OWNER'S HAND via ChoiceZone.Hand and its cost action is a TRASH, not a return-to-hand pick from a
// permanent's OWN digivolution-card stack. CardEffectFactory.SelectAndAddToHandFromZoneEffect /
// ActivatedSelectFromZoneEffect read candidates via a flat PER-PLAYER zone dictionary
// (Headless/Services/InMemoryZoneMover.cs) — a permanent's digivolution sources are NOT stored there; they live
// in the host CardInstanceRecord's own metadata (DigivolutionStackReader.SourceIdsKey), so those factories cannot
// scope to "this specific permanent's" digivolution stack at all. Headless.Runtime.DigivolutionStackHelpers
// exposes the underlying "move ONE specific digivolution source to an arbitrary destination zone" mechanic but —
// as BT1_084 documents — it is an internal Runtime helper with no CardEffectFactory-level card-facing wrapper
// chaining it after a predicate-filtered interactive select, let alone with a GainCanNotBeBlocked follow-up. No
// factory composes "select 1 predicate-matching digivolution card from THIS permanent's own stack -> return to
// hand -> grant self Unblockable". Per rule 4 this is the same primitive gap as BT1_084, out of scope for a
// single-card porting pass.
//
// No cardEffects registered for either branch. — 강모델
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.White;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_112 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [When Digivolving] "Trigger <De-Digivolve 1> on all of your opponent's Digimon. Then, delete all
        // of your opponent's Digimon with 5000 DP or less." — needs a "no-select apply-to-all mutation, THEN
        // requery live state for a second derived apply-to-all mutation" activated body that does not exist yet
        // (see file header).
        // if (timing == EffectTiming.WhenDigivolving) { ... }

        // STOP: [When Attacking] "You may make this Digimon unblockable for the turn by returning one of its
        // level 6 digivolution cards to your hand." — needs a "select 1 matching card from THIS permanent's own
        // digivolution stack -> return to hand -> grant self Unblockable" activated body that does not exist yet
        // (same gap as BT1_084's OnAllyAttack branch; see file header).
        // if (timing == EffectTiming.OnAllyAttack) { ... }

        return cardEffects;
    }
}
