namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Text;

public static class HeadlessPhaseMapping
{
    private static readonly IReadOnlyDictionary<string, HeadlessPhase> AliasMap =
        new Dictionary<string, HeadlessPhase>(StringComparer.Ordinal)
        {
            [Normalize("none")] = HeadlessPhase.None,

            // (R4 S2) The pre-game setup step collapses to phase None (its step position is the
            // TurnStepCursor.Starting sub-cursor). AS-IS has no "Setup" game phase.
            [Normalize("setup")] = HeadlessPhase.None,
            [Normalize("setup phase")] = HeadlessPhase.None,
            [Normalize("GameStateMachine setup")] = HeadlessPhase.None,

            [Normalize("active")] = HeadlessPhase.Active,
            [Normalize("active phase")] = HeadlessPhase.Active,
            [Normalize("GameContext.phase.Active")] = HeadlessPhase.Active,
            [Normalize("TurnStateMachine.ActivePhase")] = HeadlessPhase.Active,
            [Normalize("start turn")] = HeadlessPhase.Active,
            [Normalize("on start turn")] = HeadlessPhase.Active,

            // (R4 S2) The natural-unsuspend step folds into the active phase (AS-IS phase.Active); its step
            // position is the TurnStepCursor.Unsuspending sub-cursor.
            [Normalize("unsuspend")] = HeadlessPhase.Active,
            [Normalize("unsuspend phase")] = HeadlessPhase.Active,
            [Normalize("ActivePhase.Unsuspend")] = HeadlessPhase.Active,
            [Normalize("IUnsuspendPermanents")] = HeadlessPhase.Active,

            [Normalize("draw")] = HeadlessPhase.Draw,
            [Normalize("draw phase")] = HeadlessPhase.Draw,
            [Normalize("GameContext.phase.Draw")] = HeadlessPhase.Draw,
            [Normalize("TurnStateMachine.DrawPhase")] = HeadlessPhase.Draw,

            [Normalize("breeding")] = HeadlessPhase.Breeding,
            [Normalize("breeding phase")] = HeadlessPhase.Breeding,
            [Normalize("raising")] = HeadlessPhase.Breeding,
            [Normalize("GameContext.phase.Breeding")] = HeadlessPhase.Breeding,
            [Normalize("TurnStateMachine.BreedingPhase")] = HeadlessPhase.Breeding,

            [Normalize("main")] = HeadlessPhase.Main,
            [Normalize("main phase")] = HeadlessPhase.Main,
            [Normalize("GameContext.phase.Main")] = HeadlessPhase.Main,
            [Normalize("TurnStateMachine.MainPhase")] = HeadlessPhase.Main,

            // (R4 S2) The memory-pass step folds into the main phase (AS-IS phase.Main); its step position is the
            // TurnStepCursor.AwaitingMemoryPassEnd sub-cursor.
            [Normalize("memory pass")] = HeadlessPhase.Main,
            [Normalize("memory pass flow")] = HeadlessPhase.Main,
            [Normalize("pass")] = HeadlessPhase.Main,
            [Normalize("pass turn")] = HeadlessPhase.Main,
            [Normalize("PassTurn")] = HeadlessPhase.Main,
            [Normalize("EndTurnProcess")] = HeadlessPhase.Main,
            [Normalize("main memory pass")] = HeadlessPhase.Main,

            [Normalize("end")] = HeadlessPhase.End,
            [Normalize("end phase")] = HeadlessPhase.End,
            [Normalize("GameContext.phase.End")] = HeadlessPhase.End,
            [Normalize("TurnStateMachine.EndPhase")] = HeadlessPhase.End
        };

    private static readonly IReadOnlyDictionary<HeadlessPhase, string> AsIsNames =
        new Dictionary<HeadlessPhase, string>
        {
            [HeadlessPhase.None] = "none",
            [HeadlessPhase.Active] = "active",
            [HeadlessPhase.Draw] = "draw",
            [HeadlessPhase.Breeding] = "breeding",
            [HeadlessPhase.Main] = "main",
            [HeadlessPhase.End] = "end"
        };

    /// <summary>(R4 S2) The AS-IS 6-value phase order (value/order 1:1 with GameContext.phase, None first for the
    /// mirror's default-empty convention). One-hot feature order for RL observation.</summary>
    public static IReadOnlyList<HeadlessPhase> ObservationPhaseOrder { get; } = new[]
    {
        HeadlessPhase.None,
        HeadlessPhase.Active,
        HeadlessPhase.Draw,
        HeadlessPhase.Breeding,
        HeadlessPhase.Main,
        HeadlessPhase.End
    };

    /// <summary>(R4 S2) The substrate step-cursor order — the RL observation feature order for the sub-cursor.</summary>
    public static IReadOnlyList<TurnStepCursor> StepCursorOrder { get; } = new[]
    {
        TurnStepCursor.PhaseStart,
        TurnStepCursor.Starting,
        TurnStepCursor.Unsuspending,
        TurnStepCursor.AwaitingMemoryPassEnd
    };

    /// <summary>(R4 S2) The canonical turn-flow step walk — each (phase, cursor) pair the AdvancePhase driver moves
    /// through, in order. This replaces the former invented 9-value <c>AsIsTurnSequence</c>: the same eight distinct
    /// turn positions, now keyed by (phase, cursor) so no state-distinction is lost. The memory-pass / end steps are
    /// reached via <c>SetPhase</c> in live flow, but stay in the walk so <see cref="NextStep"/> is total.</summary>
    public static IReadOnlyList<(HeadlessPhase Phase, TurnStepCursor Cursor)> TurnStepSequence { get; } = new[]
    {
        (HeadlessPhase.None, TurnStepCursor.Starting),               // former Setup
        (HeadlessPhase.Active, TurnStepCursor.PhaseStart),           // former Active
        (HeadlessPhase.Active, TurnStepCursor.Unsuspending),         // former Unsuspend
        (HeadlessPhase.Draw, TurnStepCursor.PhaseStart),             // former Draw
        (HeadlessPhase.Breeding, TurnStepCursor.PhaseStart),         // former Breeding
        (HeadlessPhase.Main, TurnStepCursor.PhaseStart),             // former Main
        (HeadlessPhase.Main, TurnStepCursor.AwaitingMemoryPassEnd),  // former MemoryPass
        (HeadlessPhase.End, TurnStepCursor.PhaseStart)               // former End
    };

    public static HeadlessPhase FromAsIsName(string asIsPhase)
    {
        return TryFromAsIsName(asIsPhase, out HeadlessPhase phase)
            ? phase
            : throw new ArgumentException($"Unknown AS-IS phase name: '{asIsPhase}'.", nameof(asIsPhase));
    }

    public static bool TryFromAsIsName(
        string? asIsPhase,
        out HeadlessPhase phase)
    {
        if (string.IsNullOrWhiteSpace(asIsPhase))
        {
            phase = HeadlessPhase.None;
            return false;
        }

        return AliasMap.TryGetValue(Normalize(asIsPhase), out phase);
    }

    public static string ToAsIsName(HeadlessPhase phase)
    {
        EnsureDefined(phase);
        return AsIsNames[phase];
    }

    /// <summary>(R4 S2) The next (phase, cursor) step in the turn walk. Replaces the former phase-only
    /// <c>Next(HeadlessPhase)</c>: the AdvancePhase driver keys on the full (phase, cursor) pair so the sub-cursor
    /// positions (Setup / Unsuspend / MemoryPass) advance correctly. Pure (None, PhaseStart) begins the setup step;
    /// the terminal (End, PhaseStart) is a fixed point.</summary>
    public static (HeadlessPhase Phase, TurnStepCursor Cursor) NextStep(HeadlessPhase phase, TurnStepCursor cursor)
    {
        EnsureDefined(phase);

        // Pure None (no cursor) begins the pre-game setup step.
        if (phase == HeadlessPhase.None && cursor == TurnStepCursor.PhaseStart)
        {
            return (HeadlessPhase.None, TurnStepCursor.Starting);
        }

        int index = IndexOfStep(phase, cursor);
        if (index < 0 || index == TurnStepSequence.Count - 1)
        {
            return (HeadlessPhase.End, TurnStepCursor.PhaseStart);
        }

        return TurnStepSequence[index + 1];
    }

    public static bool CanAdvance(HeadlessPhase phase)
    {
        EnsureDefined(phase);
        return phase != HeadlessPhase.End;
    }

    public static bool IsDefined(HeadlessPhase phase)
    {
        return AsIsNames.ContainsKey(phase);
    }

    public static void EnsureDefined(HeadlessPhase phase)
    {
        if (!IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown headless phase.");
        }
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static int IndexOfStep(HeadlessPhase phase, TurnStepCursor cursor)
    {
        for (int i = 0; i < TurnStepSequence.Count; i++)
        {
            if (TurnStepSequence[i].Phase == phase && TurnStepSequence[i].Cursor == cursor)
            {
                return i;
            }
        }

        return -1;
    }
}
