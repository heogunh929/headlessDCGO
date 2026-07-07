// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_078.cs
// 1:1 mirror of the original BT1_078 (BT1/Green).
//   [When Attacking] Reveal 3 cards from the top of your deck. You can digivolve this card into 1 level 6
//   green Digimon card among them without paying its memory cost. Place the remaining cards at the bottom
//   of your deck in any order.
//   -> STOP (see below).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_078 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [When Attacking] "Reveal 3 cards from the top of your deck. You can digivolve this card
        // into 1 level 6 green Digimon card among them without paying its memory cost. Place the remaining
        // cards at the bottom of your deck in any order."
        // AS-IS: ActivateClass on OnAllyAttack, CanUseCondition = CanTriggerOnAttack, CanActivateCondition =
        // IsExistOnBattleArea && Owner.LibraryCards.Count >= 1, ORDER=-1, ISOPTIONAL=false. ActivateCoroutine =
        // CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect(revealCount:3, one SimplifiedSelectCardCondition
        // with mode:Custom [level==6, HasCardColor(Green), CanPlayCardTargetFrame(this card's own PermanentFrame,
        // payCost:false, activateClass), HasLevel], maxCount:1, remainingCardsPlace:DeckBottom, canNoSelect:true)
        // THEN, if a card was Custom-selected, new PlayCardClass(selectedCard, payCost:false,
        // targetPermanent:card.PermanentOfThisCard(), root:Library, activateETB:true).PlayCard() — i.e. digivolve
        // the reveal-selected library card onto THIS card's own battle-area permanent for free.
        //
        // Reveal + select-with-no-move IS covered: CardEffectFactory.RevealDeckTopCardsAndSelect(..., new
        // RevealSelectPass(condition, maxCount:1, RevealDestination.Custom, message, canNoSelect:true), ...) ->
        // RevealMultiSelectEffect (its CustomSelections is documented as "the card script's follow-up (e.g. 'play
        // it, ignoring its play cost') consumes them after resolution").
        //
        // But the FOLLOW-UP — digivolve that specific already-known CustomSelections[0] card onto a FIXED target
        // (this card's own permanent, no further selection) for free — has no card-facing factory:
        //   - PlayCardEffect (BT-PRE-A5, its own doc comment names BT1_078 as the motivating case) stages a plain
        //     PlayCard mutation -> MatchStateMutationSink.ApplyPlayCard, which only moves the card onto an EMPTY
        //     battle-area slot (ZoneMoveRequest ... -> ChoiceZone.BattleArea); it does not fold the card onto an
        //     existing permanent as a digivolution (no targetPermanent branch — "NOT modeled here ... until a
        //     card needs it").
        //   - SelectAndDigivolveEffect opens its OWN two ChoiceProvider selections (target THEN source-zone card)
        //     — it cannot consume a source id already fixed by a prior reveal-custom pass, and its target is a
        //     fresh open selection, not this card's permanent.
        //   - ArtsDigivolveSelfEffect is the mirror shape (source = THIS card, target = an open selection) — the
        //     inverse of what's needed (source = the revealed card, target = fixed self).
        //   - Headless.Runtime.FreeDigivolveHelpers.DigivolveFreeAsync(repository, zoneMover, cardId, targetCardId,
        //     fromZone, gameEventQueue) is the exact underlying mechanic (costless single-target digivolve, used
        //     by Blast/Arts Digivolve's SpecialPlayAction auto-match), but it is an internal Runtime helper with
        //     no CardEffectFactory-level card-facing wrapper for "digivolve a given known source onto a fixed
        //     target, free" chained after a reveal-select step.
        // Grepped PRIMITIVE-CATALOG.md (Digivolve* / RevealDeckTopCardsAndSelect / PlayCard* entries) and
        // ActivatedEffectResolver.cs's case list twice — no factory composes "reveal-custom-select" -> "free
        // digivolve the selection onto self" in one chained activation. Registering only the reveal+select+
        // remaining-to-bottom half (without the digivolve) would leave the matched card stranded in the library
        // instead of being played, which is not a faithful partial mirror — so the whole timing is left
        // unregistered pending a new IEffectBody / factory (engine-side primitive development, deferred to the
        // strong-model primitive pass). — 강모델
        // if (timing == EffectTiming.OnAllyAttack) { ... }

        return cardEffects;
    }
}
