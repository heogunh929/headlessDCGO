namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Diagnostics.CodeAnalysis;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class AttackPermanentAction
{
    private const string CanAttackKey = "canAttack";
    private const string CannotAttackKey = "cannotAttack";
    private const string CanSuspendKey = "canSuspend";
    private const string IsSuspendedKey = "isSuspended";
    private const string EnteredThisTurnKey = "enteredThisTurn";
    private const string HasRushKey = "hasRush";
    private const string CanAttackPlayerKey = "canAttackPlayer";
    private const string CannotAttackPlayerKey = "cannotAttackPlayer";
    private const string CanAttackUnsuspendedDigimonKey = "canAttackUnsuspendedDigimon";

    public IReadOnlyList<LegalAction> GetLegalActions(
        EngineContext context,
        HeadlessPlayerId playerId)
    {
        return GetAttackDeclarations(context, playerId)
            .SelectMany(declaration => declaration.TargetCandidates)
            .Select(candidate => candidate.ToLegalAction())
            .OrderBy(action => action.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<AttackDeclaration> GetAttackDeclarations(
        EngineContext context,
        HeadlessPlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (playerId.IsEmpty || context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return Array.Empty<AttackDeclaration>();
        }

        HeadlessPlayerId? defendingPlayerId = context.TurnController.Current.NonTurnPlayerId;
        if (!defendingPlayerId.HasValue)
        {
            return Array.Empty<AttackDeclaration>();
        }

        List<AttackDeclaration> declarations = new();
        foreach (HeadlessEntityId attackerId in zoneReader.GetCards(playerId, ChoiceZone.BattleArea))
        {
            var candidates = new List<AttackTargetCandidate>();
            AttackPermanentValidation directValidation = Validate(
                context,
                playerId,
                attackerId,
                defendingPlayerId.Value,
                targetId: null,
                isDirectAttack: true);

            if (directValidation.IsLegal)
            {
                candidates.Add(AttackTargetCandidate.DirectPlayer(
                    playerId,
                    attackerId,
                    directValidation.AttackerDefinitionId!.Value,
                    defendingPlayerId.Value));
            }

            foreach (HeadlessEntityId targetId in zoneReader.GetCards(defendingPlayerId.Value, ChoiceZone.BattleArea))
            {
                AttackPermanentValidation validation = Validate(
                    context,
                    playerId,
                    attackerId,
                    defendingPlayerId.Value,
                    targetId,
                    isDirectAttack: false);
                if (!validation.IsLegal)
                {
                    continue;
                }

                candidates.Add(AttackTargetCandidate.Digimon(
                    playerId,
                    attackerId,
                    validation.AttackerDefinitionId!.Value,
                    defendingPlayerId.Value,
                    targetId,
                    validation.TargetDefinitionId!.Value));
            }

            if (candidates.Count == 0)
            {
                continue;
            }

            declarations.Add(new AttackDeclaration(
                playerId,
                attackerId,
                candidates[0].AttackerDefinitionId,
                candidates
                    .OrderBy(candidate => candidate.Kind)
                    .ThenBy(candidate => candidate.TargetId?.Value ?? string.Empty, StringComparer.Ordinal)
                    .ToArray()));
        }

        return declarations
            .OrderBy(declaration => declaration.AttackerId.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public ActionProcessResult Process(
        LegalAction action,
        EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);

        if (!AttackActionPayload.TryRead(action, out AttackActionPayload? payload, out string? error))
        {
            return ActionProcessResult.Failure(error ?? "Invalid DeclareAttack payload.", BaseMetadata(action));
        }

        AttackPermanentValidation validation = Validate(
            context,
            action.PlayerId,
            payload.AttackerId,
            payload.DefendingPlayerId,
            payload.TargetId,
            payload.IsDirectAttack);
        if (!validation.IsLegal)
        {
            return ActionProcessResult.Illegal(action, validation.Reason, Metadata(action, payload, validation, context.AttackController.Current));
        }

        SuspendAttacker(context, payload.AttackerId);
        HeadlessAttackState attack = context.AttackController.DeclareAttack(
            action.PlayerId,
            payload.AttackerId,
            payload.DefendingPlayerId,
            payload.TargetId,
            payload.IsDirectAttack);

        Dictionary<string, object?> metadata = Metadata(action, payload, validation, attack);
        metadata["attackIntent"] = "AttackPermanentAction";
        metadata["attackerSuspended"] = true;
        return ActionProcessResult.Success("Attack declared.", metadata);
    }

    private static AttackPermanentValidation Validate(
        EngineContext context,
        HeadlessPlayerId playerId,
        HeadlessEntityId attackerId,
        HeadlessPlayerId defendingPlayerId,
        HeadlessEntityId? targetId,
        bool isDirectAttack)
    {
        if (playerId.IsEmpty)
        {
            return AttackPermanentValidation.Illegal("Player id must not be empty.");
        }

        if (context.AttackController.Current.IsPending)
        {
            return AttackPermanentValidation.Illegal("Another attack is already pending.");
        }

        HeadlessTurnState turn = context.TurnController.Current;
        if (turn.TurnPlayerId is null || turn.TurnPlayerId.Value != playerId)
        {
            return AttackPermanentValidation.Illegal("Only the current turn player can declare an attack.");
        }

        if (!turn.NonTurnPlayerId.HasValue || defendingPlayerId != turn.NonTurnPlayerId.Value)
        {
            return AttackPermanentValidation.Illegal("Defending player must be the current non-turn player.");
        }

        if (context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return AttackPermanentValidation.Illegal("Zone mover does not expose readable zone state.");
        }

        if (!TryReadInstanceAndCard(context, attackerId, out CardInstanceRecord? attacker, out CardRecord? attackerCard, out string? attackerError))
        {
            return AttackPermanentValidation.Illegal(attackerError ?? $"Attacker '{attackerId}' was not found.");
        }

        if (attacker.OwnerId != playerId)
        {
            return AttackPermanentValidation.Illegal($"Attacker '{attackerId}' is not owned by player '{playerId}'.");
        }

        if (!zoneReader.GetCards(playerId, ChoiceZone.BattleArea).Contains(attackerId))
        {
            return AttackPermanentValidation.Illegal($"Attacker '{attackerId}' is not in the battle area.");
        }

        if (!IsDigimon(attackerCard))
        {
            return AttackPermanentValidation.Illegal($"Attacker '{attackerId}' is not a Digimon.");
        }

        if (ReadBool(attacker.Metadata, IsSuspendedKey))
        {
            return AttackPermanentValidation.Illegal($"Attacker '{attackerId}' is suspended.");
        }

        if (!ReadBool(attacker.Metadata, CanSuspendKey, defaultValue: true))
        {
            return AttackPermanentValidation.Illegal($"Attacker '{attackerId}' cannot suspend.");
        }

        if (!ReadBool(attacker.Metadata, CanAttackKey, defaultValue: true) ||
            ReadBool(attacker.Metadata, CannotAttackKey) ||
            ReadBool(attackerCard.Metadata, CannotAttackKey))
        {
            return AttackPermanentValidation.Illegal($"Attacker '{attackerId}' cannot attack.");
        }

        // (X-04 / D-A6) Continuous effects from other cards can forbid this attacker from attacking —
        // either globally or against a SPECIFIC defender. Passing targetId (null for a direct attack)
        // makes the check target-aware: a restriction scoped to a defender only fires for that target,
        // mirroring the original ICanNotAttackTargetDefendingPermanentEffect(this, Defender).
        CannotRestrictionResult attackRestriction = ContinuousRestrictionGate.EvaluateAttack(context, attackerId, targetId);
        if (attackRestriction.IsRestricted)
        {
            return AttackPermanentValidation.Illegal($"Attacker '{attackerId}' cannot attack ({attackRestriction.Reason}).");
        }

        if (ReadBool(attacker.Metadata, EnteredThisTurnKey) && !ReadBool(attacker.Metadata, HasRushKey) && !ReadBool(attackerCard.Metadata, HasRushKey))
        {
            return AttackPermanentValidation.Illegal($"Attacker '{attackerId}' entered this turn and has no rush.");
        }

        if (!targetId.HasValue)
        {
            if (!isDirectAttack)
            {
                return AttackPermanentValidation.Illegal("Targetless attack must be marked as direct.");
            }

            if (ReadBool(attacker.Metadata, CannotAttackPlayerKey) ||
                ReadBool(attackerCard.Metadata, CannotAttackPlayerKey) ||
                !ReadBool(attacker.Metadata, CanAttackPlayerKey, defaultValue: true))
            {
                return AttackPermanentValidation.Illegal($"Attacker '{attackerId}' cannot attack the player.");
            }

            return AttackPermanentValidation.Legal(attacker.DefinitionId, null);
        }

        if (!TryReadInstanceAndCard(context, targetId.Value, out CardInstanceRecord? target, out CardRecord? targetCard, out string? targetError))
        {
            return AttackPermanentValidation.Illegal(targetError ?? $"Attack target '{targetId}' was not found.");
        }

        if (target.OwnerId != defendingPlayerId)
        {
            return AttackPermanentValidation.Illegal($"Attack target '{targetId}' is not owned by defending player '{defendingPlayerId}'.");
        }

        if (!zoneReader.GetCards(defendingPlayerId, ChoiceZone.BattleArea).Contains(targetId.Value))
        {
            return AttackPermanentValidation.Illegal($"Attack target '{targetId}' is not in the defending battle area.");
        }

        if (!IsDigimon(targetCard))
        {
            return AttackPermanentValidation.Illegal($"Attack target '{targetId}' is not a Digimon.");
        }

        if (!ReadBool(target.Metadata, IsSuspendedKey) &&
            !ReadBool(attacker.Metadata, CanAttackUnsuspendedDigimonKey) &&
            !ReadBool(attackerCard.Metadata, CanAttackUnsuspendedDigimonKey))
        {
            return AttackPermanentValidation.Illegal($"Attack target '{targetId}' is not suspended.");
        }

        return AttackPermanentValidation.Legal(attacker.DefinitionId, target.DefinitionId);
    }

    private static bool TryReadInstanceAndCard(
        EngineContext context,
        HeadlessEntityId instanceId,
        [NotNullWhen(true)] out CardInstanceRecord? instance,
        [NotNullWhen(true)] out CardRecord? card,
        out string? error)
    {
        card = null;
        if (!context.CardInstanceRepository.TryGetInstance(instanceId, out instance) || instance is null)
        {
            error = $"Card instance '{instanceId}' was not found.";
            return false;
        }

        if (!context.CardRepository.TryGetCard(instance.DefinitionId, out card) || card is null)
        {
            error = $"Card definition '{instance.DefinitionId}' was not found.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsDigimon(CardRecord card)
    {
        return string.Equals(card.CardType, "Digimon", StringComparison.OrdinalIgnoreCase);
    }

    private static void SuspendAttacker(EngineContext context, HeadlessEntityId attackerId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker) || attacker is null)
        {
            return;
        }

        Dictionary<string, object?> metadata = new(attacker.Metadata, StringComparer.Ordinal)
        {
            [IsSuspendedKey] = true,
            ["suspendedByAttack"] = true
        };
        context.CardInstanceRepository.Upsert(attacker with { Metadata = metadata });
    }

    private static bool ReadBool(
        IReadOnlyDictionary<string, object?> metadata,
        string key,
        bool defaultValue = false)
    {
        if (!metadata.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return defaultValue;
        }

        return rawValue switch
        {
            bool value => value,
            string value => bool.TryParse(value, out bool parsed) ? parsed : defaultValue,
            _ => defaultValue
        };
    }

    private static Dictionary<string, object?> Metadata(
        LegalAction action,
        AttackActionPayload payload,
        AttackPermanentValidation validation,
        HeadlessAttackState attack)
    {
        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.AttackerId] = payload.AttackerId.Value;
        metadata[HeadlessActionParameterKeys.DefendingPlayerId] = payload.DefendingPlayerId.Value;
        metadata[HeadlessActionParameterKeys.AttackTargetId] = payload.TargetId?.Value;
        metadata[HeadlessActionParameterKeys.IsDirectAttack] = payload.IsDirectAttack;
        metadata["attackerDefinitionId"] = validation.AttackerDefinitionId?.Value;
        metadata["targetDefinitionId"] = validation.TargetDefinitionId?.Value;
        AddAttackState(metadata, attack);
        return metadata;
    }

    private static Dictionary<string, object?> BaseMetadata(LegalAction action)
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.ActionId] = action.Id.Value,
            [HeadlessActionParameterKeys.PlayerId] = action.PlayerId.Value,
            [HeadlessActionParameterKeys.ActionType] = action.ActionType
        };
    }

    private static void AddAttackState(
        Dictionary<string, object?> metadata,
        HeadlessAttackState state)
    {
        metadata[HeadlessActionParameterKeys.AttackCount] = state.AttackCount;
        metadata[HeadlessActionParameterKeys.BlockerId] = state.BlockerId?.Value;
        metadata[HeadlessActionParameterKeys.AttackBlocked] = state.IsBlocked;
        metadata[HeadlessActionParameterKeys.AttackPending] = state.IsPending;
        metadata[HeadlessActionParameterKeys.AttackResolved] = state.IsResolved;
    }
}

public sealed record AttackPermanentValidation(
    bool IsLegal,
    string Reason,
    HeadlessEntityId? AttackerDefinitionId,
    HeadlessEntityId? TargetDefinitionId)
{
    public static AttackPermanentValidation Legal(
        HeadlessEntityId attackerDefinitionId,
        HeadlessEntityId? targetDefinitionId)
    {
        return new AttackPermanentValidation(true, string.Empty, attackerDefinitionId, targetDefinitionId);
    }

    public static AttackPermanentValidation Illegal(
        string reason,
        HeadlessEntityId? attackerDefinitionId = null,
        HeadlessEntityId? targetDefinitionId = null)
    {
        return new AttackPermanentValidation(false, reason ?? string.Empty, attackerDefinitionId, targetDefinitionId);
    }
}

public sealed record AttackDeclaration
{
    public AttackDeclaration(
        HeadlessPlayerId playerId,
        HeadlessEntityId attackerId,
        HeadlessEntityId attackerDefinitionId,
        IReadOnlyList<AttackTargetCandidate> targetCandidates)
    {
        if (playerId.IsEmpty)
        {
            throw new ArgumentException("Attack declaration player id must not be empty.", nameof(playerId));
        }

        if (attackerId.IsEmpty)
        {
            throw new ArgumentException("Attack declaration attacker id must not be empty.", nameof(attackerId));
        }

        if (attackerDefinitionId.IsEmpty)
        {
            throw new ArgumentException("Attack declaration attacker definition id must not be empty.", nameof(attackerDefinitionId));
        }

        ArgumentNullException.ThrowIfNull(targetCandidates);
        AttackTargetCandidate[] candidates = targetCandidates.ToArray();
        if (candidates.Length == 0)
        {
            throw new ArgumentException("Attack declaration requires at least one target candidate.", nameof(targetCandidates));
        }

        if (candidates.Any(candidate => candidate.PlayerId != playerId || candidate.AttackerId != attackerId))
        {
            throw new ArgumentException("Attack target candidates must match the declaration player and attacker.", nameof(targetCandidates));
        }

        PlayerId = playerId;
        AttackerId = attackerId;
        AttackerDefinitionId = attackerDefinitionId;
        TargetCandidates = Array.AsReadOnly(candidates);
    }

    public HeadlessPlayerId PlayerId { get; }

    public HeadlessEntityId AttackerId { get; }

    public HeadlessEntityId AttackerDefinitionId { get; }

    public IReadOnlyList<AttackTargetCandidate> TargetCandidates { get; }
}

public sealed record AttackTargetCandidate
{
    public AttackTargetCandidate(
        AttackTargetKind kind,
        HeadlessPlayerId playerId,
        HeadlessEntityId attackerId,
        HeadlessEntityId attackerDefinitionId,
        HeadlessPlayerId defendingPlayerId,
        HeadlessEntityId? targetId,
        HeadlessEntityId? targetDefinitionId,
        bool isDirectAttack)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Attack target kind must be a known value.");
        }

        if (playerId.IsEmpty)
        {
            throw new ArgumentException("Attack target candidate player id must not be empty.", nameof(playerId));
        }

        if (attackerId.IsEmpty)
        {
            throw new ArgumentException("Attack target candidate attacker id must not be empty.", nameof(attackerId));
        }

        if (attackerDefinitionId.IsEmpty)
        {
            throw new ArgumentException("Attack target candidate attacker definition id must not be empty.", nameof(attackerDefinitionId));
        }

        if (defendingPlayerId.IsEmpty)
        {
            throw new ArgumentException("Attack target candidate defending player id must not be empty.", nameof(defendingPlayerId));
        }

        if (targetId is { IsEmpty: true })
        {
            throw new ArgumentException("Attack target candidate target id must not be empty.", nameof(targetId));
        }

        if (targetDefinitionId is { IsEmpty: true })
        {
            throw new ArgumentException("Attack target candidate target definition id must not be empty.", nameof(targetDefinitionId));
        }

        if (kind == AttackTargetKind.Player && (targetId.HasValue || targetDefinitionId.HasValue || !isDirectAttack))
        {
            throw new ArgumentException("Player attack candidates must be direct and targetless.", nameof(kind));
        }

        if (kind == AttackTargetKind.Digimon && (!targetId.HasValue || !targetDefinitionId.HasValue || isDirectAttack))
        {
            throw new ArgumentException("Digimon attack candidates must have a target and must not be direct.", nameof(kind));
        }

        Kind = kind;
        PlayerId = playerId;
        AttackerId = attackerId;
        AttackerDefinitionId = attackerDefinitionId;
        DefendingPlayerId = defendingPlayerId;
        TargetId = targetId;
        TargetDefinitionId = targetDefinitionId;
        IsDirectAttack = isDirectAttack;
    }

    public AttackTargetKind Kind { get; }

    public HeadlessPlayerId PlayerId { get; }

    public HeadlessEntityId AttackerId { get; }

    public HeadlessEntityId AttackerDefinitionId { get; }

    public HeadlessPlayerId DefendingPlayerId { get; }

    public HeadlessEntityId? TargetId { get; }

    public HeadlessEntityId? TargetDefinitionId { get; }

    public bool IsDirectAttack { get; }

    public static AttackTargetCandidate DirectPlayer(
        HeadlessPlayerId playerId,
        HeadlessEntityId attackerId,
        HeadlessEntityId attackerDefinitionId,
        HeadlessPlayerId defendingPlayerId)
    {
        return new AttackTargetCandidate(
            AttackTargetKind.Player,
            playerId,
            attackerId,
            attackerDefinitionId,
            defendingPlayerId,
            targetId: null,
            targetDefinitionId: null,
            isDirectAttack: true);
    }

    public static AttackTargetCandidate Digimon(
        HeadlessPlayerId playerId,
        HeadlessEntityId attackerId,
        HeadlessEntityId attackerDefinitionId,
        HeadlessPlayerId defendingPlayerId,
        HeadlessEntityId targetId,
        HeadlessEntityId targetDefinitionId)
    {
        return new AttackTargetCandidate(
            AttackTargetKind.Digimon,
            playerId,
            attackerId,
            attackerDefinitionId,
            defendingPlayerId,
            targetId,
            targetDefinitionId,
            isDirectAttack: false);
    }

    public LegalAction ToLegalAction()
    {
        return HeadlessActionFactory.DeclareAttack(
            PlayerId,
            AttackerId,
            DefendingPlayerId,
            TargetId,
            IsDirectAttack);
    }
}

public enum AttackTargetKind
{
    Player = 0,
    Digimon = 1,
}
