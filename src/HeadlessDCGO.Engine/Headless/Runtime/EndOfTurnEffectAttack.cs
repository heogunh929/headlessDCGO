namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (GR-006 / Execute-1 / C-EoT-2) The end-of-turn effect-driven-attack window for &lt;Execute&gt; ONLY.
/// &lt;Execute&gt; — attack the PLAYER or any Digimon (incl. unsuspended); no summoning-sickness bypass; the
/// attacker self-deletes when the window's attack ends (per-attack flag, NOT on normal main-phase attacks).
///
/// (C-EoT-2, keyword_rehoming_design_2026-07-15.md §2 C-EoT) &lt;Vortex&gt; and &lt;Overclock&gt; firing has been
/// RETIRED from this gate. Their printed/granted <c>VortexSelfEffect</c>/<c>OverclockSelfEffect</c>
/// <c>ActivateClass</c> (returned by the card's <c>CardEffects(OnEndTurn)</c> / stored in the OnEndTurn
/// duration bucket by <c>GainVortex</c>/<c>GainOverclock</c>) now resolves through the AS-IS OnEndTurn window
/// (<c>AutoProcessing.GetSkillInfos</c> → <c>MultipleSkills</c> → <c>VortexProcess</c>/<c>OverclockProcess</c>
/// → <c>SelectAttackEffect</c>/<c>SelectPermanentEffect</c>), matching AS-IS <c>EndTurnProcess</c> :699. Keeping
/// them here too would DOUBLE-FIRE (the window optional "Will you use Vortex?" AND this gate). &lt;Execute&gt;
/// stays because its <c>ExecuteProcess</c> is still STOP (RD-R2-01, UntilEndAttackEffects gap) — it has no
/// window path yet.
/// </summary>
// This file REMAINS the LIVE end-of-turn attack-window substrate for &lt;Execute&gt; (consumed by
// MetadataActionProcessor / NewModelContinuousScan). The retired &lt;Vortex&gt;/&lt;Overclock&gt; funnel siblings
// (OverclockEffect, the VortexCanAttackPlayers marker read) are left dead for the G-clean batch.
public static class EndOfTurnEffectAttack
{
    /// <summary>Per-instance guard so a Digimon's end-of-turn window opens at most once per turn (Overclock
    /// attacks WITHOUT suspending, so the suspend state alone would not stop a re-offer).</summary>
    public const string UsedKey = "endOfTurnAttackUsed";

    // <Execute>: a normal (suspending) attack; may target any opponent Digimon (incl. unsuspended) or the
    // player, and the attacker self-deletes at the end of the attack (set per-offer below).
    private static readonly EffectAttackOptions ExecuteOptions =
        new(WithoutTap: false, AllowPlayerTarget: false, AllowDigimonTarget: true, TargetUnsuspended: true);

    /// <summary>If <paramref name="playerId"/> has an &lt;Execute&gt; Digimon that has not yet taken its
    /// end-of-turn attack, mark it used and open its offer (a pending choice). Returns true when a choice
    /// opened — the caller must NOT finalize the turn while one is pending. (&lt;Vortex&gt;/&lt;Overclock&gt;
    /// are RETIRED — they fire through the OnEndTurn window; see the class summary.)</summary>
    public static bool TryOpen(EngineContext context, HeadlessPlayerId? playerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (playerId is not HeadlessPlayerId player
            || context.ChoiceController.Current.IsPending
            || context.AttackController.Current.IsPending
            || context.ZoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
        {
            if (!context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? inst) || inst is null
                || ReadFlag(inst.Metadata, UsedKey))
            {
                continue;
            }

            // (C-EoT-2) <Vortex>/<Overclock> firing is RETIRED from this gate (they fire through the OnEndTurn
            // window — see the class summary). Only <Execute> remains: "[End of Your Turn] this Digimon may
            // attack; at the end of that attack, delete it." (AS-IS cards register ExecuteSelfEffect at
            // EffectTiming.OnEndTurn; ExecuteProcess is still STOP so there is no window path yet.)
            bool execute = ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.Execute);
            if (!execute)
            {
                continue;
            }

            // <Execute> makes a normal (suspending) attack — a suspended Digimon cannot take it.
            if (ReadFlag(inst.Metadata, "isSuspended"))
            {
                continue;
            }

            // AS-IS Permanent.CanAttack: isExecute does NOT bypass summoning sickness (only Rush/isVortex
            // do, Permanent.cs:2244) — an Execute Digimon played this turn without <Rush> cannot take the
            // window's attack.
            if (ReadFlag(inst.Metadata, "enteredThisTurn") &&
                !ReadFlag(inst.Metadata, "hasRush") &&
                !new Assets.Scripts.Script.CardEffectCommons.Permanent(context, id).HasRush)
            {
                continue;
            }

            SetFlag(context, inst, UsedKey, true);
            // <Execute>'s attack may always target the PLAYER (AS-IS ExecuteProcess
            // canAttackPlayerCondition: () => true) and any Digimon incl. unsuspended (isExecute lifts the
            // suspended-defender gate, Permanent.cs:2311); the attacker self-deletes when the attack ends.
            bool opened = EffectDrivenAttack.RequestChoice(context, id, ExecuteOptions with
            {
                AllowPlayerTarget = true,
                SelfDeleteAtEndOfAttack = true,
            });
            if (opened)
            {
                return true;
            }
            // No valid targets: stays marked-used; try the next eligible Digimon.
        }

        return false;
    }

    /// <summary>Clear the per-turn guard for the player's battle area (call when the turn actually ends).</summary>
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
