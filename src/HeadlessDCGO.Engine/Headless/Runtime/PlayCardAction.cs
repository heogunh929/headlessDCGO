namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Diagnostics.CodeAnalysis;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class PlayCardAction
{
    public IReadOnlyList<LegalAction> GetLegalActions(
        EngineContext context,
        HeadlessPlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (playerId.IsEmpty || context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return Array.Empty<LegalAction>();
        }

        return zoneReader
            .GetCards(playerId, ChoiceZone.Hand)
            .Select(cardId => CreateLegalActionIfPlayable(context, playerId, cardId))
            .Where(action => action is not null)
            .Cast<LegalAction>()
            .OrderBy(action => action.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<ActionProcessResult> ProcessAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!PlayCardActionPayload.TryRead(action, out PlayCardActionPayload? payload, out string? error))
        {
            return ActionProcessResult.Failure(error ?? "Invalid PlayCard payload.", BaseMetadata(action));
        }

        PlayCardValidation validation = Validate(context, action.PlayerId, payload);
        if (!validation.IsLegal)
        {
            return ActionProcessResult.Illegal(action, validation.Reason, Metadata(action, payload, validation));
        }

        // F-6.7: wrap the play-cost payment with the Before/AfterPayCost windows (subject = the card).
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.BeforePayCost, actor: action.PlayerId, subject: payload.CardId);

        // (EX8_074 Stage 3 brick 2) "When this card would be played" activated effects — e.g. suspend N of
        // your Digimon to reduce this card's play cost (SuspendCostReductionEffect). Resolve them BEFORE the
        // cost is locked in, then re-resolve so the reduction is actually paid. ResolveAsync is a no-op
        // (returns 0) for the vast majority of cards, which have no BeforePayCost effect — so the normal play
        // path is unchanged. NOTE: Validate already required the FULL (unreduced) cost to be payable, so this
        // brick only makes you pay LESS; offering the card when you can only afford the reduced cost is the
        // availability concern (brick 3).
        int memoryCost = payload.MemoryCost;
        try
        {
            int beforePayCostResolved = await ActivatedEffectResolver
                .ResolveAsync(context, payload.CardId, action.PlayerId, EffectTiming.BeforePayCost, cancellationToken)
                .ConfigureAwait(false);
            if (beforePayCostResolved > 0 && TryGetPlayCost(context, payload.CardId, out int reResolvedCost, out _))
            {
                memoryCost = reResolvedCost;
            }
        }
        catch (DeferredChoicePendingException ex)
        {
            // (brick 2b) The BeforePayCost effect asked an interactive provider which Digimon to suspend. The
            // resolver did NOT flush its sink and the cost is NOT yet paid — the card is still in hand, nothing
            // is partially applied. Mirror the OptionActivate deferral: record the suspended PLAY so the next
            // ResolveChoice replays the answer and FINISHES the play (pay reduced cost + move) via
            // CompleteDeferredPlayAsync through the MetadataActionProcessor resume seam (no re-validate, no
            // re-emit of BeforePayCost — commit-once across the pre-payment boundary).
            context.DeferredActivations.Suspend(payload.CardId, EffectTiming.BeforePayCost, action.PlayerId);
            Dictionary<string, object?> pending = Metadata(action, payload, validation);
            pending["pendingChoice"] = true;
            pending["pendingChoiceMessage"] = ex.Message;
            return ActionProcessResult.Success("Card play awaiting BeforePayCost choice.", pending);
        }

        return await CompletePlayAsync(context, action, payload, validation, memoryCost, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>The play tail shared by the synchronous path and the brick-2b deferred resume: pay the
    /// (already-reduced) <paramref name="memoryCost"/>, move the card to the battle area, register its
    /// effects, and run the [All Turns] reactivation window. The BeforePayCost window has already been
    /// resolved by the caller (its reduction is folded into <paramref name="memoryCost"/>).</summary>
    private static async Task<ActionProcessResult> CompletePlayAsync(
        EngineContext context,
        LegalAction action,
        PlayCardActionPayload payload,
        PlayCardValidation validation,
        int memoryCost,
        CancellationToken cancellationToken)
    {
        HeadlessMemoryState previousMemory = context.MemoryController.Current;
        HeadlessMemoryState paidMemory = context.MemoryController.Pay(memoryCost);
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.AfterPayCost, actor: action.PlayerId, subject: payload.CardId);
        // F-1.7: the fixed cost for this play is now locked in — expire one-shot "until cost is calculated"
        // modifiers (AS-IS clears Player.UntilCalculateFixedCostEffect on play).
        EffectDurationExpiry.ExpireFixedCostCalc(context.EffectRegistry);
        ZoneMoveResult movement = await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(
                action.PlayerId,
                payload.CardId,
                payload.FromZone,
                payload.ToZone),
            cancellationToken).ConfigureAwait(false);

        // N-1 (summoning sickness): a freshly-played permanent entered the field this turn and cannot
        // attack until its controller's next turn unless it has Rush. This mirrors the original
        // CardController setting Permanent.EnterFieldTurnCount = TurnCount on a newly played permanent
        // (CardController.cs:1386). Digivolve/breeding-move keep the existing permanent and so inherit
        // their prior status instead (see DigivolveAction / the breeding flow). The flag is cleared at
        // the controller's Unsuspend step (HeadlessEarlyPhaseFlow).
        MarkEnteredThisTurn(context, payload.CardId);

        // G6-001: auto-register the played card's ported effects (no-op for un-ported cards).
        CardEffectRegistrar.RegisterCard(context, payload.CardId, action.PlayerId);

        Dictionary<string, object?> metadata = Metadata(action, payload, validation);
        metadata[HeadlessActionParameterKeys.PreviousMemory] = previousMemory.Current;
        metadata[HeadlessActionParameterKeys.Memory] = paidMemory.Current;
        metadata["movementEventSequence"] = movement.Event.Sequence;
        metadata["cardDefinitionId"] = validation.CardDefinitionId?.Value;

        // LA-3: a Digimon entering play triggers eligible "[All Turns] (Once Per Turn) when Digimon are
        // played, activate this Digimon's [When Digivolving] effects" holders (both players). No-op when no
        // such holder is on the board. A deferred agent choice suspends that holder and reports pending.
        HeadlessEntityId? deferredHolder = await OnPlayReactivation
            .TryResolveAsync(context, payload.CardId, cancellationToken)
            .ConfigureAwait(false);
        if (deferredHolder is not null)
        {
            metadata["pendingChoice"] = true;
            return ActionProcessResult.Success("Card played; [All Turns] re-activation awaiting choice.", metadata);
        }

        return ActionProcessResult.Success("Card played.", metadata);
    }

    /// <summary>(brick 2b) Resume a play that suspended at its BeforePayCost choice. The MetadataActionProcessor
    /// resume seam has just re-resolved the suspended activation (replaying the agent's answer), so the cost
    /// reduction is now registered; finish the play at the reduced cost. The card is still in hand
    /// (commit-once) — this is the first and only time it is paid and moved.</summary>
    public static async Task<ActionProcessResult> CompleteDeferredPlayAsync(
        EngineContext context,
        HeadlessEntityId cardId,
        HeadlessPlayerId playerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!TryGetPlayCost(context, cardId, out int reducedCost, out string? error))
        {
            return ActionProcessResult.Failure(error ?? "Card play cost was not found.", new Dictionary<string, object?>());
        }

        context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance);
        PlayCardActionPayload payload = new(cardId, reducedCost, ChoiceZone.Hand, ChoiceZone.BattleArea);
        LegalAction action = HeadlessActionFactory.PlayCard(playerId, cardId, reducedCost);
        PlayCardValidation validation = PlayCardValidation.Legal(instance?.DefinitionId ?? cardId);
        return await CompletePlayAsync(context, action, payload, validation, reducedCost, cancellationToken).ConfigureAwait(false);
    }

    private LegalAction? CreateLegalActionIfPlayable(
        EngineContext context,
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId)
    {
        if (!TryGetPlayCost(context, cardId, out int playCost, out _))
        {
            return null;
        }

        PlayCardActionPayload payload = new(
            cardId,
            playCost,
            ChoiceZone.Hand,
            ChoiceZone.BattleArea);

        PlayCardValidation validation = Validate(context, playerId, payload);
        return validation.IsLegal
            ? HeadlessActionFactory.PlayCard(playerId, cardId, playCost)
            : null;
    }

    private static PlayCardValidation Validate(
        EngineContext context,
        HeadlessPlayerId playerId,
        PlayCardActionPayload payload)
    {
        if (playerId.IsEmpty)
        {
            return PlayCardValidation.Illegal("Player id must not be empty.");
        }

        if (payload.FromZone != ChoiceZone.Hand || payload.ToZone != ChoiceZone.BattleArea)
        {
            return PlayCardValidation.Illegal("PlayCard only supports Hand to BattleArea movement.");
        }

        if (payload.MemoryCost < 0)
        {
            return PlayCardValidation.Illegal("PlayCard memory cost must not be negative.");
        }

        if (!context.CardInstanceRepository.TryGetInstance(payload.CardId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            return PlayCardValidation.Illegal($"Card instance '{payload.CardId}' was not found.");
        }

        if (instance.OwnerId != playerId)
        {
            return PlayCardValidation.Illegal(
                $"Card instance '{payload.CardId}' is owned by player '{instance.OwnerId}', not player '{playerId}'.",
                instance.DefinitionId);
        }

        if (context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return PlayCardValidation.Illegal("Zone mover does not expose readable zone state.", instance.DefinitionId);
        }

        if (!zoneReader.GetCards(playerId, ChoiceZone.Hand).Contains(payload.CardId))
        {
            return PlayCardValidation.Illegal(
                $"Card instance '{payload.CardId}' is not in player '{playerId}' hand.",
                instance.DefinitionId);
        }

        if (!TryGetPlayCost(context, payload.CardId, out int repositoryCost, out string? costError))
        {
            return PlayCardValidation.Illegal(costError ?? "Card play cost was not found.", instance.DefinitionId);
        }

        if (context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? card) &&
            card is not null &&
            string.Equals(card.CardType, "Option", StringComparison.OrdinalIgnoreCase))
        {
            return PlayCardValidation.Illegal(
                $"Option card '{payload.CardId}' must be activated through ActivateOption.",
                instance.DefinitionId);
        }

        if (payload.MemoryCost != repositoryCost)
        {
            return PlayCardValidation.Illegal(
                $"PlayCard memory cost {payload.MemoryCost} does not match card play cost {repositoryCost}.",
                instance.DefinitionId);
        }

        // (EX8_074 Stage 3 brick 3 — availability) The original's [None] isCheckAvailability ChangeCostClass:
        // during availability calculation, a card with a passable BeforePayCost suspend-reduction is treated
        // as costing that much less, so it can be offered/played when the FULL cost is unaffordable but the
        // reduced cost is not. The payload cost stays full (the actual reduction is applied by the brick-2
        // pre-payment window in ProcessAsync); only the affordability check uses the reduced cost.
        int availabilityCost = Math.Max(0, repositoryCost - BeforePayCostAvailabilityReduction(context, payload.CardId, playerId));
        if (!context.MemoryController.CanPay(availabilityCost))
        {
            return PlayCardValidation.Illegal(
                $"Cannot pay play cost {payload.MemoryCost}.",
                instance.DefinitionId);
        }

        return PlayCardValidation.Legal(instance.DefinitionId);
    }

    /// <summary>(EX8_074 Stage 3 brick 3) The total play-cost reduction available from this card's
    /// <see cref="EffectTiming.BeforePayCost"/> activated effects whose gate currently passes — read straight
    /// from the card's effect list (the card only returns a <see cref="SuspendCostReductionEffect"/> when its
    /// gate, e.g. ">= 2 suspendable Digimon", is met). Mirrors the original's availability-check ChangeCostClass.
    /// 0 for the vast majority of cards (no BeforePayCost effect / un-ported), so the normal path is unchanged.</summary>
    private static int BeforePayCostAvailabilityReduction(EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId playerId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) || instance is null
            || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def) || def is null
            || !CardEffectDispatch.TryCreateForCard(def, out CEntity_Effect? effect) || effect is null)
        {
            return 0;
        }

        var card = new CardSource(context, cardId, playerId, instance.OwnerId);
        int reduction = 0;
        foreach (ICardEffect cardEffect in effect.CardEffects(EffectTiming.BeforePayCost, card))
        {
            if (cardEffect is SuspendCostReductionEffect suspendReduce)
            {
                reduction += suspendReduce.CostReduction;
            }
        }

        return reduction;
    }

    private static void MarkEnteredThisTurn(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            return;
        }

        Dictionary<string, object?> metadata = new(instance.Metadata, StringComparer.Ordinal)
        {
            ["enteredThisTurn"] = true
        };
        context.CardInstanceRepository.Upsert(instance with { Metadata = metadata });
    }

    private static bool TryGetPlayCost(
        EngineContext context,
        HeadlessEntityId cardId,
        out int playCost,
        out string? error)
    {
        playCost = default;
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            error = $"Card instance '{cardId}' was not found.";
            return false;
        }

        if (!context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? card) || card is null)
        {
            error = $"Card definition '{instance.DefinitionId}' was not found.";
            return false;
        }

        if (!PlayCostHelpers.TryResolveCost(card, instance, out int baseCost, out error))
        {
            return false;
        }

        // D-8: fold in continuous play-cost modifiers (effect-driven ±cost), honouring a continuous
        // "cost cannot be reduced" replacement. Static (card/instance metadata) cost is the base.
        playCost = ContinuousModifierGate.ResolvePlayCost(context, cardId, baseCost);
        error = null;
        return true;
    }

    private static Dictionary<string, object?> Metadata(
        LegalAction action,
        PlayCardActionPayload payload,
        PlayCardValidation validation)
    {
        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.CardId] = payload.CardId.Value;
        metadata[HeadlessActionParameterKeys.MemoryCost] = payload.MemoryCost;
        metadata[HeadlessActionParameterKeys.FromZone] = payload.FromZone.ToString();
        metadata[HeadlessActionParameterKeys.ToZone] = payload.ToZone.ToString();
        metadata["cardDefinitionId"] = validation.CardDefinitionId?.Value;
        return metadata;
    }

    private static Dictionary<string, object?> BaseMetadata(LegalAction action)
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.ActionId] = action.Id.Value,
            [HeadlessActionParameterKeys.PlayerId] = action.PlayerId.Value,
            [HeadlessActionParameterKeys.ActionType] = action.ActionType
        };
    }
}

public sealed record PlayCardActionPayload(
    HeadlessEntityId CardId,
    int MemoryCost,
    ChoiceZone FromZone,
    ChoiceZone ToZone)
{
    public IReadOnlyDictionary<string, object?> ToParameters()
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.CardId] = CardId,
            [HeadlessActionParameterKeys.MemoryCost] = MemoryCost,
            [HeadlessActionParameterKeys.FromZone] = FromZone,
            [HeadlessActionParameterKeys.ToZone] = ToZone
        };
    }

    public static bool TryRead(
        LegalAction action,
        [NotNullWhen(true)] out PlayCardActionPayload? payload,
        out string? error)
    {
        if (!HeadlessActionPayloadReader.TryReadEntityId(
                action,
                HeadlessActionParameterKeys.CardId,
                out HeadlessEntityId cardId,
                out error))
        {
            payload = null;
            return false;
        }

        if (!TryReadInt(action.Parameters, HeadlessActionParameterKeys.MemoryCost, out int memoryCost))
        {
            payload = null;
            error = $"Missing action parameter: {HeadlessActionParameterKeys.MemoryCost}.";
            return false;
        }

        ChoiceZone fromZone = HeadlessActionPayloadReader.ReadZoneOrDefault(
            action,
            HeadlessActionParameterKeys.FromZone,
            ChoiceZone.Hand);
        ChoiceZone toZone = HeadlessActionPayloadReader.ReadZoneOrDefault(
            action,
            HeadlessActionParameterKeys.ToZone,
            ChoiceZone.BattleArea);

        payload = new PlayCardActionPayload(cardId, memoryCost, fromZone, toZone);
        error = null;
        return true;
    }

    private static bool TryReadInt(
        IReadOnlyDictionary<string, object?> parameters,
        string key,
        out int value)
    {
        if (!parameters.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            value = default;
            return false;
        }

        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }

        if (rawValue is long longValue &&
            longValue >= int.MinValue &&
            longValue <= int.MaxValue)
        {
            value = (int)longValue;
            return true;
        }

        if (rawValue is string stringValue && int.TryParse(stringValue, out int parsedValue))
        {
            value = parsedValue;
            return true;
        }

        value = default;
        return false;
    }
}

public sealed record PlayCardValidation(
    bool IsLegal,
    string Reason,
    HeadlessEntityId? CardDefinitionId)
{
    public static PlayCardValidation Legal(HeadlessEntityId cardDefinitionId)
    {
        return new PlayCardValidation(true, string.Empty, cardDefinitionId);
    }

    public static PlayCardValidation Illegal(
        string reason,
        HeadlessEntityId? cardDefinitionId = null)
    {
        return new PlayCardValidation(false, reason ?? string.Empty, cardDefinitionId);
    }
}
