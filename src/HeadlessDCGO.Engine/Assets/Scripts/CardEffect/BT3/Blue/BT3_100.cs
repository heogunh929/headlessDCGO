// Source: Assets/Scripts/CardEffect/BT3/Blue/BT3_100.cs — an Option.
//
// STOP (timing == EffectTiming.OptionSkill): "[Main] Trash up to 2 digivolution cards from the bottom of ALL
// of your opponent's Digimon. Then, if you have a green Digimon in play, suspend 1 of your opponent's
// Digimon with no digivolution cards." AS-IS ActivateCoroutine: (1) compute the set of qualifying opponent
// battle-area Digimon (>=1 trashable bottom digivolution card, not immune); (2) SelectCountEffect — the
// active player interactively picks ONE shared count 0..2; (3) for EVERY permanent from step 1 (no further
// player choice), trash Min(chosenCount, thatPermanent's own DigivolutionCards.Count) cards from its bottom
// — i.e. ONE player-chosen scalar applied uniformly across an entire matched set; (4) conditionally (owner
// has a green Digimon in play), SelectPermanentEffect(Mode.Tap) picks 1 opponent Digimon with zero
// digivolution cards and suspends it.
//
// No headless primitive composes "player picks ONE count via an interactive choice, then that SAME chosen
// count is applied (via Math.Min per host) to EVERY matching permanent". Grepped (2x): ApplyToAllMatchingBody
// (ActivatedEffect.cs:414) is the no-select "apply a mutation to every matching permanent" body used by
// BT1_101/BT1_110/BT3_031 above, but it is entirely non-interactive (IsInteractive=>false, BuildRequest=>null)
// — its per-target action is a fixed compile-time closure with no way to thread a single runtime-selected
// scalar shared across every target. SelectBody (ActivatedEffect.cs:449) is interactive but selects
// PERMANENTS (maxCount targets), not an arbitrary INTEGER value — there is no "select a count" IEffectBody or
// factory anywhere in CardPortingFramework.cs (grepped "SelectCount"/"CountChoice"/"class.*Count.*Body" — zero
// hits). Building a "choose a shared count, then Math.Min-clamp-and-apply per matched host" body is new
// engine-file work (a new IEffectBody + ActivatedEffectResolver dispatch arm), out of scope for a per-card
// porting pass and forbidden by the porting rules (no new primitives during card porting). This is the SAME
// timing block as the conditional trailing "suspend 1 Digimon with no digivolution cards" step (both are
// sequential steps of the ONE AS-IS ActivateClass coroutine for OptionSkill) — the two cannot be split
// across separate `if (timing == ...)` registrations without breaking the atomic "trash-then-suspend" AS-IS
// sequencing, so the whole OptionSkill branch is STOP. Per rule 4 this is a genuine primitive gap. No
// cardEffects registered for OptionSkill. — 강모델
// if (timing == EffectTiming.OptionSkill) { ... }
//
// STOP (timing == EffectTiming.SecuritySkill): AS-IS reuses the [Main] effect verbatim
// (AddActivateMainOptionSecurityEffect -> ReuseMainOptionEffect re-resolves this card's own OptionSkill
// CardEffects live at security-check time). Since [Main] above has no cardEffects registered (the genuine
// primitive gap just described), reusing it would silently resolve to a no-op — under-delivering the AS-IS
// "trash up to 2 digivolution cards from all opponent Digimon + conditional suspend" security effect rather
// than faithfully representing it. Registering a [Security] wrapper around empty content would misrepresent
// the card's behaviour, so this branch is also left unregistered pending the same primitive-gap resolution
// as [Main]. No cardEffects registered for SecuritySkill. — 강모델
// if (timing == EffectTiming.SecuritySkill) { ... }
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_100 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        return cardEffects;
    }
}
