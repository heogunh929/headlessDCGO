// Source: Assets/Scripts/CardEffect/BT3/Purple/BT3_006.cs
// STOP: [On Deletion] Trigger <Draw 1>. (Draw 1 card from your deck.) Then trash 1 card from your hand.
//
// AS-IS ActivateCoroutine is ONE atomic sequence: Draw(1) THEN (if HandCards.Count>=1) a MANDATORY
// (canNoSelect:false) discard of exactly 1 card from the RESULTING hand — i.e. the just-drawn card is a
// legal discard target.
//
// Headless has no composite "draw N, then interactively discard M from the POST-draw hand" IEffectBody
// (ActivatedEffect.cs's DrawBody / SelectTrashHandThenSelfMutationBody / TrashSecurityBody etc. are each
// single-purpose; none sequence a draw before an interactive hand-select). Decomposing into two
// independently-registered effects under the same timing (DrawCardsEffect + SelectAndTrashFromZoneEffect
// over ChoiceZone.Hand) is NOT faithful here: ActivatedEffectResolver.ResolveAsync materializes the whole
// CardEffects() list ONCE, resolves every entry against the SAME MatchStateMutationSink, and flushes that
// sink only ONCE at the very end (CardPortingFramework.cs ActivatedEffectResolver.ResolveAsync — `await
// sink.FlushAsync()` runs AFTER the foreach over cardEffects completes). The Draw mutation itself is
// deferred (`_pendingAsync.Add(ct => zoneMover.DrawAsync(...))` in MatchStateMutationSink.ApplyDraw) — it
// does not take effect until that final flush. So a second, separately-registered discard-from-hand
// effect resolved in the SAME pass would build its candidate pool from the PRE-draw hand (via
// `((IZoneStateReader)Card.Context.ZoneMover).GetCards(...)`, which reads persisted state), silently
// excluding the just-drawn card from the discard choice — a real behavioral divergence from AS-IS (which
// allows discarding the drawn card), not merely an approximation of a boundary count. No primitive
// exists that sequences an atomic draw-then-discard-from-resulting-hand; per the primitive-gap rule (no
// new primitives, no engine edits, no throw), this card is left unregistered rather than shipping the
// discard with a stale candidate pool.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_006 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        // STOP: [On Deletion] Draw 1 then mandatorily discard 1 from the resulting hand — see file-header
        // STOP note (no atomic draw-then-discard primitive; splitting causes pre-flush candidate-pool
        // staleness). Not registered.
        return new List<ICardEffect>();
    }
}
