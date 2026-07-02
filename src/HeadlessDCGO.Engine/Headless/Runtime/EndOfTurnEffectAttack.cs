namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (GR-006 / Execute-1) The end-of-turn effect-driven-attack window for &lt;Vortex&gt; / &lt;Overclock&gt; /
/// &lt;Execute&gt;. &lt;Execute&gt; — attack the PLAYER or any Digimon (incl. unsuspended); no summoning-sickness
/// bypass; the attacker self-deletes when the window's attack ends (per-attack flag, NOT on normal
/// main-phase attacks). The first two keywords
/// had their resolution machinery (<see cref="EffectDrivenAttack"/> hub, the OverclockTarget/EffectAttack
/// choice handlers) but NO live trigger — nothing offered them during a game, so they were inert. This
/// opens the offer at end of turn, gated by <see cref="ContinuousKeywordGate"/> (so a ported self-static
/// keyword is recognised).
///
/// AS-IS targets:
///   &lt;Vortex&gt;    — attack an opponent's DIGIMON (any, incl. unsuspended), suspends, can attack the turn
///                  it was played. (DataBase.VortexEffectDiscription / VortexProcess: defenderCondition
///                  _ =&gt; true + SetIsVortex.) The PLAYER becomes a legal target only while an
///                  IVortexCanAttackPlayersEffect accepts this attacker (K1; CanActivateVortex
///                  `|| PermanentHasVortexCanAttackPlayers`).
///   &lt;Overclock&gt; — delete a token/[trait] ally, then attack a PLAYER without suspending. Handled by
///                  <see cref="OverclockEffect.RequestChoice"/> (player-only: defenderCondition _ =&gt; false).
/// </summary>
public static class EndOfTurnEffectAttack
{
    /// <summary>Per-instance guard so a Digimon's end-of-turn window opens at most once per turn (Overclock
    /// attacks WITHOUT suspending, so the suspend state alone would not stop a re-offer).</summary>
    public const string UsedKey = "endOfTurnAttackUsed";

    // <Vortex>: opponent Digimon (any), attacker suspends (a normal attack). AllowPlayerTarget is decided
    // per-offer (K1: VortexCanAttackPlayers marker).
    private static readonly EffectAttackOptions VortexOptions =
        new(WithoutTap: false, AllowPlayerTarget: false, AllowDigimonTarget: true, TargetUnsuspended: true);

    /// <summary>If <paramref name="playerId"/> has a Vortex/Overclock Digimon that has not yet taken its
    /// end-of-turn attack, mark it used and open its offer (a pending choice). Returns true when a choice
    /// opened — the caller must NOT finalize the turn while one is pending.</summary>
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

            bool overclock = ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.Overclock);
            bool vortex = ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.Vortex);
            // (Execute-1) <Execute>: "[End of Your Turn] this Digimon may attack; at the end of that attack,
            // delete it." Same window (AS-IS cards register ExecuteSelfEffect at EffectTiming.OnEndTurn).
            bool execute = !vortex && !overclock &&
                ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.Execute);
            if (!overclock && !vortex && !execute)
            {
                continue;
            }

            // Vortex/Execute make a normal (suspending) attack — a suspended Digimon cannot take it.
            if (!overclock && ReadFlag(inst.Metadata, "isSuspended"))
            {
                continue;
            }

            // AS-IS Permanent.CanAttack: isExecute does NOT bypass summoning sickness (only Rush/isVortex
            // do, Permanent.cs:2244) — an Execute Digimon played this turn without <Rush> cannot take the
            // window's attack.
            if (execute &&
                ReadFlag(inst.Metadata, "enteredThisTurn") &&
                !ReadFlag(inst.Metadata, "hasRush") &&
                !ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.Rush))
            {
                continue;
            }

            SetFlag(context, inst, UsedKey, true);
            // (K1) AS-IS CanActivateVortex: the PLAYER becomes a legal Vortex target only while an active
            // IVortexCanAttackPlayersEffect accepts this attacker — evaluated ONCE when the offer opens
            // (mirrors the AS-IS canAttackPlayers snapshot at VortexProcess start).
            bool vortexCanAttackPlayers = vortex && ContinuousKeywordGate.HasKeyword(
                context, id, ContinuousKeywordGate.VortexCanAttackPlayers);
            // <Execute>'s attack may always target the PLAYER (AS-IS ExecuteProcess
            // canAttackPlayerCondition: () => true) and any Digimon incl. unsuspended (isExecute lifts the
            // suspended-defender gate, Permanent.cs:2311); the attacker self-deletes when the attack ends.
            bool opened = overclock
                ? OverclockEffect.RequestChoice(context, id)
                : EffectDrivenAttack.RequestChoice(context, id, VortexOptions with
                {
                    AllowPlayerTarget = execute || vortexCanAttackPlayers,
                    SelfDeleteAtEndOfAttack = execute,
                });
            if (opened)
            {
                return true;
            }
            // No valid targets / no ally to sacrifice: stays marked-used; try the next eligible Digimon.
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
