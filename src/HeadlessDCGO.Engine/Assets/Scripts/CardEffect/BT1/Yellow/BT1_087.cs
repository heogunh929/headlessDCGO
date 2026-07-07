// 1:1 mirror of the original BT1_087 (Yellow Tamer, mixed):
//   [Start of Your Turn] Set your memory to 3 (if 2 or less).      -> SetMemoryTo3TamerEffect
//   [On Play] Look at your security stack, then reveal 1 card in it and add it to your hand. If that card
//             is yellow, <Recovery +1 (Deck)>. (Place the top card of your deck on top of your security
//             stack.) Then shuffle your security stack.
//   [Security] Play this Tamer.                                     -> PlaySelfTamerSecurityEffect
//
// STOP (partial): the [On Play] activated effect is NOT registered. AS-IS ActivateCoroutine is one atomic
// sequence — select 1 card out of security to hand, THEN conditionally <Recovery +1> keyed off the COLOR
// OF THE SPECIFIC CARD JUST SELECTED, THEN unconditionally shuffle the remaining security stack — and two
// of its three steps have no headless primitive:
//   1) Conditional follow-up gated on a property of the just-selected card. CardEffectFactory's
//      select-from-zone primitive (SelectAndAddToHandFromZoneEffect / ActivatedSelectFromZoneEffect,
//      CardPortingFramework.cs:5476/4150) applies one fixed MatchStateMutationSink kind to every chosen id
//      and has no post-selection hook; SimplifiedRevealAndSelectEffect's per-condition routing
//      (CardPortingFramework.cs:3000) is Library-only and routes by a target predicate known BEFORE
//      selection, not a "then do X to the same selected card" step. grep (2x) for
//      afterSelect/AfterSelect/OnSelected/PostSelect/SelectedCardCondition across Headless/* and
//      Assets/Scripts/Script/* found no reusable hook outside of unrelated digivolution-trash and
//      DNA-digivolve helpers.
//   2) Shuffling the security stack. MatchStateMutationSink has no AddToSecurity/Recover-adjacent shuffle
//      kind (grep of "Shuffle"/"Kind = \"" across CardPortingFramework.cs and Headless/* — the only shuffle
//      primitive is IZoneMover.ShuffleAsync / ZoneState.Shuffle, hard-wired to ChoiceZone.Library only;
//      InMemoryZoneMover.ShuffleAsync explicitly shuffles `GetZone(playerId, ChoiceZone.Library)`).
// Per the primitive-gap rule (no new primitives, no engine edits, no throw), this timing is left
// unregistered rather than dropping the conditional-recovery/shuffle semantics or approximating them.
// [Start of Your Turn] and [Security] are separate, fully-covered blocks and are ported below.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_087 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
        }

        // STOP: [On Play] (OnEnterFieldAnyone) select-from-security + conditional-color-gated recovery +
        // shuffle-security — see file-header STOP note. Not registered (primitive gap, no engine mod).

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
