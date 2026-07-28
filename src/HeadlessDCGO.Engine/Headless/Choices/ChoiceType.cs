namespace HeadlessDCGO.Engine.Headless.Choices;

public enum ChoiceType
{
    Unknown = 0,
    Card,
    HandCard,
    Permanent,
    Count,
    AttackTarget,
    MainPhaseAction,
    OptionalEffect,
    Blocker,
    // N-5: the opening-hand mulligan decision (keep vs redraw), made per player before security is dealt.
    Mulligan,
    // F-6.8: the optional "would be deleted" replacement decision (Evade/Barrier/Decoy/... activate or skip)
    // surfaced to the owner when a deletion would occur, mirroring the AS-IS optional keyword prompt.
    DeletionReplacement,
    // C-18 Alliance: the optional "suspend an ally to boost this attacker" decision opened when an Alliance
    // Digimon attacks (mirrors the AS-IS optional SelectPermanent suspend-cost prompt).
    AllianceTarget,
    // S1 (C-20 Vortex / C-16 Overclock): the optional target choice when an EFFECT initiates an attack
    // (mirrors the AS-IS SelectAttackEffect target selection).
    EffectAttack,
    // C-16 Overclock: the optional "delete a trait-matching ally" decision at end of turn (mirrors the
    // AS-IS SelectPermanent of an Overclock-trait Digimon to delete, before the untapped player attack).
    OverclockTarget,
    // B-7: select from revealed deck-top cards (AS-IS RevealLibrary RevealDeckTopCardsAndSelect) — the
    // selected cards go to one destination, the rest to another.
    RevealSelect,
    // (PRIM-P0-flow) the mandatory "choose one of the following modes" menu (AS-IS UserSelectionManager
    // SetBool/IntSelection). Candidates are synthetic labeled options; the selected branch is dispatched
    // by ActivatedEffectResolver. See docs/porting/mode_choice_primitive_design.md.
    ModeChoice,
    // (Stage 5, Phase 3) a trigger-window decision — either "which of these simultaneous effects resolves
    // first" (AS-IS MultipleSkills OpenSelectCardPanel, _MaxCount:1) or "activate this optional effect? yes/no"
    // (AS-IS Activate_Optional). Candidate Id == the trigger's effect id; skip == "don't activate". Resolved
    // INLINE by the pump-deposit seam in ResolveChoiceAsync (the skill body parks the pump on ChooseAsync).
    WindowChoice,
    // (R4 S3a) the breeding-phase decision (AS-IS TurnStateMachine.cs:719-816, a bool ValueSelection —
    // SendShouldHatch semantics): act (hatch when possible, else move — AS-IS both-possible resolves to hatch)
    // vs decline (skip). One synthetic candidate; skip == decline. Pump-parked (TurnFlowPump) and resolved by
    // the deposit seam in ResolveChoiceAsync.
    BreedingDecision
}
