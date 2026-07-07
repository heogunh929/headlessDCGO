// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_056.cs
//   [On Play] You may play 1 [Tinkermon] from your hand or recycle bin without paying its memory cost.
// AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone.
//   CanUseCondition = CanTriggerOnPlay. CanActivateCondition = IsExistOnBattleArea(card) &&
//   (owner's hand has >=1 card OR HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition)),
//   where CanSelectCardCondition(cardSource) = cardSource.CardNames.Contains("Tinkermon") &&
//   CanPlayAsNewPermanent(cardSource, payCost:false, activateClass). ORDER=-1, ISOPTIONAL=true.
//   ActivateCoroutine: computes canSelectHand / canSelectTrash (both filtered by CanSelectCardCondition);
//   if both, asks the owner to choose a ROOT ZONE first (SetBoolSelection: "From hand" / "From trash");
//   if only one, defaults to it. THEN runs a zone-scoped select (SelectHandEffect Mode.Custom over Hand, or
//   SelectCardEffect Mode.Custom over Root.Trash) to pick 1 matching card, collecting it via
//   SelectCardCoroutine into `selectedCards` (no zone-move on selection itself). Finally calls
//   CardEffectCommons.PlayPermanentCards(selectedCards, activateClass, payCost:false, isTapped:false,
//   root: fromHand ? Hand : Trash, activateETB:true) — plays the chosen card as a new permanent, cost-free.
//
// STOP: no headless primitive supports a SINGLE select whose CANDIDATE POOL spans two different zones
// (Hand union Trash) while still tracking each candidate's own origin zone through to the play mutation.
// `CardEffectFactory.SelectAndPlayFromZoneEffect` / `ActivatedSelectAndPlayEffect` (PRIM-W5, mirrors
// `CardEffectCommons.PlayPermanentCards(.., root)`) takes exactly ONE fixed `ChoiceZone fromZone` — its
// `Candidates()` reads only that zone, and `Apply(..)` stamps every selected id with that SAME `_fromZone`
// in the play mutation's `FromZoneKey`. Approximating this by picking only Hand (or only Trash) would
// silently drop a legal source zone (a fidelity dilution — forbidden by rule 2/4), and there is no
// existing "zone-per-candidate" select primitive to compose instead (grepped
// `SelectAndPlayFromZoneEffect`/`ActivatedSelectAndPlayEffect`/`ChoiceZone` combinators in
// CardPortingFramework.cs — none support a multi-zone candidate pool). This is the same STOP family as
// the AS-IS-catalogued `DNADigivolveWithHandOrTrashCardIntoHandOrTrash` (docs/porting/PORTING-RECIPE.md
// STOP-list): a "hand-or-trash" combined source select. Needs a new primitive (e.g. an
// `ActivatedSelectAndPlayFromZonesEffect` that unions candidates from multiple `ChoiceZone`s, tagging each
// with its own origin zone for the play mutation) — engine dev, not this pass. No cardEffects registered
// for this card (single-branch card; the only timing is fully blocked).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_056 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        // STOP: [On Play] play 1 [Tinkermon] from hand OR trash (cost-free) — needs a multi-zone
        // (Hand union Trash) select-and-play primitive that does not exist yet. See file header. — 강모델

        return cardEffects;
    }
}
