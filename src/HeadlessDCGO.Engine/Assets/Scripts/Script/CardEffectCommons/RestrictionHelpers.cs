namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using HeadlessDCGO.Engine.Headless.Effects;

public enum CannotRestrictionKind
{
    Attack = 0,
    Block = 1,
    Delete = 2,
    ReturnToHand = 3,
    ReturnToDeck = 4,
    Suspend = 5,
    // D-A5: "this Digimon cannot digivolve" continuous restriction.
    Digivolve = 6,
    // (PRIM-W3) continuous restrictions consulted by the unsuspend step / block gate / effect-delete path.
    Unsuspend = 7,
    BeBlocked = 8,
    DeleteBySkill = 9,
    // (PRIM-W4) "this Digimon cannot be attacked" — consulted by AttackPermanentAction on the defender.
    BeAttacked = 10,
}

public sealed record CannotRestriction
{
    public CannotRestriction(
        string id,
        CannotRestrictionKind kind,
        HeadlessEntityId? targetEntityId = null,
        HeadlessEntityId? sourceEntityId = null,
        string? reason = null,
        bool requiresAvailabilityCheck = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Restriction kind must be known.");
        }

        if (targetEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Restriction target id must not be empty.", nameof(targetEntityId));
        }

        if (sourceEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Restriction source id must not be empty.", nameof(sourceEntityId));
        }

        Id = id.Trim();
        Kind = kind;
        TargetEntityId = targetEntityId;
        SourceEntityId = sourceEntityId;
        Reason = string.IsNullOrWhiteSpace(reason) ? DefaultReason(kind) : reason.Trim();
        RequiresAvailabilityCheck = requiresAvailabilityCheck;
    }

    public string Id { get; }

    public CannotRestrictionKind Kind { get; }

    public HeadlessEntityId? TargetEntityId { get; }

    public HeadlessEntityId? SourceEntityId { get; }

    public string Reason { get; }

    public bool RequiresAvailabilityCheck { get; }

    public static CannotRestriction ForTarget(
        string id,
        CannotRestrictionKind kind,
        HeadlessEntityId targetEntityId,
        string? reason = null,
        HeadlessEntityId? sourceEntityId = null)
    {
        return new CannotRestriction(id, kind, targetEntityId, sourceEntityId, reason);
    }

    private static string DefaultReason(CannotRestrictionKind kind)
    {
        return kind switch
        {
            CannotRestrictionKind.Attack => "Target cannot attack.",
            CannotRestrictionKind.Block => "Target cannot block.",
            CannotRestrictionKind.Delete => "Target cannot be deleted.",
            CannotRestrictionKind.ReturnToHand => "Target cannot return to hand.",
            CannotRestrictionKind.ReturnToDeck => "Target cannot return to deck.",
            CannotRestrictionKind.Suspend => "Target cannot suspend.",
            _ => "Target is restricted.",
        };
    }
}

public sealed record CannotRestrictionRequest
{
    public CannotRestrictionRequest(
        CannotRestrictionKind kind,
        HeadlessEntityId targetEntityId,
        IReadOnlyList<CannotRestriction>? restrictions = null,
        HeadlessEntityId? sourceEntityId = null,
        bool checkAvailability = false)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Restriction kind must be known.");
        }

        if (targetEntityId.IsEmpty)
        {
            throw new ArgumentException("Restriction request target id must not be empty.", nameof(targetEntityId));
        }

        if (sourceEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Restriction request source id must not be empty.", nameof(sourceEntityId));
        }

        Kind = kind;
        TargetEntityId = targetEntityId;
        Restrictions = Array.AsReadOnly((restrictions ?? Array.Empty<CannotRestriction>()).ToArray());
        SourceEntityId = sourceEntityId;
        CheckAvailability = checkAvailability;
    }

    public CannotRestrictionKind Kind { get; }

    public HeadlessEntityId TargetEntityId { get; }

    public IReadOnlyList<CannotRestriction> Restrictions { get; }

    public HeadlessEntityId? SourceEntityId { get; }

    public bool CheckAvailability { get; }
}

public sealed record CannotRestrictionResult
{
    private CannotRestrictionResult(
        bool isRestricted,
        string reason,
        IReadOnlyList<string> appliedRestrictionIds,
        IReadOnlyList<string> skippedRestrictionIds,
        IReadOnlyDictionary<string, object?> values)
    {
        IsRestricted = isRestricted;
        Reason = reason;
        AppliedRestrictionIds = Array.AsReadOnly(appliedRestrictionIds.ToArray());
        SkippedRestrictionIds = Array.AsReadOnly(skippedRestrictionIds.ToArray());
        Values = CopyValues(values);
    }

    public bool IsRestricted { get; }

    public string Reason { get; }

    public IReadOnlyList<string> AppliedRestrictionIds { get; }

    public IReadOnlyList<string> SkippedRestrictionIds { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static CannotRestrictionResult Success(
        bool isRestricted,
        string reason,
        IReadOnlyList<string> appliedRestrictionIds,
        IReadOnlyList<string> skippedRestrictionIds,
        IReadOnlyDictionary<string, object?> values)
    {
        return new CannotRestrictionResult(isRestricted, reason, appliedRestrictionIds, skippedRestrictionIds, values);
    }

    private static IReadOnlyDictionary<string, object?> CopyValues(IReadOnlyDictionary<string, object?> values)
    {
        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}

public static class RestrictionHelpers
{
    public const string RestrictionsKey = "cannotRestrictions";
    public const string RestrictionKindKey = "restrictionKind";
    public const string RestrictionTargetEntityIdKey = "targetEntityId";
    public const string RestrictionSourceEntityIdKey = "sourceEntityId";
    public const string RestrictionReasonKey = "reason";
    public const string CannotAttackKey = "cannotAttack";
    public const string CannotBlockKey = "cannotBlock";
    public const string CannotDeleteKey = "cannotDelete";
    public const string CannotBeDeletedKey = "cannotBeDeleted";
    public const string CannotReturnToHandKey = "cannotReturnToHand";
    public const string CannotReturnToDeckKey = "cannotReturnToDeck";
    public const string CannotReturnToLibraryKey = "cannotReturnToLibrary";
    public const string CannotSuspendKey = "cannotSuspend";
    public const string CannotDigivolveKey = "cannotDigivolve";
    public const string CannotUnsuspendKey = "cannotUnsuspend";
    public const string CannotBeBlockedKey = "cannotBeBlocked";
    public const string CannotBeDeletedBySkillKey = "cannotBeDeletedBySkill";
    public const string CannotBeAttackedKey = "cannotBeAttacked";
    // (FR-P3) attached to a CannotAttack restriction: only defenders matching this predicate are off-limits
    // (AS-IS CanNotAttackTargetDefendingPermanent's defenderCondition). Value: Func<CardSource,bool>.
    public const string DefenderPredicateKey = "defenderPredicate";

    /// <summary>(W6-G) the restriction's COUNTERPART predicate — for CannotBlock/CannotBeAttacked the
    /// ATTACKER filter, for CannotBeBlocked the BLOCKER filter (AS-IS attackerCondition/defenderCondition
    /// on the Gain grants). Stored as a <c>Func&lt;CardSource,bool&gt;</c> over the counterpart's top card.</summary>
    public const string CounterpartPredicateKey = "restriction.counterpartPredicate";

    // (FR2/M-2) attached to a return/trash restriction: the restriction only blocks effects whose CAUSING
    // effect matches this predicate (AS-IS cardEffectCondition, e.g. IsOpponentEffect = "cannot be returned/
    // trashed by the OPPONENT's effects, but may be by your own"). Evaluated against the causing effect's
    // SOURCE card. Value: Func<CardSource,bool>.
    public const string CausingEffectPredicateKey = "causingEffectPredicate";

    public static CannotRestrictionResult Evaluate(CannotRestrictionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        CannotRestriction[] relevant = request.Restrictions
            .Where(restriction => restriction.Kind == request.Kind)
            .OrderBy(restriction => restriction.Id, StringComparer.Ordinal)
            .ToArray();
        var applied = new List<string>();
        var skipped = new List<string>();
        var reasons = new List<string>();

        foreach (CannotRestriction restriction in relevant)
        {
            if (!CanApply(restriction, request))
            {
                skipped.Add(restriction.Id);
                continue;
            }

            applied.Add(restriction.Id);
            reasons.Add(restriction.Reason);
        }

        bool isRestricted = applied.Count > 0;
        string reason = isRestricted
            ? string.Join(" ", reasons.Distinct(StringComparer.Ordinal))
            : "No restriction matched.";

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["kind"] = request.Kind.ToString(),
            ["targetEntityId"] = request.TargetEntityId.Value,
            ["checkAvailability"] = request.CheckAvailability,
            ["restrictionCount"] = request.Restrictions.Count,
            ["appliedRestrictionIds"] = applied.ToArray(),
            ["skippedRestrictionIds"] = skipped.ToArray(),
            ["isRestricted"] = isRestricted,
            ["reason"] = reason,
        };

        if (request.SourceEntityId is HeadlessEntityId sourceEntityId)
        {
            values["sourceEntityId"] = sourceEntityId.Value;
        }

        return CannotRestrictionResult.Success(isRestricted, reason, applied, skipped, values);
    }

    public static bool IsRestricted(CannotRestrictionRequest request)
    {
        return Evaluate(request).IsRestricted;
    }

    public static IReadOnlyList<CannotRestriction> ReadRestrictions(
        CardRecord? card = null,
        CardInstanceRecord? instance = null,
        CardInstanceState? state = null,
        IEnumerable<EffectRequest>? effectRequests = null)
    {
        var restrictions = new List<CannotRestriction>();
        if (card is not null)
        {
            restrictions.AddRange(ReadRestrictionsFromValues(card.Metadata));
        }

        if (instance is not null)
        {
            restrictions.AddRange(ReadRestrictionsFromValues(instance.Metadata));
        }

        if (state is not null)
        {
            restrictions.AddRange(ReadRestrictionsFromValues(state.Modifiers));
            restrictions.AddRange(ReadRestrictionsFromFlags(state.Flags));
        }

        if (effectRequests is not null)
        {
            foreach (EffectRequest request in effectRequests)
            {
                restrictions.AddRange(ReadRestrictionsFromValues(request.Context.Values, request.EffectId));
            }
        }

        return restrictions
            .OrderBy(restriction => restriction.Kind)
            .ThenBy(restriction => restriction.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<CannotRestriction> QueryRestrictions(
        IEffectQueryService effectQueryService,
        EffectQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(effectQueryService);
        ArgumentNullException.ThrowIfNull(context);
        return ReadRestrictions(effectRequests: effectQueryService.GetRestrictionEffects(context));
    }

    public static CannotRestrictionResult CannotAttack(
        HeadlessEntityId attackerId,
        IReadOnlyList<CannotRestriction> restrictions,
        HeadlessEntityId? defenderId = null)
    {
        return Evaluate(new CannotRestrictionRequest(CannotRestrictionKind.Attack, attackerId, restrictions, defenderId));
    }

    public static CannotRestrictionResult CannotBlock(
        HeadlessEntityId blockerId,
        IReadOnlyList<CannotRestriction> restrictions,
        HeadlessEntityId? attackerId = null)
    {
        return Evaluate(new CannotRestrictionRequest(CannotRestrictionKind.Block, blockerId, restrictions, attackerId));
    }

    public static CannotRestrictionResult CannotDelete(
        HeadlessEntityId targetId,
        IReadOnlyList<CannotRestriction> restrictions,
        HeadlessEntityId? sourceEntityId = null)
    {
        return Evaluate(new CannotRestrictionRequest(CannotRestrictionKind.Delete, targetId, restrictions, sourceEntityId));
    }

    public static CannotRestrictionResult CannotReturnToHand(
        HeadlessEntityId targetId,
        IReadOnlyList<CannotRestriction> restrictions,
        HeadlessEntityId? sourceEntityId = null)
    {
        return Evaluate(new CannotRestrictionRequest(CannotRestrictionKind.ReturnToHand, targetId, restrictions, sourceEntityId));
    }

    public static CannotRestrictionResult CannotReturnToDeck(
        HeadlessEntityId targetId,
        IReadOnlyList<CannotRestriction> restrictions,
        HeadlessEntityId? sourceEntityId = null)
    {
        return Evaluate(new CannotRestrictionRequest(CannotRestrictionKind.ReturnToDeck, targetId, restrictions, sourceEntityId));
    }

    public static CannotRestrictionResult CannotSuspend(
        HeadlessEntityId targetId,
        IReadOnlyList<CannotRestriction> restrictions,
        HeadlessEntityId? sourceEntityId = null)
    {
        return Evaluate(new CannotRestrictionRequest(CannotRestrictionKind.Suspend, targetId, restrictions, sourceEntityId));
    }

    // D-A5: "this Digimon cannot digivolve". targetEntityId is the digivolve TARGET (the under-card being
    // evolved); a restriction scoped to a specific source may pass sourceEntityId for matching.
    public static CannotRestrictionResult CannotDigivolve(
        HeadlessEntityId targetId,
        IReadOnlyList<CannotRestriction> restrictions,
        HeadlessEntityId? sourceEntityId = null)
    {
        return Evaluate(new CannotRestrictionRequest(CannotRestrictionKind.Digivolve, targetId, restrictions, sourceEntityId));
    }

    // (PRIM-W3) "this Digimon does not unsuspend" — consulted by the Unsuspend step.
    public static CannotRestrictionResult CannotUnsuspend(
        HeadlessEntityId targetId,
        IReadOnlyList<CannotRestriction> restrictions,
        HeadlessEntityId? sourceEntityId = null)
    {
        return Evaluate(new CannotRestrictionRequest(CannotRestrictionKind.Unsuspend, targetId, restrictions, sourceEntityId));
    }

    // (PRIM-W3) "this attacker cannot be blocked" — consulted when enumerating blocker candidates.
    public static CannotRestrictionResult CannotBeBlocked(
        HeadlessEntityId attackerId,
        IReadOnlyList<CannotRestriction> restrictions,
        HeadlessEntityId? sourceEntityId = null)
    {
        return Evaluate(new CannotRestrictionRequest(CannotRestrictionKind.BeBlocked, attackerId, restrictions, sourceEntityId));
    }

    // (PRIM-W4) "this Digimon cannot be attacked" — consulted on the defender by AttackPermanentAction.
    public static CannotRestrictionResult CannotBeAttacked(
        HeadlessEntityId defenderId,
        IReadOnlyList<CannotRestriction> restrictions,
        HeadlessEntityId? sourceEntityId = null)
    {
        return Evaluate(new CannotRestrictionRequest(CannotRestrictionKind.BeAttacked, defenderId, restrictions, sourceEntityId));
    }

    // (PRIM-W3) "this Digimon cannot be deleted by effects/skills" (battle deletion still applies) —
    // consulted by the effect-sourced delete path.
    public static CannotRestrictionResult CannotBeDeletedBySkill(
        HeadlessEntityId targetId,
        IReadOnlyList<CannotRestriction> restrictions,
        HeadlessEntityId? sourceEntityId = null)
    {
        return Evaluate(new CannotRestrictionRequest(CannotRestrictionKind.DeleteBySkill, targetId, restrictions, sourceEntityId));
    }

    private static IEnumerable<CannotRestriction> ReadRestrictionsFromFlags(IReadOnlyDictionary<string, bool> flags)
    {
        foreach (KeyValuePair<string, bool> pair in flags)
        {
            if (!pair.Value)
            {
                continue;
            }

            foreach (CannotRestriction restriction in ReadSimpleRestriction(pair.Key, pair.Value, null))
            {
                yield return restriction;
            }
        }
    }

    private static IEnumerable<CannotRestriction> ReadRestrictionsFromValues(
        IReadOnlyDictionary<string, object?> values,
        HeadlessEntityId? effectId = null)
    {
        foreach (CannotRestriction restriction in ReadSimpleRestrictions(values, effectId))
        {
            yield return restriction;
        }

        if (!values.TryGetValue(RestrictionsKey, out object? rawRestrictions) || rawRestrictions is null)
        {
            yield break;
        }

        foreach (object? rawRestriction in FlattenObjects(rawRestrictions))
        {
            if (TryReadRestriction(rawRestriction, effectId, out CannotRestriction? restriction))
            {
                yield return restriction!;
            }
        }
    }

    private static IEnumerable<CannotRestriction> ReadSimpleRestrictions(
        IReadOnlyDictionary<string, object?> values,
        HeadlessEntityId? effectId)
    {
        foreach (string key in new[] { CannotAttackKey, CannotBlockKey, CannotDeleteKey, CannotBeDeletedKey, CannotReturnToHandKey, CannotReturnToDeckKey, CannotReturnToLibraryKey, CannotSuspendKey, CannotDigivolveKey, CannotUnsuspendKey, CannotBeBlockedKey, CannotBeDeletedBySkillKey, CannotBeAttackedKey })
        {
            if (TryReadBool(values, key, out bool isRestricted) && isRestricted)
            {
                foreach (CannotRestriction restriction in ReadSimpleRestriction(key, isRestricted, effectId))
                {
                    yield return restriction;
                }
            }
        }
    }

    private static IEnumerable<CannotRestriction> ReadSimpleRestriction(
        string key,
        bool isRestricted,
        HeadlessEntityId? effectId)
    {
        if (!isRestricted || !TryKindFromKey(key, out CannotRestrictionKind kind))
        {
            yield break;
        }

        yield return new CannotRestriction(IdFor(effectId, key), kind);
    }

    private static bool TryReadRestriction(
        object? rawRestriction,
        HeadlessEntityId? effectId,
        out CannotRestriction? restriction)
    {
        restriction = null;
        if (rawRestriction is CannotRestriction typed)
        {
            restriction = typed;
            return true;
        }

        if (rawRestriction is not IReadOnlyDictionary<string, object?> values ||
            !TryReadEnum(values, RestrictionKindKey, CannotRestrictionKind.Attack, out CannotRestrictionKind kind))
        {
            return false;
        }

        HeadlessEntityId? targetEntityId = TryReadEntityId(values, RestrictionTargetEntityIdKey, out HeadlessEntityId target)
            ? target
            : null;
        HeadlessEntityId? sourceEntityId = TryReadEntityId(values, RestrictionSourceEntityIdKey, out HeadlessEntityId source)
            ? source
            : null;
        string? reason = TryReadString(values, RestrictionReasonKey, out string? parsedReason)
            ? parsedReason
            : null;
        bool requiresAvailability = TryReadBool(values, "requiresAvailabilityCheck", out bool parsedAvailability) && parsedAvailability;
        string id = TryReadString(values, "id", out string? parsedId)
            ? parsedId!
            : IdFor(effectId, kind.ToString());

        restriction = new CannotRestriction(id, kind, targetEntityId, sourceEntityId, reason, requiresAvailability);
        return true;
    }

    private static bool CanApply(CannotRestriction restriction, CannotRestrictionRequest request)
    {
        if (restriction.RequiresAvailabilityCheck && !request.CheckAvailability)
        {
            return false;
        }

        if (restriction.TargetEntityId is HeadlessEntityId target && target != request.TargetEntityId)
        {
            return false;
        }

        return restriction.SourceEntityId is null ||
            request.SourceEntityId is HeadlessEntityId sourceEntityId &&
            restriction.SourceEntityId.GetValueOrDefault() == sourceEntityId;
    }

    private static bool TryKindFromKey(string key, out CannotRestrictionKind kind)
    {
        kind = key switch
        {
            CannotAttackKey => CannotRestrictionKind.Attack,
            CannotBlockKey => CannotRestrictionKind.Block,
            CannotDeleteKey => CannotRestrictionKind.Delete,
            CannotBeDeletedKey => CannotRestrictionKind.Delete,
            CannotReturnToHandKey => CannotRestrictionKind.ReturnToHand,
            CannotReturnToDeckKey => CannotRestrictionKind.ReturnToDeck,
            CannotReturnToLibraryKey => CannotRestrictionKind.ReturnToDeck,
            CannotSuspendKey => CannotRestrictionKind.Suspend,
            CannotDigivolveKey => CannotRestrictionKind.Digivolve,
            CannotUnsuspendKey => CannotRestrictionKind.Unsuspend,
            CannotBeBlockedKey => CannotRestrictionKind.BeBlocked,
            CannotBeDeletedBySkillKey => CannotRestrictionKind.DeleteBySkill,
            CannotBeAttackedKey => CannotRestrictionKind.BeAttacked,
            _ => default,
        };

        return key is CannotAttackKey or CannotBlockKey or CannotDeleteKey or CannotBeDeletedKey or
            CannotReturnToHandKey or CannotReturnToDeckKey or CannotReturnToLibraryKey or CannotSuspendKey or
            CannotDigivolveKey or CannotUnsuspendKey or CannotBeBlockedKey or CannotBeDeletedBySkillKey or CannotBeAttackedKey;
    }

    private static IEnumerable<object?> FlattenObjects(object raw)
    {
        if (raw is string)
        {
            yield return raw;
            yield break;
        }

        if (raw is System.Collections.IEnumerable values)
        {
            foreach (object? value in values)
            {
                yield return value;
            }

            yield break;
        }

        yield return raw;
    }

    private static string IdFor(HeadlessEntityId? effectId, string fallback)
    {
        return effectId is HeadlessEntityId id ? $"{id.Value}:{fallback}" : fallback;
    }

    private static bool TryReadBool(IReadOnlyDictionary<string, object?> values, string key, out bool value)
    {
        value = false;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        return raw switch
        {
            bool boolValue => SetBool(boolValue, out value),
            string text when bool.TryParse(text, out bool parsed) => SetBool(parsed, out value),
            _ => false,
        };
    }

    private static bool TryReadString(IReadOnlyDictionary<string, object?> values, string key, out string? value)
    {
        value = null;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        string? parsed = raw switch
        {
            string stringValue => stringValue,
            HeadlessEntityId entityId => entityId.Value,
            _ => raw.ToString(),
        };
        if (string.IsNullOrWhiteSpace(parsed))
        {
            return false;
        }

        value = parsed.Trim();
        return true;
    }

    private static bool TryReadEntityId(IReadOnlyDictionary<string, object?> values, string key, out HeadlessEntityId value)
    {
        value = default;
        if (!TryReadString(values, key, out string? text))
        {
            return false;
        }

        value = new HeadlessEntityId(text!);
        return !value.IsEmpty;
    }

    private static bool TryReadEnum<TEnum>(
        IReadOnlyDictionary<string, object?> values,
        string key,
        TEnum fallback,
        out TEnum value)
        where TEnum : struct, Enum
    {
        value = fallback;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        if (raw is TEnum enumValue)
        {
            value = enumValue;
            return true;
        }

        if (raw is string text && Enum.TryParse(text, ignoreCase: true, out TEnum parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static bool SetBool(bool input, out bool output)
    {
        output = input;
        return true;
    }
}


public static class RestrictionHelperFactory
{
    public static CannotRestriction CannotAttack(string id, HeadlessEntityId targetEntityId, string? reason = null)
    {
        return CannotRestriction.ForTarget(id, CannotRestrictionKind.Attack, targetEntityId, reason);
    }

    public static CannotRestriction CannotBlock(string id, HeadlessEntityId targetEntityId, string? reason = null)
    {
        return CannotRestriction.ForTarget(id, CannotRestrictionKind.Block, targetEntityId, reason);
    }

    public static CannotRestriction CannotDelete(string id, HeadlessEntityId targetEntityId, string? reason = null)
    {
        return CannotRestriction.ForTarget(id, CannotRestrictionKind.Delete, targetEntityId, reason);
    }

    public static CannotRestriction CannotReturnToHand(string id, HeadlessEntityId targetEntityId, string? reason = null)
    {
        return CannotRestriction.ForTarget(id, CannotRestrictionKind.ReturnToHand, targetEntityId, reason);
    }

    public static CannotRestriction CannotReturnToDeck(string id, HeadlessEntityId targetEntityId, string? reason = null)
    {
        return CannotRestriction.ForTarget(id, CannotRestrictionKind.ReturnToDeck, targetEntityId, reason);
    }

    public static CannotRestriction CannotSuspend(string id, HeadlessEntityId targetEntityId, string? reason = null)
    {
        return CannotRestriction.ForTarget(id, CannotRestrictionKind.Suspend, targetEntityId, reason);
    }
}
