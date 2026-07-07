// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_110.cs — an Option.
//   [Main]     Suspend 1 of your opponent's Digimon.
//   [Security] Suspend all of your opponent's Digimon without <Blocker>.
// AS-IS [Main] (OptionSkill): ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1,
// ISOPTIONAL=false). ActivateCoroutine: guarded by HasMatchConditionPermanent(CanSelectPermanentCondition)
// (CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon), maxCount =
// Math.Min(1, MatchConditionPermanentCount(...)), then a SelectPermanentEffect(Mode.Tap) (canNoSelect:false,
// canEndNotMax:false) over opponent battle-area Digimon.
// Headless mirror: CardEffectFactory.SelectAndSuspendEffect (AS-IS SelectPermanentEffect Mode.Tap), maxCount:1
// — same shape as ST4_15's [Main] ("Suspend 1 of your opponent's Digimon."). CanTriggerOptionMainEffect is
// subsumed by the OptionSkill activation gate (ST1_13/ST1_15/BT1_090/BT1_092/BT1_094/ST4_15 precedent);
// HasMatchConditionPermanent + Min(1,count) are subsumed by SelectAndSuspendEffect's own "select up to
// maxCount matching permanents" behaviour (no-op / BuildRequest clamp when nothing matches, same precedent).
//
// AS-IS [Security] (SecuritySkill): ActivateClass(CanUseCondition = CanTriggerSecurityEffect, ORDER=-1,
// ISOPTIONAL=false, IsSecurityEffect=true). ActivateCoroutine has NO SelectPermanentEffect step at all: it
// computes `card.Owner.Enemy.GetBattleAreaDigimons().Filter(PermanentCondition)` — every opponent
// battle-area Digimon that (a) IsPermanentExistsOnOpponentBattleAreaDigimon, (b) `!permanent.HasBlocker`, and
// (c) `!permanent.TopCard.CanNotBeAffected(activateClass)` — then directly runs
// `new SuspendPermanentsClass(suspendTargetPermanents, ...).Tap()` on that whole precomputed list, with zero
// player choice (unlike [Main], which does select).
// STOP: [Security] needs a body that unconditionally Suspends EVERY currently-matching opponent permanent
// with no player-facing selection step — the same "apply-to-all-matching, no select" shape already flagged
// as a genuine primitive gap for BT1_100 (player-scope restriction grant) and BT1_101 (trash-all
// digivolution-cards). Grepped (2 passes: CardPortingFramework.cs class/factory list, then
// ActivatedEffectResolver.cs's switch) for a no-select "apply mutation to a precomputed target list" shape:
//   - CardEffectFactory.SelectAndSuspendEffect / ActivatedSelectEffect(Mode.Tap) (used for [Main] above):
//     ALWAYS routes through SelectPermanentEffect's BuildRequest -> player answer flow (a live ChoiceRequest);
//     even with maxCount set to the full opponent battle-area count, the player still gets an interactive
//     Tap-mode selection UI, which is a materially different (choice-bearing) exposure than the AS-IS
//     zero-agency automatic foreach-Suspend (fidelity-over-coverage — not a faithful mirror).
//   - DestroyPermanentsEffect / DeckBottomBounceEffect (CardPortingFramework.cs ~3361/~3400): DO take a
//     pre-computed `IReadOnlyList<HeadlessEntityId> targets` with NO selection step (exactly this card's
//     [Security] shape) and apply directly via MatchStateMutationSink — but each hardcodes its own mutation
//     kind (DeleteKind / ReturnToDeckBottomKind respectively) in its constructor body; neither is
//     parameterized by kind, and there is no sibling class wired for MatchStateMutationSink.SuspendKind.
//     DestroyPermanentsEffect is also only exercised by TfxDestroy (a test fixture), not by any shipped card,
//     confirming this "precomputed-list, no-select" family has not yet been extended to Suspend.
//   - The uniform IEffectBody catalog (ActivatedEffect.cs: DrawBody, MemoryBody, RecoveryBody,
//     TrashSecurityBody, SelectTrashHandThenSelfMutationBody, SuspendSelfAndGainMemoryBody (self-suspend, not
//     opponent-scope), SelfToHandBody, GrantContinuousBody, SelectBody): none apply Suspend to every matching
//     opponent permanent unconditionally.
// Confirmed gap, not a naming miss: no reusable body/class composes "Suspend every id matching a predicate,
// no selection" (a "SuspendPermanentsEffect" sibling of DestroyPermanentsEffect/DeckBottomBounceEffect wired
// to SuspendKind) — adding one is new engine-catalog work out of scope for a single-card porting pass, not a
// per-card shortcut to invent. Deferred to the strong-model primitive pass. — 강모델
// [Security] left unregistered pending that primitive; [Main] is ported below (fully expressible today).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_110 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndSuspendEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: "[Main] Suspend 1 of your opponent's Digimon."));
        }

        // STOP: [Security] "Suspend all of your opponent's Digimon without <Blocker>." (SecuritySkill) — no
        // no-select "apply Suspend to every matching permanent" primitive exists (see file header).
        // if (timing == EffectTiming.SecuritySkill) { ... }

        return cardEffects;
    }
}
