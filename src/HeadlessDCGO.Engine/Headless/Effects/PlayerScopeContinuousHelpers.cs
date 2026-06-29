namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (F-5) Player-scope continuous effects. A normal continuous effect targets a specific card
/// (matched by <c>TargetEntityIds</c>); a player-scope effect instead applies to EVERY one of a
/// player's permanents that meets a condition ("your [Dragon] Digimon get +1000 DP", "your opponent's
/// Digimon cannot block"). It is registered as a continuous binding whose <see cref="EffectContext"/>
/// values carry the <see cref="PlayerScopeKey"/> marker, a <see cref="ScopePlayerIdKey"/> (whose
/// permanents it affects), and optional condition keys; it has no specific target.
///
/// The continuous gates can't discover these via the per-card <c>targetEntityId</c> query, so this
/// helper collects the player-scope effects that apply to a queried card (right player + condition).
/// </summary>
public static class PlayerScopeContinuousHelpers
{
    /// <summary>Marker (bool true) identifying a player-scope continuous effect.</summary>
    public const string PlayerScopeKey = "playerScope";

    /// <summary>The player whose permanents the effect applies to.</summary>
    public const string ScopePlayerIdKey = "scopePlayerId";

    /// <summary>Optional condition: the card's <c>CardType</c> must equal this (case-insensitive).</summary>
    public const string ScopeCardTypeKey = "scopeCardType";

    /// <summary>Optional condition: the card's metadata must contain this key...</summary>
    public const string ScopeMetaKeyKey = "scopeMetaKey";

    /// <summary>...with this (string-compared) value.</summary>
    public const string ScopeMetaValueKey = "scopeMetaValue";

    /// <summary>Optional condition: the card must currently be in this zone (zone name, case-insensitive),
    /// e.g. "Security" for "your Security Digimon get +X DP". When set, the effect only applies if the
    /// evaluated card's zone is known and matches.</summary>
    public const string ScopeZoneKey = "scopeZone";

    /// <summary>
    /// Collect the player-scope continuous effects that apply to <paramref name="card"/> owned by
    /// <paramref name="cardOwner"/>: marked player-scope, scoped to that owner, and condition-matched.
    /// <paramref name="cardZoneName"/> is the evaluated card's current zone (for <see cref="ScopeZoneKey"/>
    /// filtering); null when unknown.
    /// </summary>
    public static IReadOnlyList<EffectRequest> CollectApplicable(
        IEffectQueryService effectQueryService,
        string scope,
        HeadlessPlayerId cardOwner,
        CardRecord? card,
        string? cardZoneName = null)
    {
        ArgumentNullException.ThrowIfNull(effectQueryService);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var applicable = new List<EffectRequest>();
        foreach (EffectRequest effect in effectQueryService.GetContinuousEffects(new EffectQueryContext(scope)))
        {
            IReadOnlyDictionary<string, object?> values = effect.Context.Values;
            if (!ReadBool(values, PlayerScopeKey))
            {
                continue;
            }

            if (!TryReadPlayer(values, ScopePlayerIdKey, out HeadlessPlayerId scopePlayer) || scopePlayer != cardOwner)
            {
                continue;
            }

            if (ReadString(values, ScopeZoneKey) is string scopeZone
                && !string.Equals(scopeZone, cardZoneName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (ConditionMatches(values, card))
            {
                applicable.Add(effect);
            }
        }

        return applicable;
    }

    /// <summary>Whether <paramref name="card"/> satisfies the effect's optional scope condition.</summary>
    public static bool ConditionMatches(IReadOnlyDictionary<string, object?> values, CardRecord? card)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (ReadString(values, ScopeCardTypeKey) is string cardType
            && !string.Equals(card?.CardType, cardType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ReadString(values, ScopeMetaKeyKey) is string metaKey)
        {
            string? expected = ReadString(values, ScopeMetaValueKey);
            if (card is null
                || !card.Metadata.TryGetValue(metaKey, out object? raw)
                || !string.Equals(raw?.ToString(), expected, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out object? raw) && raw is bool b && b;

    private static string? ReadString(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out object? raw) && raw is string s && !string.IsNullOrWhiteSpace(s) ? s : null;

    private static bool TryReadPlayer(IReadOnlyDictionary<string, object?> values, string key, out HeadlessPlayerId playerId)
    {
        playerId = default;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        switch (raw)
        {
            case HeadlessPlayerId typed:
                playerId = typed;
                return !typed.IsEmpty;
            case int i:
                playerId = new HeadlessPlayerId(i);
                return true;
            case long l:
                playerId = new HeadlessPlayerId((int)l);
                return true;
            case string s when int.TryParse(s, out int parsed):
                playerId = new HeadlessPlayerId(parsed);
                return true;
            default:
                return false;
        }
    }
}
