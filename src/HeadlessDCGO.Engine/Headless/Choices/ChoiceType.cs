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
    RevealSelect
}
