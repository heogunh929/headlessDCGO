// Source: Assets/Scripts/CardEffect/BT3/Green/BT3_054.cs — a Digimon (single BeforePayCost timing block).
// AS-IS: ">Digisorption -3>" — "When one of your Digimon digivolves into this card from your hand, you may
// suspend 1 of your Digimon to reduce the memory cost of the digivolution by 3." This is the "Digisorption"
// keyword ability (distinct from the ordinary Digivolve mechanic): ActivateClass on EffectTiming.BeforePayCost
// whose CanUseCondition reads a raw hashtable ("Card"==this card, "isEvolution"==true, "Permanents" contains
// an owner battle-area permanent, plus at least one player globally has an owner-side CanTapWhenAbsorbEvolution_
// CheckAvailability match) and whose ActivateCoroutine (a) broadcasts a WhenDigisorption cut-in skill pass to
// EVERY player's field permanents + player-level effects (GManager.instance.autoProcessing_CutIn), (b) then
// interactively SelectPermanentEffect(Mode.Tap, canNoSelect:true) suspends 1 of the owner's OWN Digimon
// (CanTapWhenAbsorbEvolution-gated), and (c) on a successful pick registers a temporary ChangeCostClass
// (-3 digivolution cost, gated on CardSourceCondition==this card + RootCondition + PermanentsCondition) into
// card.Owner.UntilCalculateFixedCostEffect for the CURRENT cost calculation pass.
//
// STOP (genuine engine-mechanic gap, not a per-card shortcut — grepped 2x+ per rule 4):
// Every supporting primitive this mechanic needs is absent from headless:
//   - `CardEffectCommons.CanTapWhenAbsorbEvolution` / `CanTapWhenAbsorbEvolution_CheckAvailability` — grepped
//     CardPortingFramework.cs, zero hits. No headless predicate reads "can this permanent be tapped to pay a
//     Digisorption cost" at all.
//   - `EffectTiming.WhenDigisorption` — grepped the EffectTiming enum (CardPortingFramework.cs:36-200+), no
//     such member exists; the WhenDigisorption cut-in broadcast pass (AS-IS ActivateCoroutine step (a)) has no
//     headless timing to even declare under.
//   - `Assets/Scripts/Script/CardEffects/CanSuspendByDigisorptionClass.cs` — the headless mirror of this
//     AS-IS support class (referenced by BT3_056's None-timing sibling block) is itself an unimplemented
//     skeleton (Decision: PORT, "Skeleton only" — not a card file, out of this unit's Green-folder scope, and
//     per the primitive-predevelopment rule engine-file work is out of scope for a single-card porting pass).
//   - The interactive "suspend 1 of the owner's OWN Digimon as a cost, THEN register a THIS-CARD-scoped
//     temporary digivolution-cost reduction for the in-flight cost calculation" composition has no analogue in
//     the uniform ActivatedEffect body catalog (ActivatedEffect.cs) or the legacy factory surface
//     (CardEffectFactory.BeforePayCostReductionEffect reduces THIS card's cost unconditionally/by a Func<bool>
//     condition — it has no suspend-cost gate, no interactive select step, and no cross-player cut-in broadcast).
// "Digisorption" is a distinct keyword-ability family (like Digi-Burst/Blast/Jogress before those were built)
// that has not been given ANY headless engine support yet — not a naming miss, a whole mechanism absent. Per
// rule 4 this is a primitive gap requiring new engine-layer work (a WhenDigisorption timing + cut-in broadcast
// + CanTapWhenAbsorbEvolution predicates + a suspend-cost-then-cost-reduce IEffectBody), out of scope for a
// single-card porting pass. No cardEffects registered (the only timing the AS-IS declares). — 강모델
// if (timing == EffectTiming.BeforePayCost) { ... }

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_054 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        return cardEffects;
    }
}
