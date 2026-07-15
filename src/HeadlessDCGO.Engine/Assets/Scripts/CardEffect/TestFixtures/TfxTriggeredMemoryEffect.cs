// TEST-FIXTURE effect (not a card). R3-C2b-2: the production invented old-model TriggeredMemoryEffect /
// AddMemoryTriggerEffect factory were deleted. A few fixtures (TfxOnDeleteGainMemory / TfxOnPlayGainMemory) drive
// the STILL-LIVE old-model registry-collection subsystem (AutoProcessingTriggerCollector.CollectAllTriggers,
// used by GameFlowProcessor / GameEventQueue), which reads ToBinding registry bindings — NOT the R3 GetSkillInfos
// window. Those fixtures therefore need an effect that lowers to a registry binding. This TEST-SCOPED effect (Tfx*,
// in the TestFixtures namespace, never returned by a real card) is a verbatim trim of the deleted TriggeredMemoryEffect
// so those suites keep exercising the registry path without re-introducing a production primitive.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed class TfxTriggeredMemoryEffect : ICardEffect, IHeadlessCardEffect
{
    private readonly Func<bool>? _condition;
    private readonly Func<CardEffectResolveContext, bool>? _triggerGate;

    public TfxTriggeredMemoryEffect(
        CardSource card, EffectTiming timing, int amount, bool isInheritedEffect, Func<bool>? condition, string description,
        Func<CardEffectResolveContext, bool>? triggerGate = null, int? maxCountPerTurn = null, string? hash = null, bool? isOptional = null,
        string? effectIdSuffix = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Amount = amount;
        IsInheritedEffect = isInheritedEffect;
        _condition = condition;
        _triggerGate = triggerGate;
        string trigger = EffectTimings.ToTriggerName(timing);
        var effectId = new HeadlessEntityId(string.IsNullOrWhiteSpace(effectIdSuffix)
            ? $"{card.InstanceId.Value}:mem:{trigger}:{amount}"
            : $"{card.InstanceId.Value}:mem:{trigger}:{amount}:{effectIdSuffix}");
        Definition = new CardEffectDefinition(effectId, card.InstanceId, description, trigger, isOptional: isOptional ?? (amount > 0), maxCountPerTurn: maxCountPerTurn, hash: hash);
    }

    public CardSource Card { get; }

    public int Amount { get; }

    public bool IsInheritedEffect { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_condition is not null && !_condition())
        {
            return CardEffectCanResolveResult.Failure("Trigger condition not met.");
        }

        if (_triggerGate is not null && !_triggerGate(context))
        {
            return CardEffectCanResolveResult.Failure("Trigger event condition not met.");
        }

        return CardEffectCanResolveResult.Success();
    }

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context,
        IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();

        CardEffectCanResolveResult check = CanResolve(context);
        if (!check.CanResolve)
        {
            return ValueTask.FromResult(EffectResult.Failure(check.Message ?? "Cannot resolve.", check.Values));
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = Amount };
        mutations.Apply(new EffectMutation(MatchStateMutationSink.AddMemoryKind, Definition.SourceEntityId, values));
        return ValueTask.FromResult(EffectResult.Success($"Memory {(Amount >= 0 ? "+" : string.Empty)}{Amount}."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, context),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: this, duration: null);
    }
}
