namespace HeadlessDCGO.Engine.Headless.Services;

using System.Collections.ObjectModel;

public sealed record CardRecord
{
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

    private static IReadOnlyDictionary<string, object?> CopyMetadata(
        IReadOnlyDictionary<string, object?>? metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(metadata));
    }
}
