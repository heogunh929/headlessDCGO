// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_101.cs (an Option, ref BT1_043's shape).
// AS-IS has two branches:
//   [Main] Trash all digivolution cards under all of your opponent's Digimon. (OptionSkill)
//     -> ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false).
//        ActivateCoroutine: a PLAIN `foreach (Permanent selectedPermanent in card.Owner.Enemy
//        .GetBattleAreaDigimons())` — every one of the opponent's battle-area Digimon, unconditionally, with
//        NO CanSelectPermanentCondition and NO player-facing selection step at all — then for each,
//        `TrashDigivolutionCardsFromTopOrBottom(trashCount: selectedPermanent.DigivolutionCards.Count,
//        isFromTop: true)`, i.e. trash every digivolution card under every opponent Digimon.
//   [Security] (use the Main effect) -> AddActivateMainOptionSecurityEffect. Depends entirely on [Main] being
//     registered under OptionSkill (ReuseMainOptionEffect re-resolves this card's CardEffects(OptionSkill)).
//
// STOP: [Main] needs a body that unconditionally applies "trash all digivolution cards" across EVERY matching
// opponent permanent with zero player choice — distinct from every existing digivolution-trash shape:
//   - CardEffectFactory.SelectAndTrashDigivolutionEffect / ActivatedSelectTrashDigivolutionEffect (used by
//     ST2_03/06/09, BT1_099): a SelectPermanentEffect(Mode.Custom) pick of up to maxCount targets. BT1_099
//     shows "trash them ALL" under a target is expressible (trashCount: int.MaxValue, clamped by
//     DigivolutionStackHelpers), but that factory always routes through a live selection: for maxCount==1 the
//     pick is mandatory-single (canEndNotMax:false), but for maxCount>1 it hard-codes
//     `canEndNotMax: maxCount > 1` — i.e. an OPTIONAL "select up to N, may stop early" pick. There is no way
//     to force "select every legal target, no choice" through this factory; setting maxCount to the opponent's
//     full battle-area count would still let the acting player choose fewer/which ones, which is a materially
//     different (weaker, choice-bearing) effect than the AS-IS unconditional all-of-them trash.
//   - CardEffectCommons.TrashDigivolutionCardsFromTopOrBottom / TrashDigivolutionCardsAndProcessAccordingToResult
//     (CardPortingFramework.cs): raw per-permanent trash helpers (used internally, e.g. by BT1_099/BT1_086's
//     write-ups) — they trash under ONE given permanent; nothing wraps a foreach-every-opponent-permanent
//     loop around them for direct card use, and composing that loop here would mean writing new IEffectBody /
//     IActivatedCardEffect logic in this per-card file — exactly the new-primitive authoring this pass forbids.
//   - The uniform IEffectBody catalog (Assets/Scripts/Script/CardEffectCommons/ActivatedEffect.cs): DrawBody,
//     MemoryBody, RecoveryBody, TrashSecurityBody, SelectTrashHandThenSelfMutationBody,
//     SuspendSelfAndGainMemoryBody, SelfToHandBody, GrantContinuousBody, SelectBody — none apply a mutation to
//     every matching permanent unconditionally (each targets either the card's own permanent/player, one
//     already-selected permanent, or a hand-selected card).
// Grepped both catalogs (CardPortingFramework.cs factory list + ActivatedEffect.cs body list) twice: confirmed
// gap, not a naming miss. Per rule 4 this is a genuine primitive gap (no reusable "unconditional apply-to-all-
// matching-permanents" body/factory exists) — fixing it means adding a new composed IEffectBody to the shared
// engine catalog, which is engine-file work out of scope for a single-card porting pass, not a per-card
// shortcut to invent.
// [Security] is left unregistered too: AddActivateMainOptionSecurityEffect's ReuseMainOptionEffect works by
// re-resolving this same card's CardEffects(OptionSkill) at trigger time — with [Main] unregistered there is
// nothing for it to reuse, so registering a bare ReuseMainOptionEffect here would silently no-op at runtime
// instead of faithfully "trash all digivolution cards under all of your opponent's Digimon" from Security —
// not a faithful partial mirror (fidelity-over-coverage), so it stays out alongside [Main].
// No cardEffects registered for this card (both OptionSkill and SecuritySkill), pending a new IEffectBody
// (e.g. an "apply body to every id matching a predicate, no selection" wrapper) — deferred to the
// strong-model primitive pass. — 강모델
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_101 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        // STOP: [Main] "Trash all digivolution cards under all of your opponent's Digimon." (OptionSkill) —
        // no factory/body applies a mutation unconditionally across every matching opponent permanent with
        // no selection step (see file header for the full gap writeup).
        // if (timing == EffectTiming.OptionSkill) { ... }

        // STOP: [Security] (reuse [Main]) — depends on [Main] being registered; see file header.
        // if (timing == EffectTiming.SecuritySkill) { ... }

        return cardEffects;
    }
}
