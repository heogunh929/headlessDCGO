namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// (C-16 Overclock, S3 trait + S1 attack) At end of its controller's turn, an Overclock Digimon MAY delete
/// one OTHER owner battle-area Digimon that is a TOKEN or carries the required trait; if one is deleted, this
/// Digimon then makes an UNTAPPED attack that may only target the player. Mirrors AS-IS
/// <c>CardEffectCommons.OverclockProcess</c> — <c>CanActivateOverclock</c> (a trait/token ally != self
/// exists) → SelectPermanent (optional) → <c>DeletePeremanentAndProcessAccordingToResult</c> → if deleted,
/// <c>SelectAttackEffect</c> with <c>WithoutTap</c> + player-only. Trait matching uses the S3 metadata keys
/// (<c>trait</c>/<c>traits</c>/<c>cardTraits</c>); the ally sacrifice reuses
/// <see cref="DeletionReplacementGate.SacrificeAsync"/> (same direct sacrifice as Decoy/Scapegoat); the
/// untapped player attack reuses the S1 hub <see cref="EffectDrivenAttack"/>.
/// </summary>
public static class OverclockEffect
{
    public const string HasOverclockKey = "hasOverclock";
    public const string OverclockTraitKey = "overclockTrait";
    public const string RequestIdPrefix = "overclock";
    private const string CannotBeDeletedKey = "cannotBeDeleted";
    private static readonly string[] TraitKeys = { "trait", "traits", "cardTraits" };

    /// <summary>The owner's other battle-area Digimon eligible to be deleted: a token, or one whose top card
    /// carries the source's required trait (AS-IS <c>permanent.IsToken || ContainsTraits(trait)</c>).</summary>
    public static IReadOnlyList<HeadlessEntityId> GetTraitAllyCandidates(EngineContext context, HeadlessEntityId sourceId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ZoneMover is not IZoneStateReader zones ||
            !context.CardInstanceRepository.TryGetInstance(sourceId, out CardInstanceRecord? source) || source is null)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        string requiredTrait = ReadTrait(source.Metadata, OverclockTraitKey);
        var candidates = new List<HeadlessEntityId>();
        foreach (HeadlessEntityId candidateId in zones.GetCards(source.OwnerId, ChoiceZone.BattleArea))
        {
            if (candidateId == sourceId ||
                !context.CardInstanceRepository.TryGetInstance(candidateId, out CardInstanceRecord? ally) || ally is null ||
                ReadFlag(ally.Metadata, CannotBeDeletedKey) ||
                !IsDigimon(context, ally))
            {
                continue;
            }

            if (ally.IsToken || HasTrait(context, ally, requiredTrait))
            {
                candidates.Add(candidateId);
            }
        }

        return candidates;
    }

    /// <summary>Opens the OPTIONAL "delete a trait/token ally" choice for the Overclock controller. Returns
    /// true when a choice opened.</summary>
    public static bool RequestChoice(EngineContext context, HeadlessEntityId sourceId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ChoiceController.Current.IsPending ||
            !context.CardInstanceRepository.TryGetInstance(sourceId, out CardInstanceRecord? source) || source is null ||
            !HasOverclock(context, sourceId))
        {
            return false;
        }

        IReadOnlyList<HeadlessEntityId> candidates = GetTraitAllyCandidates(context, sourceId);
        if (candidates.Count == 0)
        {
            return false;
        }

        ChoiceCandidate[] choiceCandidates = candidates
            .Select(id => new ChoiceCandidate(id, $"Delete {id.Value} (Overclock)", ChoiceZone.BattleArea, IsSelectable: true, ownerId: source.OwnerId))
            .ToArray();

        var request = new ChoiceRequest(
            ChoiceType.OverclockTarget,
            source.OwnerId,
            "Overclock: delete a trait/token ally to make an untapped attack, or decline.",
            minCount: 0,
            maxCount: 1,
            canSkip: true,
            ChoiceZone.BattleArea,
            choiceCandidates);
        context.ChoiceController.RequestChoice(request, new HeadlessEntityId($"{RequestIdPrefix}:{sourceId.Value}"));
        return true;
    }

    /// <summary>Resolves the Overclock ally choice: delete the chosen ally and then offer this Digimon's
    /// untapped player-only attack (S1), or skip (nothing happens).</summary>
    public static async Task<bool> ResolveChoice(EngineContext context, ChoiceResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        if (!context.ChoiceController.Current.IsPending ||
            context.ChoiceController.PendingRequest is not ChoiceRequest request ||
            request.Type != ChoiceType.OverclockTarget)
        {
            return false;
        }

        HeadlessEntityId sourceId = SourceFromRequestId(context.ChoiceController.Current.RequestId ?? default);

        try
        {
            context.ChoiceController.ResolveChoice(result);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            return true;   // declined
        }

        // Delete the chosen ally (same direct sacrifice as Decoy/Scapegoat), then offer the untapped
        // player-only attack via the S1 hub (AS-IS: if deleted, SelectAttackEffect WithoutTap, player only).
        await DeletionReplacementGate.SacrificeAsync(context.CardInstanceRepository, context.ZoneMover, result.SelectedIds[0]).ConfigureAwait(false);

        EffectDrivenAttack.RequestChoice(
            context,
            sourceId,
            new EffectAttackOptions(WithoutTap: true, AllowPlayerTarget: true, AllowDigimonTarget: false, TargetUnsuspended: false));
        return true;
    }

    private static HeadlessEntityId SourceFromRequestId(HeadlessEntityId requestId)
    {
        string value = requestId.Value;
        string prefix = $"{RequestIdPrefix}:";
        return value.StartsWith(prefix, StringComparison.Ordinal)
            ? new HeadlessEntityId(value[prefix.Length..])
            : default;
    }

    private static bool HasOverclock(EngineContext context, HeadlessEntityId sourceId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(sourceId, out CardInstanceRecord? source) || source is null)
        {
            return false;
        }

        if (ReadFlag(source.Metadata, HasOverclockKey)
            || ContinuousKeywordGate.HasKeyword(context, sourceId, ContinuousKeywordGate.Overclock)) // GR-005 C-group seal
        {
            return true;
        }

        return context.CardRepository.TryGetCard(source.DefinitionId, out CardRecord? card) &&
            card is not null &&
            ReadFlag(card.Metadata, HasOverclockKey);
    }

    private static bool IsDigimon(EngineContext context, CardInstanceRecord instance) =>
        context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? card) &&
        card is not null &&
        string.Equals(card.CardType, "Digimon", StringComparison.OrdinalIgnoreCase);

    // S3 trait read: collect the instance + definition trait values (trait/traits/cardTraits keys) and match
    // the required trait case-insensitively (empty required trait = no trait gate, token-only).
    private static bool HasTrait(EngineContext context, CardInstanceRecord instance, string requiredTrait)
    {
        if (string.IsNullOrWhiteSpace(requiredTrait))
        {
            return false;
        }

        var traits = new List<string>();
        CollectTraits(instance.Metadata, traits);
        if (context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? card) && card is not null)
        {
            CollectTraits(card.Metadata, traits);
        }

        return traits.Any(t => string.Equals(t, requiredTrait, StringComparison.OrdinalIgnoreCase));
    }

    private static void CollectTraits(IReadOnlyDictionary<string, object?> metadata, List<string> sink)
    {
        foreach (string key in TraitKeys)
        {
            if (!metadata.TryGetValue(key, out object? raw) || raw is null)
            {
                continue;
            }

            switch (raw)
            {
                case string single when !string.IsNullOrWhiteSpace(single):
                    sink.AddRange(single.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case IEnumerable<string> many:
                    sink.AddRange(many.Where(value => !string.IsNullOrWhiteSpace(value)));
                    break;
            }
        }
    }

    private static string ReadTrait(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is string value ? value : string.Empty;

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is bool value && value;
}
