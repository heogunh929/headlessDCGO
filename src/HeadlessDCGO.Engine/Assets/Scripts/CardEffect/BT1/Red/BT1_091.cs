// Source: Assets/Scripts/CardEffect/BT1/Red/BT1_091.cs
// 1:1 mirror of the original BT1_091 (BT1/Red) — an Option.
//   [Main] "1 of your Digimon gains <Piercing> ... for the turn." No [Security] block in AS-IS
//   (only `if (timing == EffectTiming.OptionSkill)` is defined).
// AS-IS (OptionSkill): ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false).
// ActivateCoroutine (gated on HasMatchConditionPermanent(own battle-area Digimon)): builds a
// SelectPermanentEffect (Mode.Custom, maxCount = min(1, matchCount), canNoSelect:false, canEndNotMax:false)
// over the owner's own battle-area Digimon; its SelectPermanentCoroutine callback then runs
// `CardEffectCommons.GainPierce(targetPermanent: <the ONE selected permanent>, EffectDuration.UntilEachTurnEnd,
// activateClass)` — i.e. an interactive single-target pick whose result feeds a keyword grant scoped to
// exactly that selection, lasting only until this turn ends.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_091 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [Main] "Select 1 of your (battle-area) Digimon; it gains <Piercing> for the turn." (OptionSkill).
        // Pieces confirmed to exist individually (grepped ActivatedEffect.cs [every IEffectBody impl] and
        // docs/porting/PRIMITIVE-CATALOG.md twice):
        //   - Interactive single-permanent selection over the owner's own battle area exists: SelectBody
        //     (ActivatedEffect.cs:278) wraps SelectPermanentEffect exactly like the AS-IS SelectPermanentEffect
        //     call here — BUT SelectPermanentEffect.BuildMutation (SelectPermanentEffect.cs:187-216) maps
        //     `Mode.Custom` (and `Mode.Attack`) to `null` — no built-in mutation — because AS-IS Custom mode
        //     always delegates to a bespoke per-card coroutine (here: GainPierce). SelectBody.Apply just calls
        //     `_select.Apply(sink, selected)`, i.e. BuildMutations(selected) → nothing for Custom. There is no
        //     hook for a body to run bespoke per-selected-target logic after Custom-mode selection resolves.
        //   - A keyword-grant continuous exists too: PiercingStaticEffect / ContinuousPlayerScopeKeywordEffect
        //     (CardPortingFramework.cs:1594, 5013) grants Piercing to the owner's Digimon matching a
        //     `Func<Permanent,bool>` scope predicate — but (a) it is a static/declarative grant fixed at
        //     construction time, wired only through the non-interactive GrantContinuousBody
        //     (ActivatedEffect.cs:255), whose Apply ignores its own `selected` parameter entirely — so there is
        //     no way to construct the scope predicate FROM an interactive SelectBody's resolved choice within a
        //     single ActivatedEffect (one body, chosen up front); and (b) ContinuousPlayerScopeKeywordEffect.
        //     ToBinding (CardPortingFramework.cs:1653-1656) always registers with `duration: null` (permanent,
        //     condition-gated) — there is no `EffectDuration.UntilEachTurnEnd`-scoped wiring path for a keyword
        //     grant surfaced anywhere in ActivatedEffect.cs, so even a single always-all-owner's-Digimon grant
        //     could not reproduce the AS-IS one-turn expiry.
        // No existing IEffectBody composes "interactively select 1 target, then grant a keyword scoped to
        // exactly that target for the remainder of the turn" (mirroring how SelectTrashHandThenSelfMutationBody
        // composes an interactive selection with a fixed self-mutation follow-up, but for a per-selection
        // keyword grant instead of a fixed-target one). Registering PiercingStaticEffect unconditionally
        // (dropping the selection, granting to ALL of the owner's Digimon) or dropping the turn-only expiry
        // (making it permanent) would each be a materially different effect, not a faithful partial mirror
        // (fidelity-over-coverage) — so the whole timing is left unregistered pending a new IEffectBody (e.g. a
        // "SelectAndGrantKeywordBody" composing SelectPermanentEffect's interactive pick with a per-selected-id
        // scoped + turn-expiring keyword grant) — engine-side primitive development, deferred to the
        // strong-model primitive pass. — 강모델
        // if (timing == EffectTiming.OptionSkill) { ... }

        return cardEffects;
    }
}
