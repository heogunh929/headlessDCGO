namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace this type-slot encoder with final RL action ids after AS-IS actions are ported.
public sealed class ActionEncoder(ActionEncodingOptions? options = null)
{
    private const string UnknownActionSlotName = "UNKNOWN";
    private readonly ActionEncodingOptions _options = options ?? ActionEncodingOptions.Default;

    public EncodedActionMask Encode(ActionMask actionMask)
    {
        ArgumentNullException.ThrowIfNull(actionMask);
        return Encode(actionMask.LegalActions);
    }

    public EncodedActionMask Encode(IEnumerable<LegalAction> legalActions)
    {
        ArgumentNullException.ThrowIfNull(legalActions);

        IReadOnlyList<string> slotNames = BuildSlotNames();
        Dictionary<string, int> slotIndexes = BuildSlotIndex(slotNames);
        List<EncodedAction> encodedActions = new();

        foreach (LegalAction legalAction in legalActions)
        {
            encodedActions.Add(EncodeAction(legalAction, slotIndexes, slotNames.Count));
        }

        return new EncodedActionMask(encodedActions.ToArray(), slotNames);
    }

    public EncodedAction EncodeAction(LegalAction legalAction)
    {
        ArgumentNullException.ThrowIfNull(legalAction);

        IReadOnlyList<string> slotNames = BuildSlotNames();
        return EncodeAction(legalAction, BuildSlotIndex(slotNames), slotNames.Count);
    }

    private EncodedAction EncodeAction(
        LegalAction legalAction,
        IReadOnlyDictionary<string, int> slotIndexes,
        int slotCount)
    {
        string normalizedType = HeadlessActionTypes.Normalize(legalAction.ActionType);
        int actionIndex = slotIndexes.TryGetValue(normalizedType, out int knownIndex)
            ? knownIndex
            : UnknownActionIndex(slotCount);

        return new EncodedAction(
            actionIndex,
            legalAction.ActionType,
            normalizedType,
            BuildEncodedKey(legalAction, actionIndex, normalizedType),
            legalAction);
    }

    private int UnknownActionIndex(int slotCount)
    {
        return _options.IncludeUnknownActionSlot ? slotCount - 1 : -1;
    }

    private string BuildEncodedKey(
        LegalAction action,
        int actionIndex,
        string normalizedActionType)
    {
        if (_options.IncludeActionIdInKey)
        {
            return $"{actionIndex}:{action.PlayerId.Value}:{normalizedActionType}:{action.Id.Value}";
        }

        return $"{actionIndex}:{action.PlayerId.Value}:{normalizedActionType}";
    }

    private IReadOnlyList<string> BuildSlotNames()
    {
        List<string> slotNames = _options.ActionTypeOrder
            .Select(HeadlessActionTypes.Normalize)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (_options.IncludeUnknownActionSlot && !slotNames.Contains(UnknownActionSlotName, StringComparer.Ordinal))
        {
            slotNames.Add(UnknownActionSlotName);
        }

        return slotNames;
    }

    private static Dictionary<string, int> BuildSlotIndex(IReadOnlyList<string> slotNames)
    {
        Dictionary<string, int> slotIndexes = new(StringComparer.Ordinal);

        for (int index = 0; index < slotNames.Count; index++)
        {
            slotIndexes[slotNames[index]] = index;
        }

        return slotIndexes;
    }
}

public sealed record ActionEncodingOptions
{
    // NOTE: DefaultActionTypeOrder must be declared before Default. Static field initializers run
    // in textual order, and Default's instance initializer reads DefaultActionTypeOrder.
    public static IReadOnlyList<string> DefaultActionTypeOrder { get; } = new[]
    {
        HeadlessActionTypes.NoOp,
        HeadlessActionTypes.Pass,
        HeadlessActionTypes.SetTerminal,
        HeadlessActionTypes.ClearTerminal,
        HeadlessActionTypes.MoveCard,
        HeadlessActionTypes.AddToHand,
        HeadlessActionTypes.AddToTrash,
        HeadlessActionTypes.AddToSecurity,
        HeadlessActionTypes.MoveToDeckTop,
        HeadlessActionTypes.MoveToDeckBottom,
        HeadlessActionTypes.DrawCards,
        HeadlessActionTypes.AddSecurityFromLibrary,
        HeadlessActionTypes.TrashSecurity,
        HeadlessActionTypes.HatchDigitama,
        HeadlessActionTypes.MoveBreedingToBattle,
        HeadlessActionTypes.DeclareAttack,
        HeadlessActionTypes.ResolveAttack,
        HeadlessActionTypes.ClearAttack,
        HeadlessActionTypes.RequestChoice,
        HeadlessActionTypes.ResolveChoice,
        HeadlessActionTypes.ClearChoice,
        HeadlessActionTypes.ShuffleDeck,
        HeadlessActionTypes.EnqueueEffect,
        HeadlessActionTypes.AdvancePhase,
        HeadlessActionTypes.EndTurn,
        HeadlessActionTypes.SetMemory,
        HeadlessActionTypes.AddMemory,
        HeadlessActionTypes.PayMemory
    };

    public static ActionEncodingOptions Default { get; } = new();

    public IReadOnlyList<string> ActionTypeOrder { get; init; } = DefaultActionTypeOrder;

    public bool IncludeUnknownActionSlot { get; init; } = true;

    public bool IncludeActionIdInKey { get; init; } = true;
}

public sealed record EncodedAction(
    int ActionIndex,
    string ActionType,
    string NormalizedActionType,
    string EncodedKey,
    LegalAction LegalAction);

public sealed record EncodedActionMask(
    IReadOnlyList<EncodedAction> LegalActions,
    IReadOnlyList<string> ActionSlotNames)
{
    public int Length => ActionSlotNames.Count;

    public bool HasAnyLegalAction => LegalActions.Count > 0;

    public bool ContainsEncodedKey(string encodedKey)
    {
        return FindByEncodedKey(encodedKey) is not null;
    }

    public bool ContainsActionId(HeadlessEntityId actionId)
    {
        return FindByActionId(actionId) is not null;
    }

    public EncodedAction? FindByEncodedKey(string encodedKey)
    {
        if (string.IsNullOrWhiteSpace(encodedKey))
        {
            return null;
        }

        return LegalActions.FirstOrDefault(action =>
            string.Equals(action.EncodedKey, encodedKey, StringComparison.Ordinal));
    }

    public EncodedAction? FindByActionId(HeadlessEntityId actionId)
    {
        return LegalActions.FirstOrDefault(action => action.LegalAction.Id == actionId);
    }

    public IReadOnlyList<EncodedAction> FindByActionIndex(int actionIndex)
    {
        return LegalActions
            .Where(action => action.ActionIndex == actionIndex)
            .ToArray();
    }

    public EncodedAction? FirstByActionIndex(int actionIndex)
    {
        return LegalActions.FirstOrDefault(action => action.ActionIndex == actionIndex);
    }

    public double[] ToMaskVector()
    {
        double[] vector = new double[Length];

        foreach (EncodedAction action in LegalActions)
        {
            if (action.ActionIndex >= 0 && action.ActionIndex < vector.Length)
            {
                vector[action.ActionIndex] = 1d;
            }
        }

        return vector;
    }

    public double[] ToCountVector()
    {
        double[] vector = new double[Length];

        foreach (EncodedAction action in LegalActions)
        {
            if (action.ActionIndex >= 0 && action.ActionIndex < vector.Length)
            {
                vector[action.ActionIndex]++;
            }
        }

        return vector;
    }
}
