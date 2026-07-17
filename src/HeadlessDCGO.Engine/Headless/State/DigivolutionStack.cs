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

    // (RD-RLENV-03 / B2 substrate-only) Lazily cached UnderCards projection. Every stack read on the
    // hot effect-scan path (CardSource.PermanentOfThisCard and friends) touched UnderCards, and the
    // previous Take().ToArray() per ACCESS dominated allocation churn. The record is immutable after
    // construction, so the projection is computed at most once. Semantics unchanged: same content,
    // same ordering, still exposed as IReadOnlyList. (No equality/`with` consumers exist for this
    // record, so the extra cache field does not alter any observed comparison.)
    private StackedCard[]? _underCardsCache;

    public DigivolutionStack(IReadOnlyList<StackedCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        // (RD-RLENV-03 / B2 substrate-only) Validation rewritten allocation-free — this constructor was
        // the single hottest frame in the B2 profile (49% exclusive CPU: LINQ ToArray + Any + Select/
        // Distinct HashSet per construction, millions of constructions per game). The checks, their
        // ORDER, exception types and messages are IDENTICAL to the original LINQ form:
        //   1) null element        -> ArgumentException(nameof(cards))
        //   2) duplicate ids       -> InvalidOperationException
        //   3) Top role placement  -> InvalidOperationException
        int count = cards.Count;
        StackedCard[] snapshot = count == 0 ? Array.Empty<StackedCard>() : new StackedCard[count];
        for (int i = 0; i < count; i++)
        {
            snapshot[i] = cards[i];
        }

        for (int i = 0; i < count; i++)
        {
            if (snapshot[i] is null)
            {
                throw new ArgumentException("Digivolution stack must not contain null cards.", nameof(cards));
            }
        }

        // Uniqueness: stacks are shallow (a handful of cards), so a pairwise scan beats a HashSet and
        // allocates nothing. Same predicate as Select(InstanceId).Distinct().Count() != Length.
        for (int i = 1; i < count; i++)
        {
            HeadlessEntityId id = snapshot[i].InstanceId;
            for (int j = 0; j < i; j++)
            {
                if (snapshot[j].InstanceId.Equals(id))
                {
                    throw new InvalidOperationException("Digivolution stack instance ids must be unique.");
                }
            }
        }

        if (count > 0)
        {
            for (int i = 0; i < count - 1; i++)
            {
                if (snapshot[i].Role == StackRole.Top)
                {
                    throw new InvalidOperationException("Only the topmost card may have the Top role.");
                }
            }

            if (snapshot[count - 1].Role != StackRole.Top)
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
    public IReadOnlyList<StackedCard> UnderCards
    {
        get
        {
            if (_cards.Count <= 1)
            {
                return Array.Empty<StackedCard>();
            }

            // (RD-RLENV-03 / B2) Same content/order as the former Take(Count-1).ToArray() per access,
            // computed once per (immutable) stack instance.
            if (_underCardsCache is null)
            {
                var under = new StackedCard[_cards.Count - 1];
                for (int i = 0; i < under.Length; i++)
                {
                    under[i] = _cards[i];
                }

                _underCardsCache = under;
            }

            return _underCardsCache;
        }
    }
}
