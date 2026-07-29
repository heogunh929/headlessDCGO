// ============================================================================================================
// A DECISION POINT — one question the engine is waiting on, surfaced to a policy.
//
// The engine's ask surfaces were censused twice (selection channels 2026-07-29; stage-4 design): every ask
// MATERIALISES its legal candidates — click-wired cards/permanents, generated command buttons, a candidate
// list. A DecisionPoint carries that materialised set, so legality is BY CONSTRUCTION (no rule predicate is
// re-implemented here). Multi-argument moves (play→target frame, attacker→defender) are decomposed into
// SEQUENTIAL decision points — the AS-IS's own incremental single-pick shape.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Rl;

/// <summary>What kind of question is pending. Order is the observation one-hot layout — append only.</summary>
public enum DecisionKind
{
    MainPhase = 0,      // NULL=pass, hand[i]=play, myField[j]=attack with j
    PlayTarget = 1,     // NULL=to empty frame (AS-IS PreferredFrame), myField[j]=onto that frame (evolution)
    AttackTarget = 2,   // foePlayer=security attack, foeField[j]=that defender
    Breeding = 3,       // NULL=decline, YES=hatch or move out
    Mulligan = 4,       // NULL=keep, YES=redraw
    Optional = 5,       // NULL=no, YES=use
    Selection = 6,      // hand[i]/myField[j]/foeField[j]/choice[i]=pick candidate, NULL=no-select/end (when legal)
    Command = 7,        // choice[i]=i-th command button
    Count = 8,          // choice[i]=i-th entry of the selector's candidate list
}

/// <summary>One pending question with its materialised candidates. Lane semantics per kind are documented on
/// <see cref="DecisionKind"/>; the apply logic lives in <see cref="PolicyVirtualPlayer"/>.</summary>
public sealed class DecisionPoint
{
    public required DecisionKind Kind { get; init; }

    public required Player Seat { get; init; }

    /// <summary>True when lane 0 (NULL: pass/decline/keep/no-select) is legal for this question.</summary>
    public bool NullLegal { get; init; }

    /// <summary>True when lane 1 (YES) is legal — bool-shaped questions only.</summary>
    public bool YesLegal { get; init; }

    /// <summary>Legal hand slots of <see cref="Seat"/> (indices into Seat.HandCards).</summary>
    public IReadOnlyList<int> HandSlots { get; init; } = Array.Empty<int>();

    /// <summary>Legal own-field slots (indices into Seat.GetFieldPermanents()).</summary>
    public IReadOnlyList<int> MyFieldSlots { get; init; } = Array.Empty<int>();

    /// <summary>Legal enemy-field slots (indices into the enemy's GetFieldPermanents()).</summary>
    public IReadOnlyList<int> FoeFieldSlots { get; init; } = Array.Empty<int>();

    /// <summary>True when the enemy player itself is a legal target (security attack).</summary>
    public bool FoePlayerLegal { get; init; }

    /// <summary>Generic candidates for choice lanes: their display card ids feed the observation, the pick
    /// applies via <see cref="ChoiceApply"/>. Capped at the schema's maxChoice with an overflow log.</summary>
    public IReadOnlyList<int> ChoiceCardIds { get; init; } = Array.Empty<int>();

    public int ChoiceCount { get; init; }

    /// <summary>Applies a choice-lane pick (0-based candidate ordinal).</summary>
    public Action<int>? ChoiceApply { get; init; }

    /// <summary>Cards already accumulated in the CURRENT multi-selection (the selector's own partial list —
    /// panel `_preSelectedHandCardList`, SelectHandEffect `_targetCards`, SelectPermanentEffect
    /// `_targetPermanents`). Observation channel `choice.selectedCount`.</summary>
    public int SelectedCount { get; init; }
}
