namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Text;

public static class HeadlessPhaseMapping
{
    private static readonly IReadOnlyDictionary<string, HeadlessPhase> AliasMap =
        new Dictionary<string, HeadlessPhase>(StringComparer.Ordinal)
        {
            [Normalize("none")] = HeadlessPhase.None,

            [Normalize("setup")] = HeadlessPhase.Setup,
            [Normalize("setup phase")] = HeadlessPhase.Setup,
            [Normalize("GameStateMachine setup")] = HeadlessPhase.Setup,

            [Normalize("active")] = HeadlessPhase.Active,
            [Normalize("active phase")] = HeadlessPhase.Active,
            [Normalize("GameContext.phase.Active")] = HeadlessPhase.Active,
            [Normalize("TurnStateMachine.ActivePhase")] = HeadlessPhase.Active,
            [Normalize("start turn")] = HeadlessPhase.Active,
            [Normalize("on start turn")] = HeadlessPhase.Active,

            [Normalize("unsuspend")] = HeadlessPhase.Unsuspend,
            [Normalize("unsuspend phase")] = HeadlessPhase.Unsuspend,
            [Normalize("ActivePhase.Unsuspend")] = HeadlessPhase.Unsuspend,
            [Normalize("IUnsuspendPermanents")] = HeadlessPhase.Unsuspend,

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

            [Normalize("memory pass")] = HeadlessPhase.MemoryPass,
            [Normalize("memory pass flow")] = HeadlessPhase.MemoryPass,
            [Normalize("pass")] = HeadlessPhase.MemoryPass,
            [Normalize("pass turn")] = HeadlessPhase.MemoryPass,
            [Normalize("PassTurn")] = HeadlessPhase.MemoryPass,
            [Normalize("EndTurnProcess")] = HeadlessPhase.MemoryPass,
            [Normalize("main memory pass")] = HeadlessPhase.MemoryPass,

            [Normalize("end")] = HeadlessPhase.End,
            [Normalize("end phase")] = HeadlessPhase.End,
            [Normalize("GameContext.phase.End")] = HeadlessPhase.End,
            [Normalize("TurnStateMachine.EndPhase")] = HeadlessPhase.End
        };

    private static readonly IReadOnlyDictionary<HeadlessPhase, string> AsIsNames =
        new Dictionary<HeadlessPhase, string>
        {
            [HeadlessPhase.None] = "none",
            [HeadlessPhase.Setup] = "setup",
            [HeadlessPhase.Active] = "active",
            [HeadlessPhase.Unsuspend] = "unsuspend",
            [HeadlessPhase.Draw] = "draw",
            [HeadlessPhase.Breeding] = "breeding",
            [HeadlessPhase.Main] = "main",
            [HeadlessPhase.MemoryPass] = "memory_pass",
            [HeadlessPhase.End] = "end"
        };

    public static IReadOnlyList<HeadlessPhase> AsIsTurnSequence { get; } = new[]
    {
        HeadlessPhase.Setup,
        HeadlessPhase.Active,
        HeadlessPhase.Unsuspend,
        HeadlessPhase.Draw,
        HeadlessPhase.Breeding,
        HeadlessPhase.Main,
        HeadlessPhase.MemoryPass,
        HeadlessPhase.End
    };

    public static IReadOnlyList<HeadlessPhase> ObservationPhaseOrder { get; } = new[]
    {
        HeadlessPhase.None,
        HeadlessPhase.Setup,
        HeadlessPhase.Active,
        HeadlessPhase.Unsuspend,
        HeadlessPhase.Draw,
        HeadlessPhase.Breeding,
        HeadlessPhase.Main,
        HeadlessPhase.MemoryPass,
        HeadlessPhase.End
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

    public static HeadlessPhase Next(HeadlessPhase phase)
    {
        EnsureDefined(phase);

        if (phase == HeadlessPhase.None)
        {
            return HeadlessPhase.Setup;
        }

        int index = IndexOfAsIsPhase(phase);
        if (index < 0 || index == AsIsTurnSequence.Count - 1)
        {
            return HeadlessPhase.End;
        }

        return AsIsTurnSequence[index + 1];
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

    private static int IndexOfAsIsPhase(HeadlessPhase phase)
    {
        for (int i = 0; i < AsIsTurnSequence.Count; i++)
        {
            if (AsIsTurnSequence[i] == phase)
            {
                return i;
            }
        }

        return -1;
    }
}
