namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// STOP: AS-IS is [When Attacking][Twice Per Turn] "You can unsuspend this Digimon by trashing 3 cards in
// your hand." (DCGO/Assets/Scripts/CardEffect/BT1/Blue/BT1_039.cs) — CanActivateCondition gates on
// `card.Owner.HandCards.Count >= 3`, then ActivateCoroutine opens a SelectHandEffect (mode: Discard,
// maxCount = Min(3, HandCards.Count), canNoSelect: false) against the OWNER'S OWN HAND, and only after that
// select resolves does it unsuspend this permanent via IUnsuspendPermanents. The interactive "select N cards
// from your own hand to discard as a cost" primitive does not exist headless-side: grepped
// CardPortingFramework.cs twice (`SelectHandEffect`/`HandCards`/`Mode.Discard`/`DiscardHandCards`) — the only
// HandCards hit is the read-only `IsExistOnHand` predicate (line ~8995); every ActivatedSelect*/SelectAnd*
// factory (ActivatedSelectEffect, SelectAndBounceEffect, etc.) wraps SelectPermanentEffect, which enumerates
// BATTLE-AREA PERMANENTS, not hand cards — there is no hand-card ChoiceRequest/mutation shape to select-then-
// discard from one's own hand. UnsuspendSelfTriggerEffect (see ST2_11) covers the unconditional
// "[When Attacking] unsuspend this Digimon" shape but has no cost/gate hook to wire this card's discard-3
// prerequisite through, and grafting the cost onto it via a hand-hack (e.g. mutating HandCards directly
// outside a Select primitive) would silently drop the AS-IS player-choice-of-which-3-cards semantics — not a
// faithful port. Per porting rule 4, no new primitive added here (would require an engine-side hand-card
// select/discard primitive, out of scope for card-porting); effect left unregistered pending that primitive.
public sealed class BT1_039 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        return cardEffects;
    }
}
