// Source: Assets/Scripts/Script/SelectPermanentEffect.cs
// Decision: PORT
// Category: AIUseful
// Migration: Port core engine source
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
//
// AS-IS mirror of the original DCGO SelectPermanentEffect (a MonoBehaviour selection flow).
// The original SetUp(...) captures a target predicate, max count, the canNoSelect/canEndNotMax
// rules, and a Mode that decides what is done to each selected Permanent. Headless port keeps the
// same authoring shape but is deterministic:
//   (1) BuildRequest  — enumerate the live board, filter by the target predicate, and build a
//                       Permanent ChoiceRequest honouring max/canNoSelect/canEndNotMax (CV-A2 / F-2).
//   (2) BuildMutations — map the selection Mode to MatchStateMutation(s) per selected target,
//                       reusing the existing mutation vocabulary (CV-A2 / F-2.3 / F-2.5).
// Resolution itself reuses EffectChoiceHelpers.ResolveAsync / DeferredChoiceProvider; this class
// only owns candidate enumeration and the Mode→mutation mapping.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class SelectPermanentEffect
{
    // 1:1 with the original SelectPermanentEffect.Mode.
    public enum Mode
    {
        Tap,
        UnTap,
        Destroy,
        Bounce,
        PutLibraryBottom,
        PutLibraryTop,
        PutSecurityBottom,
        PutSecurityTop,
        Degenerate,
        Attack,
        Custom,
    }

    private HeadlessPlayerId _selectPlayer;
    private Func<HeadlessEntityId, bool> _canTargetCondition = static _ => true;
    private int _maxCount = 1;
    private bool _canNoSelect;
    private bool _canEndNotMax;
    private Mode _mode = Mode.Custom;
    private HeadlessEntityId _sourceEntityId = new("select");
    private string _message = "Select target permanent(s).";
    private bool _faceUp;
    private int _degenerationCount = 1;
    private bool _canAttackPlayer = true;
    private Func<HeadlessEntityId, bool>? _defenderCondition;
    private Func<IReadOnlyList<HeadlessEntityId>, bool>? _canEndSelectCondition;

    /// <summary>Mirrors the original SetUp: who selects, the per-target predicate, the count rules, and
    /// the action mode. <paramref name="sourceEntityId"/> is the effect source the mutations are attributed
    /// to (the original threads this through <c>cardEffect.EffectSourceCard</c>).</summary>
    public void SetUp(
        HeadlessPlayerId selectPlayer,
        Func<HeadlessEntityId, bool> canTargetCondition,
        int maxCount,
        bool canNoSelect,
        bool canEndNotMax,
        Mode mode,
        HeadlessEntityId sourceEntityId)
    {
        ArgumentNullException.ThrowIfNull(canTargetCondition);
        if (maxCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Max count must be at least 1.");
        }

        _selectPlayer = selectPlayer;
        _canTargetCondition = canTargetCondition;
        _maxCount = maxCount;
        _canNoSelect = canNoSelect;
        _canEndNotMax = canEndNotMax;
        _mode = mode;
        _sourceEntityId = sourceEntityId.IsEmpty ? new HeadlessEntityId("select") : sourceEntityId;
    }

    public void SetUpCustomMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _message = message;
        }
    }

    /// <summary>(PutSecurity*) whether returned cards are placed face up.</summary>
    public void SetFaceUp(bool faceUp) => _faceUp = faceUp;

    /// <summary>(B5) AS-IS <c>_degenerationCount</c> — how many sources <see cref="Mode.Degenerate"/>
    /// removes per selected permanent (default 1).</summary>
    public void SetDegenerationCount(int count) => _degenerationCount = Math.Max(1, count);

    /// <summary>(B5) AS-IS ctor's <c>canAttackPlayer</c> / <c>defenderCondition</c> — forwarded into the
    /// <see cref="Mode.Attack"/> sub-flow (<c>SelectAttackEffect.SetUp(canAttackPlayerCondition,
    /// defenderCondition, ...)</c>).</summary>
    public void SetAttackOptions(bool canAttackPlayer, Func<HeadlessEntityId, bool>? defenderCondition = null)
    {
        _canAttackPlayer = canAttackPlayer;
        _defenderCondition = defenderCondition;
    }

    /// <summary>(B5) AS-IS <c>CanEndSelect</c>'s combination gate (<c>_canEndSelectCondition(permanents)</c>)
    /// — constrains which selection SETS are legal (e.g. "two of different colours"). Consult via
    /// <see cref="IsValidSelection"/> when resolving the choice.</summary>
    public void SetCanEndSelectCondition(Func<IReadOnlyList<HeadlessEntityId>, bool> canEndSelectCondition)
    {
        ArgumentNullException.ThrowIfNull(canEndSelectCondition);
        _canEndSelectCondition = canEndSelectCondition;
    }

    /// <summary>(B5) whether <paramref name="selection"/> satisfies the AS-IS combination gate (true when
    /// none is configured).</summary>
    public bool IsValidSelection(IReadOnlyList<HeadlessEntityId> selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return _canEndSelectCondition is null || _canEndSelectCondition(selection);
    }

    /// <summary>Enumerate the battle areas of <paramref name="players"/>, filter by the target predicate,
    /// and build the Permanent ChoiceRequest. Count rules follow the original:
    /// canNoSelect ⇒ may finish with zero (skippable); canEndNotMax ⇒ may finish below max (min 1);
    /// otherwise an exact pick of max is required. Counts clamp to the available candidate pool.</summary>
    public ChoiceRequest BuildRequest(IZoneStateReader zones, IEnumerable<HeadlessPlayerId> players)
    {
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(players);

        var candidates = new List<ChoiceCandidate>();
        foreach (HeadlessPlayerId player in players)
        {
            foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
            {
                if (_canTargetCondition(id))
                {
                    candidates.Add(EffectChoiceHelpers.Candidate(id, id.Value, ChoiceZone.BattleArea, isSelectable: true, player));
                }
            }
        }

        int available = candidates.Count;
        int maxCount = Math.Min(_maxCount, available);
        int minCount = _canNoSelect ? 0 : (_canEndNotMax ? Math.Min(1, maxCount) : maxCount);
        bool canSkip = _canNoSelect;

        ChoiceRequest request = EffectChoiceHelpers.CreatePermanentRequest(_selectPlayer, _message, minCount, maxCount, canSkip, candidates);
        // (P2) the AS-IS combination gate (CanEndSelect) rides on the request so the choice controller
        // rejects an illegal SET centrally (try-reject-retry).
        return _canEndSelectCondition is null ? request : request with { SelectionValidator = _canEndSelectCondition };
    }

    /// <summary>Map the configured Mode to one mutation per selected target. Attack/Custom yield no
    /// built-in mutation (the original delegates those to a bespoke coroutine); Degenerate is the
    /// de-digivolve subsystem (D-4) and is not yet a mutation.</summary>
    public IReadOnlyList<EffectMutation> BuildMutations(IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);

        var mutations = new List<EffectMutation>();
        foreach (HeadlessEntityId target in selected)
        {
            EffectMutation? mutation = BuildMutation(target);
            if (mutation is not null)
            {
                mutations.Add(mutation);
            }
        }

        return mutations;
    }

    /// <summary>Resolve the Mode's mutations against a live mutation sink (per-target, in selection order).</summary>
    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        foreach (EffectMutation mutation in BuildMutations(selected))
        {
            sink.Apply(mutation);
        }
    }

    private EffectMutation? BuildMutation(HeadlessEntityId target)
    {
        return _mode switch
        {
            Mode.Tap => Mutation(MatchStateMutationSink.SuspendKind, target),
            Mode.UnTap => Mutation(MatchStateMutationSink.UnsuspendKind, target),
            Mode.Destroy => Mutation(MatchStateMutationSink.DeleteKind, target),
            Mode.Bounce => Mutation(MatchStateMutationSink.ReturnToHandKind, target),
            Mode.PutLibraryTop => Mutation(MatchStateMutationSink.ReturnToDeckTopKind, target),
            Mode.PutLibraryBottom => Mutation(MatchStateMutationSink.ReturnToDeckBottomKind, target),
            // (B5) NOTE: AS-IS guards every placement with Owner.CanAddSecurity(cardEffect); the restriction
            // effect (CannotAddSecurityClass) is an unported skeleton with no grants — fold the gate here
            // when it lands (fidelity_debt.md, same seam as Ascension/Save).
            Mode.PutSecurityTop => SecurityMutation(target, toBottom: false),
            Mode.PutSecurityBottom => SecurityMutation(target, toBottom: true),
            // (B5) AS-IS Degenerate: new IDegeneration(selected, _degenerationCount, effect) — the D-4
            // de-digivolve subsystem (rookie floor + WhenTopCardTrashed are inside the helper, 1:1 with
            // IDegeneration's Level==3 stop).
            Mode.Degenerate => new EffectMutation(
                MatchStateMutationSink.DeDigivolveKind,
                _sourceEntityId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.TargetEntityIdKey] = target.Value,
                    [MatchStateMutationSink.CountKey] = _degenerationCount,
                }),
            Mode.Attack or Mode.Custom => null,
            _ => null,
        };
    }

    /// <summary>(B5/P3) AS-IS <see cref="Mode.Attack"/>: each selected permanent that can attack runs a
    /// <c>SelectAttackEffect</c> sub-flow honouring <c>canAttackPlayer</c>/<c>defenderCondition</c>,
    /// SEQUENTIALLY (SelectPermanentEffect.cs:1009-1027). The headless flow opens the target choice for the
    /// first attacker and queues the rest — the AttackPipeline dequeues the next once each attack fully
    /// completes (each re-checked alive/legal on its turn). Returns true when a choice opened.</summary>
    public bool TryOpenAttack(HeadlessDCGO.Engine.Headless.Bridge.EngineContext context, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selected);
        if (_mode != Mode.Attack)
        {
            return false;
        }

        // Normal attack targeting (suspended Digimon only) + the AS-IS per-effect narrowing.
        var options = new HeadlessDCGO.Engine.Headless.Runtime.EffectAttackOptions(
            WithoutTap: false,
            AllowPlayerTarget: _canAttackPlayer,
            AllowDigimonTarget: true,
            TargetUnsuspended: false)
        {
            DefenderCondition = _defenderCondition,
        };
        return HeadlessDCGO.Engine.Headless.Runtime.EffectDrivenAttack.RequestQueuedChoices(context, selected.ToArray(), options);
    }

    private EffectMutation Mutation(string kind, HeadlessEntityId target)
    {
        return new EffectMutation(
            kind,
            _sourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = target.Value,
            });
    }

    private EffectMutation SecurityMutation(HeadlessEntityId target, bool toBottom)
    {
        return new EffectMutation(
            MatchStateMutationSink.AddToSecurityKind,
            _sourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = target.Value,
                [MatchStateMutationSink.ToBottomKey] = toBottom,
                [MatchStateMutationSink.FaceUpKey] = _faceUp,
            });
    }
}
