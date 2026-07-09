namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// (F-5) Shared continuous evaluation for a single card that folds in BOTH card-targeted continuous
/// effects (matched by <c>targetEntityId</c>) AND player-scope continuous effects (matched by the
/// card's owner + condition, via <see cref="PlayerScopeContinuousHelpers"/>). The continuous gates
/// (DP / restriction) call this so a "your Digimon get +1000 DP / cannot block" effect reaches every
/// applicable permanent without being individually targeted.
/// </summary>
public static class ContinuousScopeEvaluation
{
    public static ContinuousEvaluationResult EvaluateForCard(
        EngineContext context,
        string scope,
        HeadlessEntityId cardId,
        HeadlessEntityId digivolveTargetPermanentId = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var queryContext = new EffectQueryContext(scope, targetEntityId: cardId);
        ResolveCard(context, cardId, out _, out CardRecord? card, out CardInstanceRecord? instance);
        EffectRequest[] combined = ApplicableEffects(context, scope, cardId, digivolveTargetPermanentId);

        return ContinuousEffectEvaluator.Evaluate(
            new ContinuousEvaluationRequest(queryContext, combined, card, instance, state: null));
    }

    /// <summary>(FR-P3) The continuous effects that APPLY to <paramref name="cardId"/> under <paramref name="scope"/>
    /// — card-targeted + inherited + player-scope (owner + condition + arbitrary permanentCondition predicate,
    /// evaluated 1:1) — after disable/condition filtering and dynamic-value resolution. Registry-only gates
    /// (sink / battle-deletion) scan this to honour player-scope effects with predicates, not just self.</summary>
    public static EffectRequest[] ApplicableEffects(
        EngineContext context, string scope, HeadlessEntityId cardId, HeadlessEntityId digivolveTargetPermanentId = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var queryContext = new EffectQueryContext(scope, targetEntityId: cardId);
        IEffectQueryService registry = context.EffectRegistry;

        IReadOnlyList<EffectRequest> cardTargeted = registry.GetContinuousEffects(queryContext)
            .Where(effect => !IsInherited(effect))
            .ToArray();

        IReadOnlyList<EffectRequest> inherited = CollectInheritedEffects(context, registry, scope, cardId);

        ResolveCard(context, cardId, out HeadlessPlayerId owner, out CardRecord? card, out _);

        IReadOnlyList<EffectRequest> playerScoped = owner.IsEmpty
            ? Array.Empty<EffectRequest>()
            : PlayerScopeContinuousHelpers.CollectApplicable(registry, scope, owner, card, ResolveZoneName(context, owner, cardId))
                .Where(effect => PlayerScopePredicatePasses(context, effect, cardId, owner))
                .Where(effect => DigivolveTargetPredicatePasses(context, effect, cardId, digivolveTargetPermanentId, owner))
                .ToArray();

        // (faceup security) AS-IS scans `player.SecurityCards where !IsFlipped` as a continuous-effect SOURCE
        // population alongside the field permanents in every getter (DP / immunity / battle-deletion / …). Mirror
        // that live per-access scan: fold each face-up security card's continuous effects that apply to this
        // candidate (card-targeted at it, or player-scope owner+predicate matched), routed through the same
        // predicate filters as registry player-scope effects. No-op (empty) when no face-up security exists.
        IReadOnlyList<EffectRequest> faceUpSecurity = owner.IsEmpty
            ? Array.Empty<EffectRequest>()
            : CollectFaceUpSecuritySourced(context, scope, cardId, owner, card)
                .Where(effect => PlayerScopePredicatePasses(context, effect, cardId, owner))
                .Where(effect => DigivolveTargetPredicatePasses(context, effect, cardId, digivolveTargetPermanentId, owner))
                .ToArray();

        return cardTargeted
            .Concat(inherited)
            .Concat(playerScoped)
            .Concat(faceUpSecurity)
            .GroupBy(effect => effect.EffectId.Value, StringComparer.Ordinal)
            .Select(group => group.First())
            .Where(effect => !EffectInvalidation.IsEffectsDisabled(context, effect.Context.SourceEntityId))
            .Where(ConditionPasses)
            .Select(ResolveDynamicValue)
            .OrderBy(effect => effect.EffectId.Value, StringComparer.Ordinal)
            .ToArray();
    }

    // (faceup security) The continuous effects sourced by BOTH players' face-up security cards that apply to the
    // candidate: card-targeted at it, OR player-scope matched by the same coarse filter registry player-scope
    // effects use (marker + owner/any-player + zone + card-type/meta condition). The ScopePredicate is refined by
    // the caller's PlayerScopePredicatePasses, uniform with registry player-scope effects.
    private static IReadOnlyList<EffectRequest> CollectFaceUpSecuritySourced(
        EngineContext context, string scope, HeadlessEntityId candidateId, HeadlessPlayerId candidateOwner, CardRecord? candidateCard)
    {
        IReadOnlyList<HeadlessPlayerId> players = context.TurnController.Current.PlayerOrder;
        if (players.Count == 0)
        {
            return Array.Empty<EffectRequest>();
        }

        var result = new List<EffectRequest>();
        foreach (HeadlessPlayerId player in players)
        {
            foreach (HeadlessEntityId securityId in SecurityFaceState.FaceUpSecurityCards(context, player))
            {
                if (securityId == candidateId)
                {
                    continue;   // a source never scans itself as its own target
                }

                foreach (EffectRequest request in CardEffectRegistrar.BuildContinuousRequests(context, securityId, player, scope))
                {
                    if (request.Context.TargetEntityIds.Contains(candidateId))
                    {
                        result.Add(request);   // explicitly targets the candidate
                        continue;
                    }

                    IReadOnlyDictionary<string, object?> values = request.Context.Values;
                    if (!values.TryGetValue(PlayerScopeContinuousHelpers.PlayerScopeKey, out object? ps) || ps is not true)
                    {
                        continue;   // self/other card-targeted at a non-candidate — not applicable here
                    }

                    // Mirror PlayerScopeContinuousHelpers.CollectApplicable's coarse owner/zone/condition filter.
                    bool anyPlayer = values.TryGetValue(PlayerScopeContinuousHelpers.ScopeAnyPlayerKey, out object? ap) && ap is true;
                    if (!anyPlayer)
                    {
                        if (!TryScopePlayer(values, out HeadlessPlayerId scopePlayer) || scopePlayer != candidateOwner)
                        {
                            continue;
                        }
                    }

                    if (values.TryGetValue(PlayerScopeContinuousHelpers.ScopeZoneKey, out object? zoneRaw) && zoneRaw is string scopeZone
                        && !string.Equals(scopeZone, ResolveZoneName(context, candidateOwner, candidateId), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (PlayerScopeContinuousHelpers.ConditionMatches(values, candidateCard))
                    {
                        result.Add(request);
                    }
                }
            }
        }

        return result;
    }

    private static bool TryScopePlayer(IReadOnlyDictionary<string, object?> values, out HeadlessPlayerId playerId)
    {
        playerId = default;
        if (!values.TryGetValue(PlayerScopeContinuousHelpers.ScopePlayerIdKey, out object? raw) || raw is null)
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

    // The zones a player-scope effect can scope to (battle-area buffs and security-Digimon buffs). Returns
    // the zone name for ScopeZoneKey matching, or null if the card is in neither.
    private static string? ResolveZoneName(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId cardId)
    {
        var zones = (IZoneStateReader)context.ZoneMover;
        foreach (ChoiceZone zone in new[] { ChoiceZone.BattleArea, ChoiceZone.Security })
        {
            if (zones.GetCards(owner, zone).Contains(cardId))
            {
                return zone.ToString();
            }
        }

        return null;
    }

    private static bool IsInherited(EffectRequest effect) =>
        effect.Context.Values.TryGetValue(ContinuousSelfModifierEffect.InheritedEffectKey, out object? raw)
        && raw is bool flag && flag;

    private static bool ConditionPasses(EffectRequest effect)
    {
        if (effect.Context.Values.TryGetValue(ContinuousSelfModifierEffect.ConditionKey, out object? raw)
            && raw is Func<bool> condition)
        {
            return condition();
        }

        return true;
    }

    // (FR-P1) Evaluate a player-scope effect's arbitrary per-permanent predicate (permanentCondition, stored
    // as Func<CardSource,bool>) against the candidate card. No predicate = applies (subject to the coarse
    // scope already checked). This makes "your <X> Digimon get ..." target exactly the original's set.
    private static bool PlayerScopePredicatePasses(EngineContext context, EffectRequest effect, HeadlessEntityId cardId, HeadlessPlayerId owner)
    {
        if (!effect.Context.Values.TryGetValue(PlayerScopeContinuousHelpers.ScopePredicateKey, out object? raw)
            || raw is not Func<Assets.Scripts.Script.CardEffectCommons.CardSource, bool> predicate)
        {
            return true;
        }

        return predicate(new Assets.Scripts.Script.CardEffectCommons.CardSource(context, cardId, owner, owner));
    }

    // (BT1_109) Evaluate a player-scope effect's optional TWO-sided digivolution-cost predicate against
    // (the digivolving-TO card, the digivolving-FROM target permanent). Absent key -> unaffected (applies).
    // Present key but no target supplied (any non-digivolve-cost query) -> does NOT apply, keeping the effect
    // scoped to digivolution-cost resolution.
    private static bool DigivolveTargetPredicatePasses(
        EngineContext context, EffectRequest effect, HeadlessEntityId toCardId, HeadlessEntityId targetPermanentId, HeadlessPlayerId owner)
    {
        if (!effect.Context.Values.TryGetValue(PlayerScopeContinuousHelpers.ScopeDigivolveTargetPredicateKey, out object? raw)
            || raw is not Func<Assets.Scripts.Script.CardEffectCommons.CardSource, Assets.Scripts.Script.CardEffectCommons.CardSource, bool> predicate)
        {
            return true;
        }

        if (targetPermanentId.IsEmpty)
        {
            return false;
        }

        var toCard = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, toCardId, owner, owner);
        var targetCard = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, targetPermanentId, owner, owner);
        return predicate(toCard, targetCard);
    }

    private static EffectRequest ResolveDynamicValue(EffectRequest effect)
    {
        if (!effect.Context.Values.TryGetValue(ContinuousSelfModifierEffect.DynamicValueKey, out object? rawValue)
            || rawValue is not Func<int> compute
            || !effect.Context.Values.TryGetValue(ContinuousSelfModifierEffect.DynamicMetricKey, out object? rawMetric)
            || rawMetric is not string metricKey)
        {
            return effect;
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in effect.Context.Values)
        {
            if (pair.Key == ContinuousSelfModifierEffect.DynamicValueKey
                || pair.Key == ContinuousSelfModifierEffect.DynamicMetricKey)
            {
                continue;
            }

            values[pair.Key] = pair.Value;
        }

        values[metricKey] = compute();

        EffectContext source = effect.Context;
        var rewritten = new EffectContext(
            source.SourcePlayerId,
            source.OwnerPlayerId,
            source.SourceEntityId,
            source.TriggerEntityId,
            source.TargetEntityIds,
            values);
        return new EffectRequest(effect.EffectId, effect.ControllerId, effect.Timing, rewritten);
    }

    private static IReadOnlyList<EffectRequest> CollectInheritedEffects(
        EngineContext context,
        IEffectQueryService registry,
        string scope,
        HeadlessEntityId topCardId)
    {
        DigivolutionStack stack = DigivolutionStackReader.Read(
            context.CardInstanceRepository, context.CardRepository, topCardId);
        if (stack.IsEmpty)
        {
            return Array.Empty<EffectRequest>();
        }

        // Sources are considered non-flipped here; flip-state gating is a later refinement.
        IReadOnlyList<HeadlessEntityId> activeSources =
            InheritedEffectHelpers.ActiveInheritedSources(stack, _ => false, permanentIsDigimon: true);
        if (activeSources.Count == 0)
        {
            return Array.Empty<EffectRequest>();
        }

        var collected = new List<EffectRequest>();
        foreach (HeadlessEntityId source in activeSources)
        {
            IReadOnlyList<EffectRequest> sourceEffects = registry.GetContinuousEffects(
                new EffectQueryContext(scope, targetEntityId: source));
            collected.AddRange(sourceEffects.Where(IsInherited));
        }

        return collected;
    }

    private static void ResolveCard(
        EngineContext context,
        HeadlessEntityId cardId,
        out HeadlessPlayerId owner,
        out CardRecord? card,
        out CardInstanceRecord? instance)
    {
        owner = default;
        card = null;
        instance = null;

        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        instance = record;
        owner = record.OwnerId;
        if (context.CardRepository.TryGetCard(record.DefinitionId, out CardRecord? definition))
        {
            card = definition;
        }
    }
}
