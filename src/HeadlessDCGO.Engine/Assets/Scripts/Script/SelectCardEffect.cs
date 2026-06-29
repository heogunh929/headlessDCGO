// Source: Assets/Scripts/Script/SelectCardEffect.cs
// Decision: PORT
// Category: AIUseful
// Migration: Port core engine source
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
//
// AS-IS mirror of the original DCGO SelectCardEffect (the card-selection sibling of
// SelectPermanentEffect, operating over a card ROOT zone — hand / library / trash / security / ...
// rather than the battle area). Headless port keeps the authoring shape (Mode + Root enums, SetUp) but
// is deterministic:
//   (1) BuildRequest  — enumerate the select player's cards in the Root zone, filter by the predicate,
//                       and build a Card ChoiceRequest honouring max/canNoSelect/canEndNotMax (F-2.2/F-2.4).
//   (2) BuildMutations — map the Mode to MatchStateMutation(s) per selected card (B-5: Discard = trash,
//                       AddHand = return to hand). PlayForFree/PlayForCost need the effect-Play mutation
//                       (F-3.7) and are not yet mapped.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class SelectCardEffect
{
    // 1:1 with the original SelectCardEffect.Mode.
    public enum Mode
    {
        AddHand,
        Discard,
        PlayForFree,
        PlayForCost,
        Custom,
    }

    // 1:1 with the original SelectCardEffect.Root (the source zone of the selectable cards).
    public enum Root
    {
        Library,
        Trash,
        Clock,
        Security,
        Custom,
        Hand,
        Recollection,
        Execution,
        DigivolutionCards,
        LinkedCards,
        None,
    }

    private HeadlessPlayerId _selectPlayer;
    private Func<HeadlessEntityId, bool> _canTargetCondition = static _ => true;
    private int _maxCount = 1;
    private bool _canNoSelect;
    private bool _canEndNotMax;
    private Mode _mode = Mode.Custom;
    private Root _root = Root.Hand;
    private HeadlessEntityId _sourceEntityId = new("select");
    private string _message = "Select card(s).";
    private int _playCost;

    /// <summary>(D-8) Memory cost paid per selected card in PlayForCost mode. The effect resolves the
    /// (cost-pipeline-reduced) cost via <c>ContinuousModifierGate</c> and sets it here before Apply;
    /// 0 = play for free.</summary>
    public void SetPlayCost(int memoryCost) => _playCost = memoryCost < 0 ? 0 : memoryCost;

    public void SetUp(
        HeadlessPlayerId selectPlayer,
        Func<HeadlessEntityId, bool> canTargetCondition,
        int maxCount,
        bool canNoSelect,
        bool canEndNotMax,
        Mode mode,
        Root root,
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
        _root = root;
        _sourceEntityId = sourceEntityId.IsEmpty ? new HeadlessEntityId("select") : sourceEntityId;
    }

    public void SetUpCustomMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _message = message;
        }
    }

    /// <summary>The <see cref="ChoiceZone"/> the configured <see cref="Root"/> maps to.</summary>
    public ChoiceZone RootZone => MapRoot(_root);

    /// <summary>Enumerate the select player's cards in the Root zone, filter by the predicate, and build
    /// the Card ChoiceRequest. Count rules mirror SelectPermanentEffect / the original.</summary>
    public ChoiceRequest BuildRequest(IZoneStateReader zones)
    {
        ArgumentNullException.ThrowIfNull(zones);

        ChoiceZone zone = MapRoot(_root);
        var candidates = new List<ChoiceCandidate>();
        if (zone != ChoiceZone.None)
        {
            foreach (HeadlessEntityId id in zones.GetCards(_selectPlayer, zone))
            {
                if (_canTargetCondition(id))
                {
                    candidates.Add(EffectChoiceHelpers.Candidate(id, id.Value, zone, isSelectable: true, _selectPlayer));
                }
            }
        }

        int available = candidates.Count;
        int maxCount = Math.Min(_maxCount, available);
        int minCount = _canNoSelect ? 0 : (_canEndNotMax ? Math.Min(1, maxCount) : maxCount);
        bool canSkip = _canNoSelect;

        return EffectChoiceHelpers.CreateCardRequest(_selectPlayer, _message, minCount, maxCount, canSkip, zone, candidates);
    }

    /// <summary>Map the Mode to one mutation per selected card. PlayForFree/PlayForCost need the
    /// effect-Play mutation (F-3.7); Custom yields no built-in mutation.</summary>
    public IReadOnlyList<EffectMutation> BuildMutations(IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);

        var mutations = new List<EffectMutation>();
        foreach (HeadlessEntityId card in selected)
        {
            EffectMutation? mutation = BuildMutation(card);
            if (mutation is not null)
            {
                mutations.Add(mutation);
            }
        }

        return mutations;
    }

    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        foreach (EffectMutation mutation in BuildMutations(selected))
        {
            sink.Apply(mutation);
        }
    }

    private EffectMutation? BuildMutation(HeadlessEntityId card)
    {
        return _mode switch
        {
            Mode.AddHand => Mutation(MatchStateMutationSink.ReturnToHandKind, card),
            Mode.Discard => Mutation(MatchStateMutationSink.TrashCardKind, card),
            Mode.PlayForFree => PlayMutation(card, memoryCost: 0),
            // D-8: PlayForCost pays the resolved cost (set via SetPlayCost) per played card.
            Mode.PlayForCost => PlayMutation(card, memoryCost: _playCost),
            Mode.Custom => null,
            _ => null,
        };
    }

    private EffectMutation Mutation(string kind, HeadlessEntityId card)
    {
        return new EffectMutation(
            kind,
            _sourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = card.Value,
            });
    }

    private EffectMutation PlayMutation(HeadlessEntityId card, int memoryCost)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MatchStateMutationSink.TargetEntityIdKey] = card.Value,
            [MatchStateMutationSink.FromZoneKey] = MapRoot(_root).ToString(),
        };
        if (memoryCost > 0)
        {
            values[MatchStateMutationSink.MemoryCostKey] = memoryCost;
        }

        return new EffectMutation(MatchStateMutationSink.PlayCardKind, _sourceEntityId, values);
    }

    private static ChoiceZone MapRoot(Root root)
    {
        return root switch
        {
            Root.Library => ChoiceZone.Library,
            Root.Trash => ChoiceZone.Trash,
            Root.Clock => ChoiceZone.Clock,
            Root.Security => ChoiceZone.Security,
            Root.Hand => ChoiceZone.Hand,
            Root.Recollection => ChoiceZone.Recollection,
            Root.Execution => ChoiceZone.Execution,
            Root.DigivolutionCards => ChoiceZone.DigivolutionCards,
            Root.LinkedCards => ChoiceZone.LinkedCards,
            Root.Custom => ChoiceZone.Custom,
            _ => ChoiceZone.None,
        };
    }
}
