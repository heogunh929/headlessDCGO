namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Aliased (not a namespace import) to avoid pulling the sibling `...Script.CardEffectFactory` namespace
// into scope, which would clash with the CardEffectFactory type below.
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;


/// <summary>(W6 tail) the AS-IS <c>StartOfMainAttack</c> activate body: at the owner's main-phase start,
/// open a MANDATORY attack offer for the granted Digimon (AS-IS SetCanNotSelectNotAttack — cannot decline;
/// player or any Digimon).</summary>
public sealed class StartOfMainAttackEffect : Headless.Effects.IHeadlessCardEffect
{
    private readonly EngineContext _context;
    private readonly HeadlessEntityId _attackerId;

    public StartOfMainAttackEffect(EngineContext context, HeadlessEntityId attackerId)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _attackerId = attackerId;
    }

    public Headless.Effects.CardEffectDefinition Definition => new(
        new HeadlessEntityId($"start-of-main-attack:{_attackerId.Value}"), _attackerId,
        "[Start of Your Main Phase] Attack with this Digimon.", Headless.Effects.TriggerTimings.OnStartMainPhase,
        isOptional: false);

    public Headless.Effects.CardEffectCanResolveResult CanResolve(Headless.Effects.CardEffectResolveContext context)
    {
        bool onField = _context.ZoneMover is IZoneStateReader zones &&
            _context.CardInstanceRepository.TryGetInstance(_attackerId, out CardInstanceRecord? rec) && rec is not null &&
            zones.GetCards(rec.OwnerId, ChoiceZone.BattleArea).Contains(_attackerId);
        return onField
            ? Headless.Effects.CardEffectCanResolveResult.Success()
            : Headless.Effects.CardEffectCanResolveResult.Failure("The granted Digimon is no longer on the battle area.");
    }

    public ValueTask<Headless.Effects.EffectResult> ResolveAsync(
        Headless.Effects.CardEffectResolveContext context,
        Headless.Effects.IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        Headless.Runtime.EffectDrivenAttack.RequestChoice(
            _context, _attackerId,
            new Headless.Runtime.EffectAttackOptions(WithoutTap: false, AllowPlayerTarget: true, AllowDigimonTarget: true, TargetUnsuspended: true));
        return ValueTask.FromResult(Headless.Effects.EffectResult.Success("Attack offer opened."));
    }
}


/// <summary>
/// A triggered effect that gains / loses memory when its timing fires (the common ActivateClass form
/// "[When ...] gain/lose N memory", e.g. ST1_06 / ST1_09). Carries the effect body itself so the existing
/// scheduler / resolver pipeline (TriggerEventEmitter -> AutoProcessingTriggerCollector -> EffectScheduler
/// -> CardEffectSchedulerResolver) resolves it into an AddMemory mutation on the
/// <see cref="MatchStateMutationSink"/>. The original coroutine becomes an emitted mutation (1:1 relaxed
/// for trigger plumbing).
/// </summary>
public sealed class TriggeredMemoryEffect : ICardEffect, IHeadlessCardEffect
{
    private readonly Func<bool>? _condition;
    private readonly Func<CardEffectResolveContext, bool>? _triggerGate;

    public TriggeredMemoryEffect(
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
        // The default id is DETERMINISTIC (RegisterCard idempotency). A DELAYED ONE-SHOT registration
        // (AddEffectToPlayer — AS-IS UntilEachTurnEndEffects is a LIST that stacks per activation, e.g.
        // BT1_021 attacking twice loses 6 at end of turn) passes a unique suffix so two registrations of the
        // same shape coexist.
        var effectId = new HeadlessEntityId(string.IsNullOrWhiteSpace(effectIdSuffix)
            ? $"{card.InstanceId.Value}:mem:{trigger}:{amount}"
            : $"{card.InstanceId.Value}:mem:{trigger}:{amount}:{effectIdSuffix}");
        // Gaining memory defaults to an optional "you may" prompt; a card whose trigger is mandatory passes
        // isOptional: false explicitly (e.g. ST3_04 "gain 1 memory").
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
            Card.Controller,
            Card.Owner,
            Card.InstanceId,
            triggerEntityId: null,
            targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, context),
            keywords: null,
            EffectQueryRole.None,
            Array.Empty<string>(),
            effect: this,
            duration: null);
    }
}


// (R3-F1b fold) TriggeredSetMemoryEffect DELETED — its sole factory (CardEffectFactory.SetMemoryTo3TamerEffect)
// is now the AS-IS 1:1 ActivateClass port (DCGO CardEffectFactory.cs:11). Zero remaining constructions in src or
// tests (G9-026 references the class name only in a stale comment, not a construction).


/// <summary>(PRIM-W3) A triggered "gain <see cref="Amount"/> memory (if <see cref="ExtraCondition"/> holds)"
/// effect — the Tamer memory-gain family (AS-IS Gain1MemoryTamerOpponentDigimonEffect etc.). Auto-registered
/// under its timing; resolves only on the owner's turn (and when the extra condition passes), emitting an
/// AddMemory mutation.</summary>
public sealed class TriggeredGainMemoryEffect : ICardEffect, IHeadlessCardEffect
{
    private readonly Func<bool>? _extraCondition;

    public TriggeredGainMemoryEffect(CardSource card, EffectTiming timing, int amount, string description, Func<bool>? extraCondition = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Amount = amount;
        _extraCondition = extraCondition;
        string trigger = EffectTimings.ToTriggerName(timing);
        Definition = new CardEffectDefinition(
            new HeadlessEntityId($"{card.InstanceId.Value}:gainmemory:{trigger}"), card.InstanceId, description, trigger, isOptional: false);
    }

    public CardSource Card { get; }

    public int Amount { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();

        if (Card.Context.TurnController.Current.TurnPlayerId != Card.Owner || (_extraCondition is not null && !_extraCondition()))
        {
            return ValueTask.FromResult(EffectResult.Success("Gain-memory condition not met; no change."));
        }

        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.AddMemoryKind,
            Definition.SourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = Amount }));
        return ValueTask.FromResult(EffectResult.Success($"Gain {Amount} memory."));
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


/// <summary>(PRIM-W2 #10) The one-shot OnEndBattle trigger registered by
/// <see cref="PlaySelfAtEndOfBattleSecurityEffect"/>: at the end of the current battle, play this card (from
/// security / the executing area) cost-free, then — if a delete timing was requested — mark the played Digimon
/// for a turn-end self-delete (the same marker <c>AddSelfDeleteEffect</c> uses). Removes its own binding on
/// resolution so it fires exactly once.</summary>
public sealed class PlaySelfAtEndOfBattleTriggerEffect : ICardEffect, IHeadlessCardEffect
{
    private readonly string? _deleteTiming;

    public PlaySelfAtEndOfBattleTriggerEffect(CardSource card, string? deleteTiming)
    {
        ArgumentNullException.ThrowIfNull(card);
        Card = card;
        _deleteTiming = deleteTiming;
        Definition = new CardEffectDefinition(
            new HeadlessEntityId($"{card.InstanceId.Value}:playAfterBattle"),
            card.InstanceId, "Play this card without paying its memory cost.",
            Headless.Effects.TriggerTimings.OnEndBattle, isOptional: false);
    }

    public CardSource Card { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();

        // Fire exactly once: drop this binding regardless of whether the play proceeds.
        Card.Context.EffectRegistry.RemoveWhere(b => b.Request.EffectId == Definition.EffectId);

        var zones = (IZoneStateReader)Card.Context.ZoneMover;
        ChoiceZone? from = zones.GetCards(Card.Owner, ChoiceZone.Security).Contains(Card.InstanceId) ? ChoiceZone.Security
            : zones.GetCards(Card.Owner, ChoiceZone.Execution).Contains(Card.InstanceId) ? ChoiceZone.Execution
            : (ChoiceZone?)null;
        if (from is null || !CardEffectCommons.CanPlayAsNewPermanent(Card, payCost: false, null))
        {
            return ValueTask.FromResult(EffectResult.Success("Card no longer available to play after battle."));
        }

        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.PlayCardKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = Card.InstanceId.Value,
                [MatchStateMutationSink.FromZoneKey] = from.Value.ToString(),
            }));

        if (_deleteTiming is not null &&
            Card.Context.CardInstanceRepository.TryGetInstance(Card.InstanceId, out CardInstanceRecord? rec) && rec is not null)
        {
            Card.Context.CardInstanceRepository.Upsert(rec with
            {
                Metadata = new Dictionary<string, object?>(rec.Metadata, StringComparer.Ordinal)
                {
                    [Headless.Runtime.GameFlowProcessor.DeleteAtTurnEndKey] = _deleteTiming,
                    [Headless.Runtime.GameFlowProcessor.DeleteAtTurnEndSourceKey] = Card.InstanceId.Value,
                }
            });
        }

        return ValueTask.FromResult(EffectResult.Success("Play this card at the end of the battle."));
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

