namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Diagnostics.CodeAnalysis;
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

        HeadlessMemoryState previousMemory = context.MemoryController.Current;
        // F-6.7: wrap the play-cost payment with the Before/AfterPayCost windows (subject = the card).
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.BeforePayCost, actor: action.PlayerId, subject: payload.CardId);
        HeadlessMemoryState paidMemory = context.MemoryController.Pay(payload.MemoryCost);
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

        Dictionary<string, object?> metadata = Metadata(action, payload, validation);
        metadata[HeadlessActionParameterKeys.PreviousMemory] = previousMemory.Current;
        metadata[HeadlessActionParameterKeys.Memory] = paidMemory.Current;
        metadata["movementEventSequence"] = movement.Event.Sequence;
        metadata["cardDefinitionId"] = validation.CardDefinitionId?.Value;

        return ActionProcessResult.Success("Card played.", metadata);
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

        if (!context.MemoryController.CanPay(payload.MemoryCost))
        {
            return PlayCardValidation.Illegal(
                $"Cannot pay play cost {payload.MemoryCost}.",
                instance.DefinitionId);
        }

        return PlayCardValidation.Legal(instance.DefinitionId);
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
