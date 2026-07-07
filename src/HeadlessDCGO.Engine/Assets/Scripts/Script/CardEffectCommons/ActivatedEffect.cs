// Uniform activated effect — a 1:1 mirror of the AS-IS ActivateClass (see
// docs/audit/uniform_activated_primitive_design.md). ONE effect type parameterised by
// (timing, CanUse gate, CanActivate precondition, composable body, once-per-turn, isOptional) replaces the
// fragmented per-(action x timing) factory set that caused the combinatorial primitive-gap explosion.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

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
