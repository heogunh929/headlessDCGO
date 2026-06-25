namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public static class HeadlessActionFactory
{
    public static LegalAction Create(
        string actionType,
        HeadlessPlayerId playerId,
        string? actionId = null,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionType);

        return new LegalAction(
            BuildActionId(playerId, actionType, actionId, keyPart: null),
            playerId,
            actionType,
            parameters ?? new Dictionary<string, object?>());
    }

    public static LegalAction NoOp(HeadlessPlayerId playerId, string? actionId = null)
    {
        return Create(HeadlessActionTypes.NoOp, playerId, actionId);
    }

    public static LegalAction Pass(HeadlessPlayerId playerId, string? actionId = null)
    {
        return Create(HeadlessActionTypes.Pass, playerId, actionId);
    }

    public static LegalAction Cheat(
        HeadlessPlayerId playerId,
        string cheatType,
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.Cheat,
            playerId,
            actionId ?? BuildActionId(playerId, HeadlessActionTypes.Cheat, actionId, cheatType).Value,
            new Dictionary<string, object?>
            {
                [HeadlessActionParameterKeys.CheatType] = cheatType
            });
    }

    public static LegalAction PlayCard(
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        int memoryCost,
        ChoiceZone fromZone = ChoiceZone.Hand,
        ChoiceZone toZone = ChoiceZone.BattleArea,
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.PlayCard,
            playerId,
            actionId ?? BuildActionId(playerId, HeadlessActionTypes.PlayCard, actionId, cardId.Value).Value,
            new PlayCardActionPayload(cardId, memoryCost, fromZone, toZone).ToParameters());
    }

    public static LegalAction Digivolve(
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        HeadlessEntityId targetCardId,
        int memoryCost,
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.Digivolve,
            playerId,
            actionId ?? BuildActionId(
                playerId,
                HeadlessActionTypes.Digivolve,
                actionId,
                $"{cardId.Value}:{targetCardId.Value}").Value,
            new DigivolveActionPayload(cardId, targetCardId, memoryCost).ToParameters());
    }

    public static LegalAction ActivateOption(
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        HeadlessEntityId effectId,
        int memoryCost,
        int skillIndex = 0,
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.ActivateOption,
            playerId,
            actionId ?? BuildActionId(
                playerId,
                HeadlessActionTypes.ActivateOption,
                actionId,
                $"{cardId.Value}:{skillIndex}").Value,
            new OptionActivateActionPayload(cardId, effectId, memoryCost, skillIndex).ToParameters());
    }

    public static LegalAction SetTerminal(HeadlessPlayerId playerId, string? actionId = null)
    {
        return Create(HeadlessActionTypes.SetTerminal, playerId, actionId);
    }

    public static LegalAction SetTerminalResult(
        HeadlessPlayerId playerId,
        HeadlessPlayerId? winnerPlayerId = null,
        bool isDraw = false,
        bool isSurrender = false,
        string reason = "",
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.SetTerminal,
            playerId,
            actionId,
            new TerminalActionPayload(
                IsTerminal: true,
                WinnerPlayerId: winnerPlayerId,
                IsDraw: isDraw,
                IsSurrender: isSurrender,
                Reason: reason).ToParameters());
    }

    public static LegalAction ClearTerminal(HeadlessPlayerId playerId, string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.ClearTerminal,
            playerId,
            actionId,
            new TerminalActionPayload(IsTerminal: false).ToParameters());
    }

    public static LegalAction MoveCard(
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        ChoiceZone fromZone,
        ChoiceZone toZone,
        bool faceUp = false,
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.MoveCard,
            playerId,
            actionId ?? BuildActionId(playerId, HeadlessActionTypes.MoveCard, actionId, cardId.Value).Value,
            new MoveCardActionPayload(cardId, fromZone, toZone, faceUp).ToParameters());
    }

    public static LegalAction AddToHand(
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        string? actionId = null)
    {
        return CardAction(HeadlessActionTypes.AddToHand, playerId, cardId, actionId);
    }

    public static LegalAction AddToTrash(
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        string? actionId = null)
    {
        return CardAction(HeadlessActionTypes.AddToTrash, playerId, cardId, actionId);
    }

    public static LegalAction AddToSecurity(
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        bool faceUp = false,
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.AddToSecurity,
            playerId,
            actionId ?? BuildActionId(playerId, HeadlessActionTypes.AddToSecurity, actionId, cardId.Value).Value,
            new SecurityActionPayload(cardId, faceUp).ToParameters());
    }

    public static LegalAction MoveToDeckTop(
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        string? actionId = null)
    {
        return CardAction(HeadlessActionTypes.MoveToDeckTop, playerId, cardId, actionId);
    }

    public static LegalAction MoveToDeckBottom(
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        string? actionId = null)
    {
        return CardAction(HeadlessActionTypes.MoveToDeckBottom, playerId, cardId, actionId);
    }

    public static LegalAction DrawCards(
        HeadlessPlayerId playerId,
        int count,
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.DrawCards,
            playerId,
            actionId,
            new Dictionary<string, object?>
            {
                [HeadlessActionParameterKeys.DrawCount] = count
            });
    }

    public static LegalAction AddSecurityFromLibrary(
        HeadlessPlayerId playerId,
        int count,
        bool faceUp = false,
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.AddSecurityFromLibrary,
            playerId,
            actionId,
            new Dictionary<string, object?>
            {
                [HeadlessActionParameterKeys.SecurityCount] = count,
                [HeadlessActionParameterKeys.FaceUp] = faceUp
            });
    }

    public static LegalAction TrashSecurity(
        HeadlessPlayerId playerId,
        int count,
        bool fromTop = true,
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.TrashSecurity,
            playerId,
            actionId,
            new Dictionary<string, object?>
            {
                [HeadlessActionParameterKeys.TrashCount] = count,
                [HeadlessActionParameterKeys.FromTop] = fromTop
            });
    }

    public static LegalAction HatchDigitama(HeadlessPlayerId playerId, string? actionId = null)
    {
        return Create(HeadlessActionTypes.HatchDigitama, playerId, actionId);
    }

    public static LegalAction MoveBreedingToBattle(
        HeadlessPlayerId playerId,
        int count = 1,
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.MoveBreedingToBattle,
            playerId,
            actionId,
            new Dictionary<string, object?>
            {
                [HeadlessActionParameterKeys.BreedingMoveCount] = count
            });
    }

    public static LegalAction DeclareAttack(
        HeadlessPlayerId playerId,
        HeadlessEntityId attackerId,
        HeadlessPlayerId defendingPlayerId,
        HeadlessEntityId? targetId = null,
        bool isDirectAttack = false,
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.DeclareAttack,
            playerId,
            actionId ?? BuildActionId(
                playerId,
                HeadlessActionTypes.DeclareAttack,
                actionId,
                $"{attackerId.Value}:{targetId?.Value ?? "player"}").Value,
            new AttackActionPayload(
                attackerId,
                defendingPlayerId,
                targetId,
                isDirectAttack || !targetId.HasValue).ToParameters());
    }

    public static LegalAction ResolveAttack(
        HeadlessPlayerId playerId,
        string reason = "",
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.ResolveAttack,
            playerId,
            actionId,
            new Dictionary<string, object?>
            {
                [HeadlessActionParameterKeys.Reason] = reason
            });
    }

    public static LegalAction ClearAttack(HeadlessPlayerId playerId, string? actionId = null)
    {
        return Create(HeadlessActionTypes.ClearAttack, playerId, actionId);
    }

    public static LegalAction RequestChoice(
        HeadlessPlayerId playerId,
        ChoiceType choiceType,
        string message,
        int minCount,
        int maxCount,
        bool canSkip,
        ChoiceZone sourceZone,
        IEnumerable<HeadlessEntityId>? candidateIds = null,
        string? actionId = null)
    {
        HeadlessEntityId[] candidates = candidateIds?.ToArray() ?? Array.Empty<HeadlessEntityId>();
        return Create(
            HeadlessActionTypes.RequestChoice,
            playerId,
            actionId,
            new Dictionary<string, object?>
            {
                [HeadlessActionParameterKeys.ChoiceType] = choiceType,
                [HeadlessActionParameterKeys.ChoiceMessage] = message,
                [HeadlessActionParameterKeys.ChoiceMinCount] = minCount,
                [HeadlessActionParameterKeys.ChoiceMaxCount] = maxCount,
                [HeadlessActionParameterKeys.ChoiceCanSkip] = canSkip,
                [HeadlessActionParameterKeys.ChoiceSourceZone] = sourceZone,
                [HeadlessActionParameterKeys.ChoiceCandidateIds] = candidates
            });
    }

    public static LegalAction ResolveChoice(HeadlessPlayerId playerId, string? actionId = null)
    {
        return Create(HeadlessActionTypes.ResolveChoice, playerId, actionId);
    }

    public static LegalAction ClearChoice(HeadlessPlayerId playerId, string? actionId = null)
    {
        return Create(HeadlessActionTypes.ClearChoice, playerId, actionId);
    }

    public static LegalAction ShuffleDeck(HeadlessPlayerId playerId, string? actionId = null)
    {
        return Create(HeadlessActionTypes.ShuffleDeck, playerId, actionId);
    }

    public static LegalAction EnqueueEffect(
        HeadlessPlayerId playerId,
        HeadlessEntityId effectId,
        string timing = "Manual",
        HeadlessEntityId? sourceEntityId = null,
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.EnqueueEffect,
            playerId,
            actionId ?? BuildActionId(playerId, HeadlessActionTypes.EnqueueEffect, actionId, effectId.Value).Value,
            EffectActionPayload.Create(effectId, timing, sourceEntityId).ToParameters());
    }

    public static LegalAction AdvancePhase(HeadlessPlayerId playerId, string? actionId = null)
    {
        return Create(HeadlessActionTypes.AdvancePhase, playerId, actionId);
    }

    public static LegalAction EndTurn(HeadlessPlayerId playerId, string? actionId = null)
    {
        return Create(HeadlessActionTypes.EndTurn, playerId, actionId);
    }

    public static LegalAction SetMemory(
        HeadlessPlayerId playerId,
        int memory,
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.SetMemory,
            playerId,
            actionId,
            new Dictionary<string, object?>
            {
                [HeadlessActionParameterKeys.Memory] = memory
            });
    }

    public static LegalAction AddMemory(
        HeadlessPlayerId playerId,
        int amount,
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.AddMemory,
            playerId,
            actionId,
            new Dictionary<string, object?>
            {
                [HeadlessActionParameterKeys.MemoryAmount] = amount
            });
    }

    public static LegalAction PayMemory(
        HeadlessPlayerId playerId,
        int cost,
        string? actionId = null)
    {
        return Create(
            HeadlessActionTypes.PayMemory,
            playerId,
            actionId,
            new Dictionary<string, object?>
            {
                [HeadlessActionParameterKeys.MemoryCost] = cost
            });
    }

    private static LegalAction CardAction(
        string actionType,
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        string? actionId)
    {
        return Create(
            actionType,
            playerId,
            actionId ?? BuildActionId(playerId, actionType, actionId, cardId.Value).Value,
            new CardActionPayload(cardId).ToParameters());
    }

    private static HeadlessEntityId BuildActionId(
        HeadlessPlayerId playerId,
        string actionType,
        string? explicitActionId,
        string? keyPart)
    {
        if (!string.IsNullOrWhiteSpace(explicitActionId))
        {
            return new HeadlessEntityId(explicitActionId);
        }

        string normalizedActionType = actionType.Replace(" ", string.Empty, StringComparison.Ordinal);
        return new HeadlessEntityId(keyPart is null
            ? $"{playerId.Value}:{normalizedActionType}"
            : $"{playerId.Value}:{normalizedActionType}:{keyPart}");
    }
}
