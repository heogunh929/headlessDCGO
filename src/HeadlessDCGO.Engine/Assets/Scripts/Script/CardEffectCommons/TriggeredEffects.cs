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


// (R3-C2b-2 fold) TriggeredMemoryEffect DELETED — the invented old-model "[When …] gain/lose N memory" effect
// (registry-lowering, its own scheduler resolution) is retired along with its sole factory
// CardEffectFactory.AddMemoryTriggerEffect. Every former caller (ST1_06/09, ST3_04/05, ST2_12, BT2_010/073,
// BT1_114, TfxOnDeleteGainMemory, TfxOnPlayGainMemory, and the BT1_090 EoT reversal) is now the AS-IS 1:1
// new-model inline ActivateClass memory recipe (card.Owner.AddMemory(N, activateClass) + the AS-IS Hashtable
// CanUse gate).


// (R3-F1b fold) TriggeredSetMemoryEffect DELETED — its sole factory (CardEffectFactory.SetMemoryTo3TamerEffect)
// is now the AS-IS 1:1 ActivateClass port (DCGO CardEffectFactory.cs:11). Zero remaining constructions in src or
// tests (G9-026 references the class name only in a stale comment, not a construction).


// (R3-C2b-2 fold) TriggeredGainMemoryEffect DELETED — the invented old-model "gain N memory" effect (the Tamer
// memory-gain family / the EoTLose3Memory backing) is retired. CardEffectFactory.EoTLose3Memory and the
// Gain1MemoryTamer* factories are now AS-IS 1:1 new-model ActivateClass ports (card.Owner.AddMemory(N,
// activateClass), owner-turn gate inline). Zero remaining constructions in src (tests retargeted in R3-C2b-2).


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

