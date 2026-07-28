namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Diagnostics.CodeAnalysis;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using EffectTiming = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.EffectTiming;

/// <summary>
/// (B-2 / P1-5) The main-phase "declare a battle-area permanent's [Main] activated skill" action — AS-IS
/// <c>TurnStateMachine.SetActSkill</c> (TurnStateMachine.cs:3061, permanent + skill index) feeding the
/// declarative branch of the main loop (TurnStateMachine.cs:1174-1195): a usable <c>OnDeclaration</c>
/// <c>ActivateICardEffect</c> chosen by the ACTIVE player is resolved. Legal moves are gated by the AS-IS
/// <c>Permanent.CanDeclareSkill()</c> / <c>CardSource.CanDeclareSkill</c> accessors (see
/// <c>CanDeclareSkill</c> below); the EXECUTION half is the pump (TurnFlowDriver -> the AS-IS SetActSkill /
/// SetActCardSkill packets -> the main-loop declaration branch), so this class is legal-moves-only.
///
/// Per-turn accounting: AS-IS registers the use BEFORE anything else in the declaration branch — before even
/// the optional prompt (TurnStateMachine.cs:1183-1186), so DECLINING a declared capped optional skill leaves
/// its use consumed. That ordering lives in the AS-IS declaration branch the pump runs, not here.
///
/// Scope note (design item B2-05): one action is offered per permanent, resolving that permanent's OnDeclaration
/// skill(s). No ported card carries more than one OnDeclaration skill, so a per-skill-index selector (AS-IS
/// SetActSkill's skillIndex) is unnecessary for the current pool.
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

        // SUBSTRATE: the AS-IS CanDeclareSkill scans (EffectList -> CanUse -> CheckEffectDisabledClass) read
        // game state through the process-global GManager.instance; the mirror resolves that from
        // AmbientMatchContext, so scope the match for the whole enumeration (a caller already in the same
        // scope re-enters harmlessly) — same idiom as NewModelContinuousScan's public entry points.
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);

        return DeclarableZones
            .SelectMany(zone => zoneReader.GetCards(playerId, zone).Select(cardId => (Zone: zone, CardId: cardId)))
            .Where(entry => CanDeclareSkill(context, entry.Zone, entry.CardId, playerId))
            .Select(entry => HeadlessActionFactory.ActivateMain(playerId, entry.CardId, ResolveEffectId(context, entry.CardId)))
            .OrderBy(action => action.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>AS-IS <c>TurnStateMachine.CanSelect</c> (TurnStateMachine.cs:917/925/929): a FIELD permanent
    /// offers a declarable [Main] skill through <c>Permanent.CanDeclareSkill()</c> (:917); a HAND (:925) or
    /// TRASH (:929) card through <c>CardSource.CanDeclareSkill</c>. (ActivatedEffectResolver retirement) the
    /// substrate <c>CanDeclareAt</c> gate is replaced by these two AS-IS accessors, which the mirror already
    /// carries verbatim — both are <c>EffectList(OnDeclaration)</c> filtered to <c>ActivateICardEffect</c> with
    /// a live <c>CanUse(null)</c>, i.e. exactly the gate the substrate helper re-implemented.</summary>
    private static bool CanDeclareSkill(
        EngineContext context,
        ChoiceZone zone,
        HeadlessEntityId cardId,
        HeadlessPlayerId playerId)
    {
        return zone == ChoiceZone.BattleArea
            ? new Permanent(context, cardId, playerId).CanDeclareSkill()
            : new CardSource(context, cardId, playerId).CanDeclareSkill;
    }

    // (ActivatedEffectResolver retirement) ProcessAsync + Validate are RETIRED. ActivateMain is PUMP-ONLY:
    // MetadataActionProcessor rejects it outright ("routes through TurnFlowDriver to the mirror main-skill
    // activation") and TurnFlowDriver.NormalizedActivateMain queues the AS-IS packet instead —
    // ActivatePermanentAction(permanentIndex, skillIndex) / ActivateCardAction(cardIndex, skillIndex) ->
    // TurnStateMachine.SetActSkill / SetActCardSkill (:3050-3082) -> the AS-IS main-loop declaration branch
    // (:1174-1195) -> AutoProcessing.ActivateEffectProcess. That is the AS-IS direct activation, so this class
    // now only produces the legal-move table (GetLegalActions, above). The per-turn-use ordering note and the
    // DeferredActivations suspend/resume that lived here belong to the AS-IS declaration branch and the pump's
    // in-place choice parking respectively — neither needs an action-processor shim.

    private static HeadlessEntityId ResolveEffectId(EngineContext context, HeadlessEntityId permanentId)
    {
        if (context.CardInstanceRepository.TryGetInstance(permanentId, out CardInstanceRecord? instance) && instance is not null)
        {
            return new HeadlessEntityId($"{instance.DefinitionId.Value}:declaration");
        }

        return new HeadlessEntityId($"{permanentId.Value}:declaration");
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
