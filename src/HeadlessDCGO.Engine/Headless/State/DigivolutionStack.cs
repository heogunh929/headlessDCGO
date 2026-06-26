namespace HeadlessDCGO.Engine.Headless.State;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// The role a card plays within a digivolution stack (G3.5-RL-B1). The original tracked these as
/// typed <c>CardSource</c>s rather than a flat id list; this restores that structure.
/// </summary>
public enum StackRole
{
    DigiEgg,
    Digivolution,
    Top
}

/// <summary>One card within a digivolution stack, with the identity/stat fields battle and the RL
/// observation need without a separate per-source lookup.</summary>
public sealed record StackedCard(
    HeadlessEntityId InstanceId,
    string CardNumber,
    StackRole Role,
    int Level,
    int BaseDp)
{
    public bool IsTop => Role == StackRole.Top;
}

/// <summary>
/// An ordered digivolution stack (bottom DigiEgg first, top Digimon last). Replaces the flat
/// <c>SourceIds</c> list so DP, level, and stack depth are first-class instead of requiring a
/// per-source repository lookup. The top card supplies the base DP for <see cref="DpCalculator"/>.
/// </summary>
public sealed record DigivolutionStack
{
    private readonly IReadOnlyList<StackedCard> _cards;

    public DigivolutionStack(IReadOnlyList<StackedCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        StackedCard[] snapshot = cards.ToArray();
        if (snapshot.Any(card => card is null))
        {
            throw new ArgumentException("Digivolution stack must not contain null cards.", nameof(cards));
        }

        if (snapshot.Select(card => card.InstanceId).Distinct().Count() != snapshot.Length)
        {
            throw new InvalidOperationException("Digivolution stack instance ids must be unique.");
        }

        if (snapshot.Length > 0)
        {
            for (int i = 0; i < snapshot.Length - 1; i++)
            {
                if (snapshot[i].Role == StackRole.Top)
                {
                    throw new InvalidOperationException("Only the topmost card may have the Top role.");
                }
            }

            if (snapshot[^1].Role != StackRole.Top)
            {
                throw new InvalidOperationException("The topmost card must have the Top role.");
            }
        }

        _cards = snapshot;
    }

    public static DigivolutionStack Empty { get; } = new(Array.Empty<StackedCard>());

    public IReadOnlyList<StackedCard> Cards => _cards;

    public int Depth => _cards.Count;

    public bool IsEmpty => _cards.Count == 0;

    public StackedCard? TopCard => _cards.Count > 0 ? _cards[^1] : null;

    /// <summary>The base (printed) DP that <see cref="DpCalculator"/> starts from — the top card's DP.</summary>
    public int BaseDp => TopCard?.BaseDp ?? 0;

    /// <summary>Cards beneath the top, i.e. the digivolution sources (eggs + lower digivolutions).</summary>
    public IReadOnlyList<StackedCard> UnderCards =>
        _cards.Count <= 1 ? Array.Empty<StackedCard>() : _cards.Take(_cards.Count - 1).ToArray();
}
