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
// STOP (both branches — genuine uniform-ActivatedEffect IEffectBody / CardEffectFactory primitive gaps, not
// per-card shortcuts; grepped twice per rule 4):
//
// Branch 1: needs a body of "interactively select 1 reference permanent (Mode.Custom-shaped: mandatory pick of
// min(1, available), no further per-target narrowing beyond IsOpponentBattleAreaDigimon), THEN — using the
// SPECIFIC selected id — compute a DERIVED destroy-target set (every opponent battle-area Digimon whose card
// name overlaps the reference's) and destroy them all". Grepped (2x) the uniform IEffectBody catalog
// (Assets/Scripts/Script/CardEffectCommons/ActivatedEffect.cs): DrawBody / MemoryBody / RecoveryBody /
// TrashSecurityBody / SelectTrashHandThenSelfMutationBody / SuspendSelfAndGainMemoryBody / SelfToHandBody /
// GrantContinuousBody / SelectBody — none compute a mutation from the selected id's OWN card-name; SelectBody's
// Apply delegates entirely to SelectPermanentEffect.Mode, and Mode.Custom is EXPLICITLY a no-op in
// SelectPermanentEffect.BuildMutation ("Mode.Attack or Mode.Custom => null" — Assets/Scripts/Script/
// SelectPermanentEffect.cs:213), so it cannot emit a "destroy every same-named permanent" mutation from the pick.
// Also grepped the legacy per-shape factories (CardPortingFramework.cs): CardEffectFactory.SelectAndDestroyEffect
// only destroys the SELECTED permanent(s) themselves (SelectPermanentEffect.Mode.Destroy applied to the pick),
// not a name-matched set derived from the pick. No factory composes "select 1 reference -> destroy every
// same-CardName permanent on the opponent's side" (CardSource.EqualsCardName / CardNames already exist as query
// primitives — Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs:435 — but no activated-effect BODY
// wires a select's answer into that derived-destroy computation). Per rule 4 this is a primitive gap requiring a
// new composed IEffectBody in the shared catalog, out of scope for a single-card porting pass.
//
// Branch 2: needs a body of "interactively select 1 matching card (level==6 Digimon) from THIS card's OWN
// permanent's digivolution-card stack (not a global zone), move it to the owner's hand, THEN apply a fixed self
// mutation (Unsuspend)". Grepped (2x) the same IEffectBody catalog: SelectTrashHandThenSelfMutationBody is the
// closest shape (select+cost then fixed self-mutation follow-up) but its select source is hard-coded to the
// OWNER'S HAND via ChoiceZone.Hand, and its cost action is a TRASH (TrashCardKind), not a return-to-hand pick
// from a permanent's OWN digivolution-card stack. CardEffectFactory.SelectAndAddToHandFromZoneEffect /
// ActivatedSelectFromZoneEffect read candidates via ((IZoneStateReader)Context.ZoneMover).GetCards(owner,
// fromZone) — a flat PER-PLAYER zone dictionary (Headless/Services/InMemoryZoneMover.cs) — but a permanent's
// digivolution sources are NOT stored in that per-player zone table; they live in the host CardInstanceRecord's
// own metadata (Headless.State.DigivolutionStackReader.SourceIdsKey / CardPortingFramework.cs:7766-7771's
// SourcesOf helper), so ChoiceZone.DigivolutionCards candidates would always resolve empty through that factory
// — it cannot scope to "this specific permanent's" digivolution stack at all. CardEffectFactory.
// SelectAndTrashDigivolutionEffect / ActivatedSelectTrashDigivolutionEffect (ST2_03/06/09 shape) does read a
// permanent's own digivolution sources, but only to TRASH a count from top/bottom (no card-level predicate select,
// no hand destination, no self-mutation follow-up). Headless.Runtime.DigivolutionStackHelpers.PlaySpecificSourceAsync
// is the exact underlying mechanic (move ONE specific digivolution source to an arbitrary destination zone,
// including Hand) but — like FreeDigivolveHelpers in BT1_078's gap — it is an internal Runtime helper with no
// CardEffectFactory-level card-facing wrapper chaining it after a predicate-filtered interactive select, let alone
// with a follow-up self-Unsuspend mutation. No factory composes "select 1 predicate-matching digivolution card
// from THIS permanent's own stack -> return to hand -> self-unsuspend". Per rule 4 this is a primitive gap
// requiring a new composed IEffectBody, out of scope for a single-card porting pass.
//
// No cardEffects registered for either branch. — 강모델
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.White;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_084 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [When Digivolving] "Choose 1 of your opponent's Digimon. Delete all of your opponent's
        // Digimon that share a name with it." — needs a "select 1 reference -> destroy every same-named
        // opponent permanent" activated body that does not exist yet (see file header).
        // if (timing == EffectTiming.WhenDigivolving) { ... }

        // STOP: [When Attacking] "You can unsuspend this Digimon by returning 1 of this Digimon's level 6
        // digivolution cards to your hand." — needs a "select 1 matching card from THIS permanent's own
        // digivolution stack -> return to hand -> self-unsuspend" activated body that does not exist yet
        // (see file header).
        // if (timing == EffectTiming.OnAllyAttack) { ... }

        return cardEffects;
    }
}
