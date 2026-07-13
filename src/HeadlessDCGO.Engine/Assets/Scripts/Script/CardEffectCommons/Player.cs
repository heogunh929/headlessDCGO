namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>(MIG5 goal-5 surface) Headless mirror of the original <c>Player</c> — the handle a card-effect
/// builder receives for "the owner" / "the opponent" / a scanned seat. No mirror Player class existed before
/// this goal (only <see cref="CardSource"/> and <see cref="Permanent"/> did); each member below cites its
/// AS-IS anchor (<c>Player.cs</c>) and the verified headless delegate it forwards to.
///
/// SCOPE: only the members with a clean, verified delegate are exposed. AS-IS Player members that have NO
/// headless delegate (CanAddMemory — the CannotAddMemory scan is private to the mutation sink; CanReduceCost —
/// no CannotReduceCostKey; IsEmptyFrame/IsBattleAreaFrame — no frame/slot model, zones are lists; EffectList —
/// effects are modeled via EffectRegistry, not per-player bag lists) are deliberately NOT stubbed here (a
/// compile-error on a missing method is a better card-port signal than a compiles-then-throws surface). Those
/// are tracked as design items MIG5-CANADDMEMORY / MIG5-CANREDUCECOST / MIG5-FRAME-MODEL / MIG5-PLAYER-EFFECTLIST.</summary>
public sealed class Player
{
    public Player(EngineContext context, HeadlessPlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (playerId.IsEmpty)
        {
            throw new ArgumentException("Player id must not be empty.", nameof(playerId));
        }

        Context = context;
        PlayerId = playerId;
    }

    public EngineContext Context { get; }

    public HeadlessPlayerId PlayerId { get; }

    /// <summary>(MIG5) AS-IS <c>Player.Enemy</c> (Player.cs:742-768): the OTHER seated player, or null when THIS
    /// player is not seated or no other seat exists (AS-IS's own null fallthrough). Reads the seat order —
    /// the mirror's <c>gameContext.Players</c> equivalent.</summary>
    public Player? Enemy
    {
        get
        {
            IReadOnlyList<HeadlessPlayerId> seats = Context.TurnController.Current.PlayerOrder;
            if (!seats.Contains(PlayerId))
            {
                return null;
            }

            foreach (HeadlessPlayerId seat in seats)
            {
                if (seat != PlayerId && !seat.IsEmpty)
                {
                    return new Player(Context, seat);
                }
            }

            return null;
        }
    }

    // ===== field-zone reads (AS-IS Player.GetBattleAreaPermanents/GetBreedingAreaPermanents/GetFieldPermanents/
    // GetBattleAreaDigimons) — the SAME zone read the mirror AutoProcessing.cs private helpers already use,
    // promoted to a public per-player surface. Headless has no frame array: zone-list membership already IS the
    // AS-IS "FieldPermanents[i] != null && TopCard != null" condition, so no extra guard is needed.

    /// <summary>(MIG5) AS-IS <c>Player.GetBattleAreaPermanents()</c> (Player.cs:617-636).</summary>
    public List<Permanent> GetBattleAreaPermanents() => GetZonePermanents(ChoiceZone.BattleArea);

    /// <summary>(MIG5) AS-IS <c>Player.GetBreedingAreaPermanents()</c> (Player.cs:640-659).</summary>
    public List<Permanent> GetBreedingAreaPermanents() => GetZonePermanents(ChoiceZone.BreedingArea);

    /// <summary>(MIG5) AS-IS <c>Player.GetFieldPermanents()</c> (Player.cs:665-681): battle + breeding (disjoint
    /// zones, so a plain concat matches AS-IS's single-array scan).</summary>
    public List<Permanent> GetFieldPermanents() =>
        GetBattleAreaPermanents().Concat(GetBreedingAreaPermanents()).ToList();

    /// <summary>(MIG5) AS-IS <c>Player.GetBattleAreaDigimons()</c> (Player.cs:683-704): battle-area permanents
    /// filtered to <see cref="Permanent.IsDigimon"/>.</summary>
    public List<Permanent> GetBattleAreaDigimons() =>
        GetBattleAreaPermanents().Where(permanent => permanent.IsDigimon).ToList();

    private List<Permanent> GetZonePermanents(ChoiceZone zone)
    {
        if (Context.ZoneMover is not IZoneStateReader zones)
        {
            return new List<Permanent>();
        }

        return zones.GetCards(PlayerId, zone).ToArray()
            .Select(cardId => new Permanent(Context, cardId, PlayerId))
            .ToList();
    }

    // ===== security rule gates =====

    /// <summary>(MIG5) AS-IS <c>Player.CanAddSecurity(cardEffect)</c> (Player.cs:1469-1517): delegates to the
    /// mirror <see cref="Assets.Scripts.Script.SecurityRuleGateSeam.CanAddSecurity"/> (currently stubbed true,
    /// pre-existing design item MIG3-CANADDSECURITY — the real CannotAddSecurity scan runs at the AddToSecurity
    /// mutation choke, so a pre-check here can read true while the mutation later no-ops).</summary>
    public bool CanAddSecurity(HeadlessEntityId? causeEffectSourceId) =>
        Assets.Scripts.Script.SecurityRuleGateSeam.CanAddSecurity(Context, PlayerId, causeEffectSourceId);

    /// <summary>(MIG5) AS-IS <c>Player.CanReduceSecurity()</c> (Player.cs:1521-1529): delegates to the mirror
    /// <see cref="Assets.Scripts.Script.SecurityRuleGateSeam.CanReduceSecurity"/> (stubbed true, pre-existing
    /// design item MIG3-CANREDUCESECURITY).</summary>
    public bool CanReduceSecurity() =>
        Assets.Scripts.Script.SecurityRuleGateSeam.CanReduceSecurity(Context, PlayerId);

    // ===== match-status =====

    /// <summary>(MIG5) AS-IS <c>Player.SetLose()</c> (Player.cs:119-122): mark this player as having lost —
    /// the same one-way flag <c>TerminalEvaluator</c> reads.</summary>
    public void SetLose() => Context.PlayerStatusController.MarkLose(PlayerId);
}
