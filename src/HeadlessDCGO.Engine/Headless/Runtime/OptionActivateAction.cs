namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Diagnostics.CodeAnalysis;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class OptionActivateAction
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
            .Select(cardId => CreateLegalActionIfUsable(context, playerId, cardId))
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

        if (!OptionActivateActionPayload.TryRead(action, out OptionActivateActionPayload? payload, out string? error))
        {
            return ActionProcessResult.Failure(error ?? "Invalid ActivateOption payload.", BaseMetadata(action));
        }

        OptionActivateValidation validation = Validate(context, action.PlayerId, payload);
        if (!validation.IsLegal)
        {
            return ActionProcessResult.Illegal(action, validation.Reason, Metadata(action, payload, validation));
        }

        HeadlessMemoryState previousMemory = context.MemoryController.Current;
        // F-6.7: wrap the option-cost payment with the Before/AfterPayCost windows.
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.BeforePayCost, actor: action.PlayerId, subject: payload.CardId);
        HeadlessMemoryState paidMemory = context.MemoryController.Pay(payload.MemoryCost);
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.AfterPayCost, actor: action.PlayerId, subject: payload.CardId);
        // F-1.7: fixed cost locked — expire one-shot "until cost is calculated" modifiers.
        EffectDurationExpiry.ExpireFixedCostCalc(context.EffectRegistry);
        ZoneMoveResult movement = await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(
                action.PlayerId,
                payload.CardId,
                ChoiceZone.Hand,
                ChoiceZone.Trash,
                FaceUp: true),
            cancellationToken).ConfigureAwait(false);

        // F-6.6: opening an Option card opens the OnUseOption window (subject = the option card).
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnUseOption, actor: action.PlayerId, subject: payload.CardId);

        EffectContext effectContext = new(
            action.PlayerId,
            action.PlayerId,
            payload.CardId,
            payload.CardId,
            new[] { payload.CardId },
            Metadata(action, payload, validation));
        context.EffectScheduler.Enqueue(new EffectRequest(
            payload.EffectId,
            action.PlayerId,
            "OptionActivate",
            effectContext));

        Dictionary<string, object?> metadata = Metadata(action, payload, validation);
        metadata[HeadlessActionParameterKeys.PreviousMemory] = previousMemory.Current;
        metadata[HeadlessActionParameterKeys.Memory] = paidMemory.Current;
        metadata["movementEventSequence"] = movement.Event.Sequence;
        metadata["pendingEffectCount"] = context.EffectScheduler.PendingCount;
        metadata["totalEnqueuedEffectCount"] = context.EffectScheduler.TotalEnqueuedCount;

        return ActionProcessResult.Success("Option activated.", metadata);
    }

    private LegalAction? CreateLegalActionIfUsable(
        EngineContext context,
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId)
    {
        if (!TryReadOptionCard(context, cardId, out CardRecord? card, out _))
        {
            return null;
        }

        _ = context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance);
        int memoryCost = ResolveOptionCost(context, cardId, card, instance);
        HeadlessEntityId effectId = ResolveEffectId(card);
        OptionActivateActionPayload payload = new(cardId, effectId, memoryCost, SkillIndex: 0);
        OptionActivateValidation validation = Validate(context, playerId, payload);
        return validation.IsLegal
            ? HeadlessActionFactory.ActivateOption(playerId, cardId, effectId, memoryCost)
            : null;
    }

    private static OptionActivateValidation Validate(
        EngineContext context,
        HeadlessPlayerId playerId,
        OptionActivateActionPayload payload)
    {
        if (playerId.IsEmpty)
        {
            return OptionActivateValidation.Illegal("Player id must not be empty.");
        }

        if (payload.MemoryCost < 0)
        {
            return OptionActivateValidation.Illegal("Option memory cost must not be negative.");
        }

        if (payload.SkillIndex < 0)
        {
            return OptionActivateValidation.Illegal("Option skill index must not be negative.");
        }

        if (!context.CardInstanceRepository.TryGetInstance(payload.CardId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            return OptionActivateValidation.Illegal($"Card instance '{payload.CardId}' was not found.");
        }

        if (instance.OwnerId != playerId)
        {
            return OptionActivateValidation.Illegal(
                $"Card instance '{payload.CardId}' is owned by player '{instance.OwnerId}', not player '{playerId}'.",
                instance.DefinitionId);
        }

        if (context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return OptionActivateValidation.Illegal("Zone mover does not expose readable zone state.", instance.DefinitionId);
        }

        if (!zoneReader.GetCards(playerId, ChoiceZone.Hand).Contains(payload.CardId))
        {
            return OptionActivateValidation.Illegal(
                $"Card instance '{payload.CardId}' is not in player '{playerId}' hand.",
                instance.DefinitionId);
        }

        if (!TryReadOptionCard(context, payload.CardId, out CardRecord? card, out string? cardError))
        {
            return OptionActivateValidation.Illegal(cardError ?? "Option card was not found.", instance.DefinitionId);
        }

        if (IsOptionLocked(instance, card))
        {
            return OptionActivateValidation.Illegal(
                $"Option card '{payload.CardId}' cannot be activated.",
                instance.DefinitionId,
                payload.EffectId);
        }

        int cardCost = ResolveOptionCost(context, payload.CardId, card, instance);
        if (payload.MemoryCost != cardCost)
        {
            return OptionActivateValidation.Illegal(
                $"Option memory cost {payload.MemoryCost} does not match card play cost {cardCost}.",
                instance.DefinitionId,
                payload.EffectId);
        }

        HeadlessEntityId expectedEffectId = ResolveEffectId(card);
        if (payload.EffectId != expectedEffectId)
        {
            return OptionActivateValidation.Illegal(
                $"Option effect id '{payload.EffectId}' does not match card effect '{expectedEffectId}'.",
                instance.DefinitionId,
                expectedEffectId);
        }

        if (!context.MemoryController.CanPay(payload.MemoryCost))
        {
            return OptionActivateValidation.Illegal(
                $"Cannot pay option cost {payload.MemoryCost}.",
                instance.DefinitionId,
                payload.EffectId);
        }

        return OptionActivateValidation.Legal(instance.DefinitionId, payload.EffectId);
    }

    private static bool TryReadOptionCard(
        EngineContext context,
        HeadlessEntityId cardId,
        [NotNullWhen(true)] out CardRecord? card,
        out string? error)
    {
        card = null;
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            error = $"Card instance '{cardId}' was not found.";
            return false;
        }

        if (!context.CardRepository.TryGetCard(instance.DefinitionId, out card) || card is null)
        {
            error = $"Card definition '{instance.DefinitionId}' was not found.";
            return false;
        }

        if (!string.Equals(card.CardType, "Option", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Card definition '{instance.DefinitionId}' is not an Option card.";
            card = null;
            return false;
        }

        error = null;
        return true;
    }

    private static HeadlessEntityId ResolveEffectId(CardRecord card)
    {
        return new HeadlessEntityId(
            string.IsNullOrWhiteSpace(card.EffectBindingKey)
                ? $"{card.Id.Value}:option"
                : card.EffectBindingKey);
    }

    // D-8: static option play cost + continuous ±cost modifiers (cannot-reduce replacement honoured).
    // Used by both legal-action generation and validation so the offered and checked costs match.
    private static int ResolveOptionCost(EngineContext context, HeadlessEntityId cardId, CardRecord card, CardInstanceRecord? instance)
    {
        int baseCost = PlayCostHelpers.TryResolveCost(card, instance, out int resolved, out _) ? resolved : 0;
        return ContinuousModifierGate.ResolvePlayCost(context, cardId, baseCost);
    }

    private static bool IsOptionLocked(CardInstanceRecord instance, CardRecord card)
    {
        return ReadBool(instance.Metadata, "canNotPlayThisOption") ||
            ReadBool(instance.Metadata, "cannotActivateOption") ||
            ReadBool(card.Metadata, "canNotPlayThisOption") ||
            ReadBool(card.Metadata, "cannotActivateOption");
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return false;
        }

        return rawValue switch
        {
            bool value => value,
            string value => bool.TryParse(value, out bool parsed) && parsed,
            _ => false
        };
    }

    private static Dictionary<string, object?> Metadata(
        LegalAction action,
        OptionActivateActionPayload payload,
        OptionActivateValidation validation)
    {
        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.CardId] = payload.CardId.Value;
        metadata[HeadlessActionParameterKeys.EffectId] = payload.EffectId.Value;
        metadata[HeadlessActionParameterKeys.MemoryCost] = payload.MemoryCost;
        metadata[HeadlessActionParameterKeys.SkillIndex] = payload.SkillIndex;
        metadata[HeadlessActionParameterKeys.FromZone] = ChoiceZone.Hand.ToString();
        metadata[HeadlessActionParameterKeys.ToZone] = ChoiceZone.Trash.ToString();
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

public sealed record OptionActivateActionPayload(
    HeadlessEntityId CardId,
    HeadlessEntityId EffectId,
    int MemoryCost,
    int SkillIndex)
{
    public IReadOnlyDictionary<string, object?> ToParameters()
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.CardId] = CardId,
            [HeadlessActionParameterKeys.EffectId] = EffectId,
            [HeadlessActionParameterKeys.MemoryCost] = MemoryCost,
            [HeadlessActionParameterKeys.SkillIndex] = SkillIndex
        };
    }

    public static bool TryRead(
        LegalAction action,
        [NotNullWhen(true)] out OptionActivateActionPayload? payload,
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

        if (!HeadlessActionPayloadReader.TryReadEntityId(
                action,
                HeadlessActionParameterKeys.EffectId,
                out HeadlessEntityId effectId,
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

        int skillIndex = TryReadInt(action.Parameters, HeadlessActionParameterKeys.SkillIndex, out int parsedSkillIndex)
            ? parsedSkillIndex
            : 0;

        payload = new OptionActivateActionPayload(cardId, effectId, memoryCost, skillIndex);
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

public sealed record OptionActivateValidation(
    bool IsLegal,
    string Reason,
    HeadlessEntityId? CardDefinitionId,
    HeadlessEntityId? EffectId)
{
    public static OptionActivateValidation Legal(
        HeadlessEntityId cardDefinitionId,
        HeadlessEntityId effectId)
    {
        return new OptionActivateValidation(true, string.Empty, cardDefinitionId, effectId);
    }

    public static OptionActivateValidation Illegal(
        string reason,
        HeadlessEntityId? cardDefinitionId = null,
        HeadlessEntityId? effectId = null)
    {
        return new OptionActivateValidation(false, reason ?? string.Empty, cardDefinitionId, effectId);
    }
}
