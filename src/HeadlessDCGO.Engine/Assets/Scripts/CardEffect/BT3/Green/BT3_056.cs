// Source: Assets/Scripts/CardEffect/BT3/Green/BT3_056.cs — a Digimon (three timing blocks, all "Digisorption").
// AS-IS:
//   BeforePayCost — the IDENTICAL ">Digisorption -3>" mechanic as the BT3_054 sibling (verbatim same
//     CanUseCondition/ActivateCoroutine shape: cross-player WhenDigisorption cut-in broadcast, then an
//     interactive Mode.Tap self-suspend-cost select, then a temporary ChangeCostClass registration).
//   WhenDigisorption — "[Your Turn][Once Per Turn] When suspending Digimon for a <Digisorption> skill, you may
//     suspend your opponent's Digimon instead." A second ActivateClass declared UNDER the WhenDigisorption
//     timing itself (the cut-in the BeforePayCost block above broadcasts to), which — if triggered — registers
//     a CanSuspendByDigisorptionClass onto card.Owner.UntilCalculateFixedCostEffect redirecting the upcoming
//     Digisorption suspend-cost target to an opponent's Digimon instead of the owner's own.
//   None — a baseline (always-registered, SetNotShowUI(true)) CanSuspendByDigisorptionClass granting "you may
//     suspend your opponent's Digimon instead" whenever this card's own [Your Turn][Once Per Turn] cap has not
//     been used yet this turn (read via card.cEntity_EffectController.isOverMaxCountPerTurn on the
//     WhenDigisorption-timing effect declared above).
//
// STOP (genuine engine-mechanic gap — the entire "Digisorption" keyword-ability family, not a per-card
// shortcut; grepped 2x+ per rule 4, same finding as the BT3_054 sibling):
//   - `EffectTiming.WhenDigisorption` does not exist in the headless EffectTiming enum (grepped
//     CardPortingFramework.cs:36-200+) — the WhenDigisorption block below cannot even declare its `if (timing
//     == ...)` guard against a real enum member, and the BeforePayCost block's cross-player cut-in broadcast to
//     that timing has nothing to broadcast to.
//   - `CardEffectCommons.CanTapWhenAbsorbEvolution` / `_CheckAvailability` — absent (grepped, zero hits).
//   - `Assets/Scripts/Script/CardEffects/CanSuspendByDigisorptionClass.cs` — the headless mirror is an
//     unimplemented skeleton (Decision: PORT, "Skeleton only"); it is not a CardEffect/BT3/Green file (out of
//     this unit's scope) and, per the primitive-predevelopment rule, building it out is engine-file work out
//     of scope for a single-card porting pass regardless.
//   - No IEffectBody/factory composes "redirect the target of an in-flight Digisorption suspend-cost to the
//     opponent's side" or "grant this redirect only while this card's own once-per-turn WhenDigisorption
//     effect is unused" — both are Digisorption-internal bookkeeping with zero headless analogue.
// Per rule 4 this is a primitive gap requiring new engine-layer work (the same WhenDigisorption timing +
// cut-in broadcast + CanTapWhenAbsorbEvolution predicates + CanSuspendByDigisorptionClass implementation the
// BT3_054 sibling STOP documents), out of scope for a single-card porting pass. No cardEffects registered for
// any of the three timings the AS-IS declares. — 강모델
// if (timing == EffectTiming.BeforePayCost) { ... }
// if (timing == EffectTiming.WhenDigisorption) { ... }  // enum member does not exist
// if (timing == EffectTiming.None) { ... }

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_056 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        return cardEffects;
    }
}
