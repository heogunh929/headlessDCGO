namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (A군 4단계 / Execute) RETIRED — this invented end-of-turn effect-driven-attack gate no longer fires ANY
/// keyword. Its last remaining tenant &lt;Execute&gt; is now rehoused to the AS-IS OnEndTurn window (a printed
/// card's <c>CardEffects(OnEndTurn)</c> returns <c>CardEffectFactory.ExecuteSelfEffect</c> / a grant stores an
/// <c>ExecuteEffect</c> ActivateClass in the OnEndTurn duration bucket via <c>GainExecute</c>; the window
/// <c>AutoProcessing.GetSkillInfos(OnEndTurn)</c> → <c>MultipleSkills</c> → <c>ExecuteProcess</c> resolves it,
/// matching AS-IS <c>EndTurnProcess</c> :699 — the same path &lt;Vortex&gt;/&lt;Overclock&gt; use since
/// C-EoT-2). <c>ExecuteProcess</c>'s old blocker (RD-R2-01, <c>Permanent.UntilEndAttackEffects</c>) is resolved:
/// the W3 bucket is live and <c>PermanentEffectFactory.DeleteSelfEffect/AddDetailClass</c> are now the AS-IS
/// ActivateClass overloads, so the "attack then self-delete at end of attack" body is a faithful 1:1 port.
///
/// Consequently <see cref="TryOpen"/> now opens NOTHING (single-fire: window only). This file is FUNCTIONALLY
/// DEAD and is left in place for the G-clean batch to physically delete together with its call-sites
/// (MetadataActionProcessor.cs:1036 <c>TryOpen</c> / :1065 <c>ClearForPlayer</c>) and the now-orphaned
/// <c>EffectDrivenAttack</c> SelfDeleteAtEndOfAttack / OverclockEffect Vortex-side siblings.
/// </summary>
public static class EndOfTurnEffectAttack
{
    /// <summary>Per-instance guard key (legacy; the gate no longer sets it — every EoT keyword fires through the
    /// window). Retained so <see cref="ClearForPlayer"/> and its call-site keep the same shape until G-clean.</summary>
    public const string UsedKey = "endOfTurnAttackUsed";

    /// <summary>RETIRED — always returns false. &lt;Vortex&gt;/&lt;Overclock&gt;/&lt;Execute&gt; (the only
    /// keywords this gate ever fired) all resolve through the AS-IS OnEndTurn window now (see the class summary),
    /// which the caller drains BEFORE this call (MetadataActionProcessor.cs:955). Kept as a no-op so the
    /// call-site compiles until the G-clean batch removes both.</summary>
    public static bool TryOpen(EngineContext context, HeadlessPlayerId? playerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = playerId;
        return false;
    }

    /// <summary>Clear the per-turn guard for the player's battle area (call when the turn actually ends). Now a
    /// no-op in practice (the guard is never set), retained for the call-site until G-clean.</summary>
    public static void ClearForPlayer(EngineContext context, HeadlessPlayerId? playerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (playerId is not HeadlessPlayerId player || context.ZoneMover is not IZoneStateReader zones)
        {
            return;
        }

        foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
        {
            if (context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? inst) && inst is not null
                && ReadFlag(inst.Metadata, UsedKey))
            {
                SetFlag(context, inst, UsedKey, false);
            }
        }
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? value) && value is true;

    private static void SetFlag(EngineContext context, CardInstanceRecord instance, string key, bool value)
    {
        var metadata = new Dictionary<string, object?>(instance.Metadata, StringComparer.Ordinal) { [key] = value };
        context.CardInstanceRepository.Upsert(instance with { Metadata = metadata });
    }
}
