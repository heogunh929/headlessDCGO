namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Diagnostics.CodeAnalysis;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using EffectTiming = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.EffectTiming;

/// <summary>
/// (B-2 / P1-5) The main-phase "declare a battle-area permanent's [Main] activated skill" action — AS-IS
/// <c>TurnStateMachine.SetActSkill</c> (TurnStateMachine.cs:3061, permanent + skill index) feeding the
/// declarative branch of the main loop (TurnStateMachine.cs:1174-1195): a usable <c>OnDeclaration</c>
/// <c>ActivateICardEffect</c> chosen by the ACTIVE player is resolved. Legal moves are gated by
/// <see cref="ActivatedEffectResolver.CanDeclareAt"/> (AS-IS <c>Permanent.CanDeclareSkillList</c> → <c>CanUse</c>,
/// cap included), and the effect is resolved through the shared <see cref="ActivatedEffectResolver.ResolveAsync"/>.
///
/// Per-turn accounting (B-1 rework, 2026-07-11): AS-IS registers the use BEFORE anything else in the
/// declaration branch — before even the optional prompt (TurnStateMachine.cs:1183-1186), so DECLINING a
/// declared capped optional skill leaves its use consumed. This action passes <c>declarative: true</c> to the
/// resolver, whose uniform case then consumes before the optional prompt (a non-declarative resolution
/// consumes after the optional accept, mirroring ICardEffect.cs:1117-1124). Refund is a PER-CARD opt-in
/// (<c>ActivatedEffect.RefundWhenNotExecuted</c>, the AS-IS explicit <c>if (!executed) RemoveUse()</c> cards) —
/// never a default. Suspend/resume safety comes from the OnceFlags uniform-cycle transaction (staged consumes
/// replay across the resume), NOT from any consume re-ordering.
///
/// Before this action existed, <c>OnDeclaration</c> was resolved only through the attack-declaration proxy
/// stopgap in <see cref="AttackPermanentAction"/> (now removed) — this is its real home.
///
/// Scope note (design item B2-05): one action is offered per permanent, resolving that permanent's OnDeclaration
/// skill(s). No ported card carries more than one OnDeclaration skill, so a per-skill-index selector (AS-IS
/// SetActSkill's skillIndex) is unnecessary for the current pool; it becomes needed only when a multi-[Main]-skill
/// card is ported, which also needs per-index resolution the resolver does not yet expose.
/// </summary>
public sealed class MainSkillActivateAction
{
    // (RD-EXT1-01) AS-IS TurnStateMachine.CanSelect (TurnStateMachine.cs:917/925/929) offers a declarable [Main]
    // skill from THREE origins, not just the battle area: field permanents (Permanent.CanDeclareSkill, :917), HAND
    // cards (CardSource.CanDeclareSkill, :925 — the [Hand][Main] lane, e.g. BT17_026 "digivolve your Koji Minamoto
    // into this card"), and TRASH cards (:929). The declaration path (SetActSkill for a card, :3078 →
    // CanDeclareSkillList) resolves an off-field card's OnDeclaration effects the same way ResolveAsync does. The
    // CanDeclareAt gate (CanUse(null)) self-filters each origin, so a card only surfaces when its own precondition
    // holds (BT17_026 needs Lobomon+KendoGarurumon in trash + a Koji Minamoto on the field). SECURITY is NOT scanned
    // (AS-IS CanSelect has no security CanDeclareSkill branch).
    private static readonly ChoiceZone[] DeclarableZones = { ChoiceZone.BattleArea, ChoiceZone.Hand, ChoiceZone.Trash };

    public IReadOnlyList<LegalAction> GetLegalActions(EngineContext context, HeadlessPlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (playerId.IsEmpty || context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return Array.Empty<LegalAction>();
        }

        return DeclarableZones
            .SelectMany(zone => zoneReader.GetCards(playerId, zone))
            .Where(cardId => ActivatedEffectResolver.CanDeclareAt(context, cardId, playerId, EffectTiming.OnDeclaration))
            .Select(cardId => HeadlessActionFactory.ActivateMain(playerId, cardId, ResolveEffectId(context, cardId)))
            .OrderBy(action => action.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<ActionProcessResult> ProcessAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!MainSkillActivateActionPayload.TryRead(action, out MainSkillActivateActionPayload? payload, out string? error))
        {
            return ActionProcessResult.Failure(error ?? "Invalid ActivateMain payload.", BaseMetadata(action));
        }

        MainSkillActivateValidation validation = Validate(context, action.PlayerId, payload);
        if (!validation.IsLegal)
        {
            return ActionProcessResult.Illegal(action, validation.Reason, Metadata(action, payload, validation));
        }

        try
        {
            // declarative: the AS-IS main-loop declaration registers the per-turn use BEFORE the optional prompt
            // (TurnStateMachine.cs:1183-1186) — declining a declared capped skill leaves the cap consumed.
            int resolved = await ActivatedEffectResolver
                .ResolveAsync(context, payload.PermanentId, action.PlayerId, EffectTiming.OnDeclaration, cancellationToken, declarative: true)
                .ConfigureAwait(false);

            Dictionary<string, object?> metadata = Metadata(action, payload, validation);
            metadata["resolvedEffectCount"] = resolved;
            return ActionProcessResult.Success("Main skill declared.", metadata);
        }
        catch (DeferredChoicePendingException ex)
        {
            // The [Main] skill's body asked the agent for a choice (interactive provider). The originating action
            // has committed (nothing to re-pay — the cost lives in the body), so record the suspended activation
            // and let the next ResolveChoice resume it via the generic re-resolve path
            // (MetadataActionProcessor.ResolveChoiceAsync → ResolveAsync with this timing), WITHOUT re-running here.
            context.DeferredActivations.Suspend(payload.PermanentId, EffectTiming.OnDeclaration, action.PlayerId, declarative: true);
            Dictionary<string, object?> pending = Metadata(action, payload, validation);
            pending["pendingChoice"] = true;
            pending["pendingChoiceMessage"] = ex.Message;
            return ActionProcessResult.Success("Main skill declared; awaiting choice.", pending);
        }
    }

    private static MainSkillActivateValidation Validate(
        EngineContext context,
        HeadlessPlayerId playerId,
        MainSkillActivateActionPayload payload)
    {
        if (playerId.IsEmpty)
        {
            return MainSkillActivateValidation.Illegal("Player id must not be empty.");
        }

        if (payload.SkillIndex < 0)
        {
            return MainSkillActivateValidation.Illegal("Main skill index must not be negative.");
        }

        if (!context.CardInstanceRepository.TryGetInstance(payload.PermanentId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            return MainSkillActivateValidation.Illegal($"Card instance '{payload.PermanentId}' was not found.");
        }

        if (instance.OwnerId != playerId)
        {
            return MainSkillActivateValidation.Illegal(
                $"Card instance '{payload.PermanentId}' is owned by player '{instance.OwnerId}', not player '{playerId}'.",
                instance.DefinitionId);
        }

        if (context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return MainSkillActivateValidation.Illegal("Zone mover does not expose readable zone state.", instance.DefinitionId);
        }

        // (RD-EXT1-01) the declared card must live in one of the AS-IS declarable origins (battle area / hand /
        // trash — TurnStateMachine.CanSelect), not the battle area alone.
        if (!DeclarableZones.Any(zone => zoneReader.GetCards(playerId, zone).Contains(payload.PermanentId)))
        {
            return MainSkillActivateValidation.Illegal(
                $"Card '{payload.PermanentId}' is not in a declarable zone (battle area / hand / trash) of player '{playerId}'.",
                instance.DefinitionId);
        }

        // Re-check the declare gate (AS-IS main loop re-tests CanUse before running) — keeps a capped-out or
        // no-longer-usable [Main] skill inside the legality boundary rather than deferring to a silent no-op.
        if (!ActivatedEffectResolver.CanDeclareAt(context, payload.PermanentId, playerId, EffectTiming.OnDeclaration))
        {
            return MainSkillActivateValidation.Illegal(
                $"Permanent '{payload.PermanentId}' has no usable [Main] declared skill.",
                instance.DefinitionId,
                payload.EffectId);
        }

        return MainSkillActivateValidation.Legal(instance.DefinitionId, payload.EffectId);
    }

    private static HeadlessEntityId ResolveEffectId(EngineContext context, HeadlessEntityId permanentId)
    {
        if (context.CardInstanceRepository.TryGetInstance(permanentId, out CardInstanceRecord? instance) && instance is not null)
        {
            return new HeadlessEntityId($"{instance.DefinitionId.Value}:declaration");
        }

        return new HeadlessEntityId($"{permanentId.Value}:declaration");
    }

    private static Dictionary<string, object?> Metadata(
        LegalAction action,
        MainSkillActivateActionPayload payload,
        MainSkillActivateValidation validation)
    {
        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.CardId] = payload.PermanentId.Value;
        metadata[HeadlessActionParameterKeys.EffectId] = payload.EffectId.Value;
        metadata[HeadlessActionParameterKeys.SkillIndex] = payload.SkillIndex;
        metadata["cardDefinitionId"] = validation.CardDefinitionId?.Value;
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
}

public sealed record MainSkillActivateActionPayload(
    HeadlessEntityId PermanentId,
    HeadlessEntityId EffectId,
    int SkillIndex)
{
    public IReadOnlyDictionary<string, object?> ToParameters()
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.CardId] = PermanentId,
            [HeadlessActionParameterKeys.EffectId] = EffectId,
            [HeadlessActionParameterKeys.SkillIndex] = SkillIndex
        };
    }

    public static bool TryRead(
        LegalAction action,
        [NotNullWhen(true)] out MainSkillActivateActionPayload? payload,
        out string? error)
    {
        if (!HeadlessActionPayloadReader.TryReadEntityId(
                action,
                HeadlessActionParameterKeys.CardId,
                out HeadlessEntityId permanentId,
                out error))
        {
            payload = null;
            return false;
        }

        if (!HeadlessActionPayloadReader.TryReadEntityId(
                action,
                HeadlessActionParameterKeys.EffectId,
                out HeadlessEntityId effectId,
                out error))
        {
            payload = null;
            return false;
        }

        int skillIndex = TryReadInt(action.Parameters, HeadlessActionParameterKeys.SkillIndex, out int parsedSkillIndex)
            ? parsedSkillIndex
            : 0;

        payload = new MainSkillActivateActionPayload(permanentId, effectId, skillIndex);
        error = null;
        return true;
    }

    private static bool TryReadInt(
        IReadOnlyDictionary<string, object?> parameters,
        string key,
        out int value)
    {
        if (!parameters.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            value = default;
            return false;
        }

        switch (rawValue)
        {
            case int intValue:
                value = intValue;
                return true;
            case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                value = (int)longValue;
                return true;
            case string stringValue when int.TryParse(stringValue, out int parsedValue):
                value = parsedValue;
                return true;
            default:
                value = default;
                return false;
        }
    }
}

public sealed record MainSkillActivateValidation(
    bool IsLegal,
    string Reason,
    HeadlessEntityId? CardDefinitionId,
    HeadlessEntityId? EffectId)
{
    public static MainSkillActivateValidation Legal(
        HeadlessEntityId cardDefinitionId,
        HeadlessEntityId effectId)
    {
        return new MainSkillActivateValidation(true, string.Empty, cardDefinitionId, effectId);
    }

    public static MainSkillActivateValidation Illegal(
        string reason,
        HeadlessEntityId? cardDefinitionId = null,
        HeadlessEntityId? effectId = null)
    {
        return new MainSkillActivateValidation(false, reason ?? string.Empty, cardDefinitionId, effectId);
    }
}
