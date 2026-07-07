// Uniform activated effect — a 1:1 mirror of the AS-IS ActivateClass (see
// docs/audit/uniform_activated_primitive_design.md). ONE effect type parameterised by
// (timing, CanUse gate, CanActivate precondition, composable body, once-per-turn, isOptional) replaces the
// fragmented per-(action x timing) factory set that caused the combinatorial primitive-gap explosion.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>The composable effect body — mirrors the AS-IS ActivateCoroutine. Non-interactive bodies emit a
/// mutation in <see cref="Apply"/>; interactive bodies surface a choice via <see cref="BuildRequest"/> then act
/// on the answer in <see cref="Apply"/>.</summary>
public interface IEffectBody
{
    bool IsInteractive { get; }

    ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players);

    void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected);
}

/// <summary>&lt;Draw N&gt; (AS-IS DrawClass.Draw). Non-interactive.</summary>
public sealed class DrawBody : IEffectBody
{
    private readonly int _count;

    public DrawBody(int count) => _count = count;

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        if (_count <= 0)
        {
            return; // AS-IS: `if (_drawCount <= 0) yield break;`
        }

        sink.Apply(new EffectMutation(
            MatchStateMutationSink.DrawCardsKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.PlayerIdKey] = card.Owner,
                [MatchStateMutationSink.CountKey] = _count,
            }));
    }
}

/// <summary>Gain / lose N memory (AS-IS card.Owner.AddMemory). Non-interactive.</summary>
public sealed class MemoryBody : IEffectBody
{
    private readonly int _amount;

    public MemoryBody(int amount) => _amount = amount;

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.AddMemoryKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = _amount }));
    }
}

/// <summary>&lt;Recovery +N (Deck)&gt; — move the top N deck cards onto the owner's security stack. Non-interactive.</summary>
public sealed class RecoveryBody : IEffectBody
{
    private readonly int _amount;

    public RecoveryBody(int amount) => _amount = amount;

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.RecoverKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.PlayerIdKey] = card.Owner.Value,
                [MatchStateMutationSink.CountKey] = _amount,
            }));
    }
}

/// <summary>Trash the top/bottom N security cards of <paramref name="player"/> (AS-IS IDestroySecurity /
/// DestroySecurity coroutine). Non-interactive.</summary>
public sealed class TrashSecurityBody : IEffectBody
{
    private readonly HeadlessPlayerId _player;
    private readonly int _count;
    private readonly bool _fromTop;

    public TrashSecurityBody(HeadlessPlayerId player, int count, bool fromTop)
    {
        _player = player;
        _count = count;
        _fromTop = fromTop;
    }

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.TrashSecurityKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.PlayerIdKey] = _player.Value,
                [MatchStateMutationSink.CountKey] = _count,
                [MatchStateMutationSink.FromTopKey] = _fromTop,
            }));
    }
}

/// <summary>Pay a "trash <c>count</c> cards from your own hand" cost, then apply a self mutation
/// (<paramref name="followUpKind"/>, e.g. <see cref="MatchStateMutationSink.UnsuspendKind"/> for BT1_039
/// "trash 3 cards in your hand to unsuspend this Digimon"). Interactive: the player picks exactly
/// <c>count</c> hand cards (the caller's CanActivate has already ensured enough are held), they are trashed,
/// then the self mutation resolves — mirroring the AS-IS SelectHandEffect(discard) → follow-up coroutine.</summary>
public sealed class SelectTrashHandThenSelfMutationBody : IEffectBody
{
    private readonly int _count;
    private readonly string _followUpKind;
    private readonly string _message;

    public SelectTrashHandThenSelfMutationBody(int count, string followUpKind, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(followUpKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _count = count;
        _followUpKind = followUpKind;
        _message = message;
    }

    public bool IsInteractive => true;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players)
    {
        ArgumentNullException.ThrowIfNull(card);
        var reader = (IZoneStateReader)card.Context.ZoneMover;
        var candidates = reader.GetCards(card.Owner, ChoiceZone.Hand)
            .Select(id => EffectChoiceHelpers.Candidate(id, id.Value, ChoiceZone.Hand, isSelectable: true, card.Owner))
            .ToList();
        int max = Math.Min(_count, candidates.Count);
        if (max <= 0)
        {
            return null;
        }

        // canNoSelect:false (AS-IS) → exactly max, no skip.
        return EffectChoiceHelpers.CreatePermanentRequest(card.Owner, _message, minCount: max, maxCount: max, canSkip: false, candidates);
    }

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        // Cost: trash each selected hand card.
        foreach (HeadlessEntityId id in selected)
        {
            if (id.IsEmpty)
            {
                continue;
            }

            sink.Apply(new EffectMutation(
                MatchStateMutationSink.TrashCardKind, card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = id.Value }));
        }

        // Effect: the self mutation (e.g. unsuspend this card).
        sink.Apply(new EffectMutation(
            _followUpKind, card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.InstanceId.Value }));
    }
}

/// <summary>Pay a self-suspend cost, then gain N memory (AS-IS <c>SuspendPermanentsClass(this).Tap()</c>
/// followed by <c>card.Owner.AddMemory(N)</c>) — e.g. ST4_14 "you may suspend this Tamer to gain 1 memory".
/// The optional "may" is the activation itself (isOptional on the ActivatedEffect); the cost is applied
/// first, then the memory gain, matching the AS-IS coroutine order. Non-interactive.</summary>
public sealed class SuspendSelfAndGainMemoryBody : IEffectBody
{
    private readonly int _amount;

    public SuspendSelfAndGainMemoryBody(int amount) => _amount = amount;

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        // Cost: suspend this card's own permanent (EntityId = source, TargetEntityIdKey = the suspend target).
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.SuspendKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.InstanceId.Value }));
        // Effect: gain N memory for the card's owner (same shape as MemoryBody).
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.AddMemoryKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = _amount }));
    }
}

/// <summary>Return THIS card to its owner's hand (AS-IS AddThisCardToHand). Non-interactive.</summary>
public sealed class SelfToHandBody : IEffectBody
{
    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.ReturnToHandKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.InstanceId.Value }));
    }
}

/// <summary>Grant a continuous effect (a restriction / cost-modifier / keyword — any registered-continuous
/// <see cref="ICardEffect"/>) by registering its binding when the activated skill resolves. Subsumes the
/// "apply a CanNot* restriction / cost change via a [Main]/[Security]/trigger skill" shape — the ~18 static
/// restriction factories + the cost factories are the payload; this body just wires their registration into the
/// activation flow. Non-interactive.</summary>
public sealed class GrantContinuousBody : IEffectBody
{
    private readonly ICardEffect _continuous;

    public GrantContinuousBody(ICardEffect continuous)
    {
        ArgumentNullException.ThrowIfNull(continuous);
        _continuous = continuous;
    }

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        card.Context.EffectRegistry.Register(_continuous.ToBinding($"{card.InstanceId.Value}:grant:{_continuous.GetType().Name}"));
    }
}

/// <summary>Select up to <c>maxCount</c> matching permanents and apply a <see cref="SelectPermanentEffect.Mode"/>
/// (Destroy / Tap / UnTap / Bounce / Discard / Custom …) — the AS-IS SelectPermanentEffect coroutine. Interactive.</summary>
public sealed class SelectBody : IEffectBody
{
    private readonly SelectPermanentEffect _select = new();

    public SelectBody(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canNoSelect, bool canEndNotMax,
        SelectPermanentEffect.Mode mode, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        _select.SetUp(card.Owner, canTarget, maxCount, canNoSelect, canEndNotMax, mode, card.InstanceId);
        _select.SetUpCustomMessage(description);
    }

    public bool IsInteractive => true;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) =>
        _select.BuildRequest((IZoneStateReader)card.Context.ZoneMover, players);

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected) =>
        _select.Apply(sink, selected);
}

/// <summary>The uniform activated effect. Resolved through the activation flow (bridge / OptionActivate /
/// PlayCardAction / DigivolveAction -> ActivatedEffectResolver), which honours the gate + precondition before
/// driving the body. The trigger timing + <see cref="CanUse"/> gate carry what the AS-IS put in the timing
/// block + CanUseCondition; <see cref="MaxCountPerTurn"/> carries the AS-IS once-per-turn order.</summary>
public sealed class ActivatedEffect : IActivatedCardEffect
{
    public ActivatedEffect(
        CardSource card,
        EffectTiming timing,
        Func<CardEffectResolveContext, bool>? canUse,
        Func<bool>? canActivate,
        IEffectBody body,
        int? maxCountPerTurn,
        bool isOptional,
        string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Timing = timing;
        CanUse = canUse;
        CanActivate = canActivate;
        Body = body;
        MaxCountPerTurn = maxCountPerTurn;
        IsOptional = isOptional;
        Description = description;
        // Stable per (card, timing, body-kind) so a once-per-turn cap keys the same flag across firings.
        EffectId = new HeadlessEntityId($"{card.InstanceId.Value}:ae:{timing}:{body.GetType().Name}");
    }

    public CardSource Card { get; }

    /// <summary>Stable id for once-per-turn flag keying (the activation flow is imperative, not a registered binding).</summary>
    public HeadlessEntityId EffectId { get; }

    public EffectTiming Timing { get; }

    public Func<CardEffectResolveContext, bool>? CanUse { get; }

    public Func<bool>? CanActivate { get; }

    public IEffectBody Body { get; }

    public int? MaxCountPerTurn { get; }

    public bool IsOptional { get; }

    public string Description { get; }

    /// <summary>The gate consulted before the once-per-turn cap is consumed (AS-IS CanUseCondition +
    /// CanActivateCondition). The event subject reaches <see cref="CanUse"/> through the enriched context.</summary>
    public bool CanResolve(CardEffectResolveContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (CanUse is not null && !CanUse(context))
        {
            return false;
        }

        return CanActivate is null || CanActivate();
    }

    /// <summary>Resolve the body: interactive bodies surface a choice (skippable = optional / no selection),
    /// non-interactive bodies emit their mutation directly. The caller has already passed <see cref="CanResolve"/>
    /// and consumed any once-per-turn cap.</summary>
    public async ValueTask ResolveBodyAsync(
        MatchStateMutationSink sink,
        IChoiceProvider choices,
        IReadOnlyList<HeadlessPlayerId> players,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (Body.IsInteractive)
        {
            ChoiceRequest? request = Body.BuildRequest(Card, players);
            if (request is not null)
            {
                ChoiceResult result = await choices.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
                if (result.IsSkipped)
                {
                    return;
                }

                Body.Apply(Card, sink, result.SelectedIds);
                return;
            }
        }

        Body.Apply(Card, sink, Array.Empty<HeadlessEntityId>());
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Uniform activated effect is resolved via the activation flow, not registered: {Description}");
}
