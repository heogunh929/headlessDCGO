namespace HeadlessDCGO.Engine.Headless.Services;

using System.Collections.ObjectModel;

public sealed record CardRecord
{
    /// <summary>(C7) metadata key carrying ADDITIONAL card types for dual cards (AS-IS
    /// <c>CardSource.CardKinds</c> is a LIST — a hybrid dual card is BOTH Digimon and Option). The primary
    /// <see cref="CardType"/> stays single; extra kinds ride here so every type judgement sees both.</summary>
    public const string AdditionalCardTypesKey = "cardTypes";

    private string _cardNumber = string.Empty;
    private string _name = string.Empty;
    private string _cardType = "Unknown";
    private string? _evolutionCondition;
    private string? _effectBindingKey;
    private IReadOnlyDictionary<string, object?> _metadata = ReadOnlyDictionary<string, object?>.Empty;

    public CardRecord(
        HeadlessEntityId Id,
        string CardNumber,
        string Name,
        IReadOnlyDictionary<string, object?> Metadata,
        string? CardType = null,
        int? PlayCost = null,
        int? EvolutionCost = null,
        string? EvolutionCondition = null,
        string? EffectBindingKey = null)
    {
        if (Id.IsEmpty)
        {
            throw new ArgumentException("Card definition id must not be empty.", nameof(Id));
        }

        if (PlayCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PlayCost), "Play cost must not be negative.");
        }

        if (EvolutionCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(EvolutionCost), "Evolution cost must not be negative.");
        }

        this.Id = Id;
        this.CardNumber = CardNumber;
        this.Name = Name;
        this.Metadata = Metadata;
        this.CardType = CardType ?? string.Empty;
        this.PlayCost = PlayCost;
        this.EvolutionCost = EvolutionCost;
        this.EvolutionCondition = EvolutionCondition;
        this.EffectBindingKey = EffectBindingKey;
    }

    public HeadlessEntityId Id { get; init; }

    public string CardNumber
    {
        get => _cardNumber;
        init => _cardNumber = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("CardNumber must not be empty.", nameof(value))
            : value.Trim();
    }

    public string Name
    {
        get => _name;
        init => _name = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Name must not be empty.", nameof(value))
            : value.Trim();
    }

    public string CardType
    {
        get => _cardType;
        init => _cardType = string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
    }

    public int? PlayCost { get; init; }

    public int? EvolutionCost { get; init; }

    public string? EvolutionCondition
    {
        get => _evolutionCondition;
        init => _evolutionCondition = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public string? EffectBindingKey
    {
        get => _effectBindingKey;
        init => _effectBindingKey = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public IReadOnlyDictionary<string, object?> Metadata
    {
        get => _metadata;
        init => _metadata = CopyMetadata(value);
    }

    /// <summary>(C7) whether this card is of <paramref name="cardType"/> — the single printed
    /// <see cref="CardType"/> OR any additional kind under <see cref="AdditionalCardTypesKey"/> (AS-IS
    /// <c>CardKinds.Contains(kind)</c>: a dual card reports true for BOTH its kinds). Every type
    /// judgement must use this instead of comparing <see cref="CardType"/> directly.</summary>
    public bool IsCardType(string cardType)
    {
        if (string.IsNullOrWhiteSpace(cardType))
        {
            return false;
        }

        if (string.Equals(CardType, cardType, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Metadata.TryGetValue(AdditionalCardTypesKey, out object? raw) || raw is null)
        {
            return false;
        }

        return raw switch
        {
            IEnumerable<string> kinds => kinds.Any(kind => string.Equals(kind, cardType, StringComparison.OrdinalIgnoreCase)),
            string text => text
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(kind => string.Equals(kind, cardType, StringComparison.OrdinalIgnoreCase)),
            _ => false,
        };
    }

    private static IReadOnlyDictionary<string, object?> CopyMetadata(
        IReadOnlyDictionary<string, object?>? metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(metadata));
    }
}
