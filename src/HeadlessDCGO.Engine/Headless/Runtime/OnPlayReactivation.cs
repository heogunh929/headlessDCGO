namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (LA-3) The "[All Turns] (Once Per Turn) When Digimon are played, you may activate this Digimon's
/// [When Digivolving] effects" reactive window — EX8_074 region "All Turns". When any Digimon is played,
/// every battle-area card that holds such an [All Turns] re-activation (its <c>CardEffects(OnEnterFieldAnyone)</c>
/// yields a <see cref="ReuseWhenDigivolvingEffect"/>) and whose once-per-turn guard is clear gets its
/// re-activation resolved through the choice flow (<see cref="ActivatedEffectResolver"/>, full EngineContext).
/// Both players' holders are scanned (the original triggers on ANY play); the just-played card is excluded
/// (an [All Turns] holder reacts to OTHER plays, not its own entry). The optional "you may" is preserved by
/// the re-activated [When Digivolving] selections being skippable.
/// </summary>
public static class OnPlayReactivation
{
    /// <summary>Per-instance once-per-turn guard. Cleared at turn end (see <see cref="ClearAll"/>).</summary>
    public const string UsedKey = "allTurnsReactivationUsed";

    /// <summary>Resolve every eligible [All Turns] re-activation holder after <paramref name="playedCardId"/>
    /// entered play. Returns the holder id if a resolution DEFERRED for an agent choice (the caller reports
    /// pending and suspends that activation); otherwise null. No-op for boards without such a holder.</summary>
    public static async Task<HeadlessEntityId?> TryResolveAsync(
        EngineContext context,
        HeadlessEntityId playedCardId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return null;
        }

        foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
        {
            foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
            {
                if (id == playedCardId
                    || !context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? inst) || inst is null
                    || ReadFlag(inst.Metadata, UsedKey)
                    || !IsAllTurnsReactivationHolder(context, id, player))
                {
                    continue;
                }

                SetFlag(context, inst, UsedKey, true);
                try
                {
                    await ActivatedEffectResolver
                        .ResolveAsync(context, id, player, EffectTiming.OnEnterFieldAnyone, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (DeferredChoicePendingException)
                {
                    context.DeferredActivations.Suspend(id, EffectTiming.OnEnterFieldAnyone, player);
                    return id;
                }
            }
        }

        return null;
    }

    /// <summary>Clear the per-turn guard across both players' battle areas (call at turn end).</summary>
    public static void ClearAll(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return;
        }

        foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
        {
            foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
            {
                if (context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? inst) && inst is not null
                    && ReadFlag(inst.Metadata, UsedKey))
                {
                    SetFlag(context, inst, UsedKey, false);
                }
            }
        }
    }

    private static bool IsAllTurnsReactivationHolder(EngineContext context, HeadlessEntityId id, HeadlessPlayerId controller)
    {
        if (!context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? inst) || inst is null
            || !context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) || def is null
            || !CardEffectDispatch.TryCreateForCard(def, out CEntity_Effect? effect) || effect is null)
        {
            return false;
        }

        var card = new CardSource(context, id, controller, inst.OwnerId);
        foreach (ICardEffect cardEffect in effect.CardEffects(EffectTiming.OnEnterFieldAnyone, card))
        {
            if (cardEffect is ReuseWhenDigivolvingEffect)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? value) && value is true;

    private static void SetFlag(EngineContext context, CardInstanceRecord instance, string key, bool value)
    {
        var metadata = new Dictionary<string, object?>(instance.Metadata, StringComparer.Ordinal) { [key] = value };
        context.CardInstanceRepository.Upsert(instance with { Metadata = metadata });
    }
}
