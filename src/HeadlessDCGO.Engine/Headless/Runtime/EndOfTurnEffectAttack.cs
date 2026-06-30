namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (GR-006) The end-of-turn effect-driven-attack window for &lt;Vortex&gt; / &lt;Overclock&gt;. Both keywords
/// had their resolution machinery (<see cref="EffectDrivenAttack"/> hub, the OverclockTarget/EffectAttack
/// choice handlers) but NO live trigger — nothing offered them during a game, so they were inert. This
/// opens the offer at end of turn, gated by <see cref="ContinuousKeywordGate"/> (so a ported self-static
/// keyword is recognised).
///
/// AS-IS targets:
///   &lt;Vortex&gt;    — attack an opponent's DIGIMON (any, incl. unsuspended; NOT the player), suspends, can
///                  attack the turn it was played. (DataBase.VortexEffectDiscription / VortexProcess:
///                  defenderCondition _ =&gt; true + SetIsVortex.)
///   &lt;Overclock&gt; — delete a token/[trait] ally, then attack a PLAYER without suspending. Handled by
///                  <see cref="OverclockEffect.RequestChoice"/> (player-only: defenderCondition _ =&gt; false).
/// </summary>
public static class EndOfTurnEffectAttack
{
    /// <summary>Per-instance guard so a Digimon's end-of-turn window opens at most once per turn (Overclock
    /// attacks WITHOUT suspending, so the suspend state alone would not stop a re-offer).</summary>
    public const string UsedKey = "endOfTurnAttackUsed";

    // <Vortex>: opponent Digimon (any), not the player, attacker suspends (a normal attack).
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
            if (!overclock && !vortex)
            {
                continue;
            }

            // Vortex makes a normal (suspending) attack — a suspended Digimon cannot take it.
            if (!overclock && ReadFlag(inst.Metadata, "isSuspended"))
            {
                continue;
            }

            SetFlag(context, inst, UsedKey, true);
            bool opened = overclock
                ? OverclockEffect.RequestChoice(context, id)
                : EffectDrivenAttack.RequestChoice(context, id, VortexOptions);
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
