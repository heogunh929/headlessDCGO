namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Diagnostics.CodeAnalysis;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
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
        int memoryCost = ResolveOptionCost(context, cardId, instance);
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

        // (OPTION-GATE re-migration) AS-IS <c>CardSource.CanNotPlayThisOption</c> (CardSource.cs:184-249) in ONE
        // getter: the three ICanNotPlayCardEffect regions ①②③ FIRST, then the colour requirement
        // (!MatchColorRequirement). The substrate pair `CanNotPlayOptionScan.CanNotPlay` +
        // `!OptionColorRequirement.Matches` was exactly that getter split in two (the colour half already
        // delegated to the mirror getter, DEF-S9); the mirror getter is now called directly, so the AS-IS order
        // and the AS-IS non-Option early-out are inherited rather than re-implemented. The two split failure
        // messages collapse into the one AS-IS predicate.
        if (new Assets.Scripts.Script.CardEffectCommons.CardSource(context, payload.CardId, playerId, playerId).CanNotPlayThisOption)
        {
            return OptionActivateValidation.Illegal(
                $"Option card '{payload.CardId}' cannot be played (a 'cannot play Option' effect is active, or its colour requirement is not met).",
                instance.DefinitionId,
                payload.EffectId);
        }

        int cardCost = ResolveOptionCost(context, payload.CardId, instance);
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

        if (!card.IsCardType("Option"))
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

    // The option's play cost, used by both legal-action generation and validation so the offered and checked
    // costs match. (PlayCostHelpers retirement) The invented metadata-modifier pre-fold (PlayCostHelpers
    // .TryResolveCost: `playCostModifiers` / `fixedPlayCost` metadata read) is RETIRED — AS-IS has no such
    // stage. AS-IS <c>CardSource.PayingCost(root, targetPermanents)</c> (CardSource.cs:635-658) takes the
    // PRINTED base cost (<c>_cEntity_Base.PlayCost</c>) straight into GetPayingCostWithBaseCost's
    // ChangeCostClass fold; an option is played from hand (Root.Hand) with no digivolution target.
    private static int ResolveOptionCost(EngineContext context, HeadlessEntityId cardId, CardInstanceRecord? instance)
    {
        HeadlessPlayerId owner = instance?.OwnerId ?? default;
        return new CardSource(context, cardId, owner)
            .PayingCost(Assets.Scripts.Script.SelectCardEffect.Root.Hand, targetPermanents: null);
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
