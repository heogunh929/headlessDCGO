namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>(MIG6 goal-6 surface) Headless mirror of the original <c>GameContext</c> — the per-match state
/// handle card effects read through <c>GManager.instance.turnStateMachine.gameContext.X</c>. No mirror
/// GameContext existed before this goal (only <see cref="CardSource"/>/<see cref="Permanent"/>/
/// <see cref="Player"/> did). Each accessor cites its AS-IS anchor and returns MIRROR <see cref="Player"/> /
/// <see cref="Permanent"/> objects (not substrate PlayerState/EntityId), so a local-LLM card port mechanically
/// mirrors e.g. <c>gameContext.Players_ForTurnPlayer.Map(p =&gt; p.GetBattleAreaDigimons())</c>. Delegates to the
/// substrate <see cref="IHeadlessTurnController"/> (seat order / turn player / phase) + zone reads.
///
/// SCOPE: only accessors with a clean, verified delegate are exposed.
///
/// (R4 P1 gap-fill, 2026-07-16) AS-IS <c>Memory</c> (GameContext.cs:27) IS exposed — it is a genuine
/// GameContext member with a live delegate (<see cref="Context"/>.MemoryController.Current.Current, the single
/// signed turn-player-relative gauge the mirror <c>Player.MemoryForPlayer</c> already reads as
/// "gameContext.Memory"). The other two R4 P1 candidates are STOPPED, not invented:
/// • <c>TurnCount</c> — NOT a GameContext member in AS-IS (zero <c>gameContext.TurnCount</c> call sites); it is
///   owned by <c>TurnStateMachine</c> (TurnStateMachine.cs:29, read via <c>turnStateMachine.TurnCount</c>). Its
///   faithful mirror home is the sibling shim <see cref="TurnStateMachine"/> (which already mirrors DoneStartGame /
///   isExecuting by delegating to the same TurnController), NOT here — placing it on GameContext would be a
///   wrong-host invention. The value exists in substrate (<c>HeadlessTurnState.TurnNumber</c>, 1-indexed like
///   AS-IS TurnCount), so the additive is trivial once landed on the correct file (deferred: outside this file's
///   sole ownership / R4 P2a phase-body scope).
/// • <c>IsSecurityLooking</c> (10 card call sites) has NO live EngineContext source — the only substrate holder
///   (<c>GameContextStateAccessor.IsSecurityLooking</c>) is never instantiated/set, i.e. dead. It is a mutable
///   <c>{ get; set; }</c> flag WRITTEN by cards (BT14_033/BT14_093/ST10_06/BT16_024) around an interactive
///   "look at security" window; the headless model deliberately replaced that IsSelecting/IsSecurityLooking
///   polling with the choice-pause mechanism (asis-mirror-migration-decision), where the interactive select IS
///   the look and is atomic (no concurrent effect observes a mid-look state). Its consumers are already covered:
///   the CanAddSecurity/CanReduceSecurity guards (Player.cs:1471/1523) delegate to
///   <c>SecurityRuleGateSeam</c> (stubbed as if IsSecurityLooking==false), and the face-down visibility reads
///   (CardObjectController:373 / Permanent:1092) are subsumed by choice-pause reveal. A faithful mirror would
///   need a match-scoped mutable box (the <c>isExecuting</c> pattern) to host the writes on this stateless VIEW —
///   substrate state the existing decision deliberately avoided ("a compile-error is a clearer card-port signal
///   than a throwing/constant property"). Not stubbed here — design item MIG6-SECURITYLOOKING (unchanged).</summary>
public sealed class GameContext
{
    /// <summary>(MIG6) 1:1 mirror of AS-IS <c>GameContext.phase</c> (GameContext.cs:116-124).</summary>
    public enum phase
    {
        Active,
        Draw,
        Breeding,
        Main,
        End,
        None,
    }

    public GameContext(EngineContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public EngineContext Context { get; }

    private HeadlessTurnState Turn => Context.TurnController.Current;

    /// <summary>(R4 P1) AS-IS <c>GameContext.Memory</c> (GameContext.cs:22-28) — the single shared, signed memory
    /// gauge (range [-10, 10]; positive favours the turn player). This stateless VIEW delegates to the live
    /// substrate source, <see cref="Context"/>.MemoryController.Current.Current — the SAME value the mirror
    /// <c>Player.MemoryForPlayer</c> (Player.cs) reads as AS-IS "gameContext.Memory" before re-signing per seat,
    /// and that <c>AceOverflowGate.MemoryDelta</c> / the play-cost pipeline mutate. Read-only here (a card port
    /// that WRITES memory mirrors <c>Player.AddMemory</c> / <c>SetFixedMemory</c>, which route through the
    /// controller — AS-IS never assigns <c>gameContext.Memory</c> from a card, only through Player memory ops).</summary>
    public int Memory => Context.MemoryController.Current.Current;

    /// <summary>(MIG6) AS-IS <c>GameContext.Players</c>: every seated player.</summary>
    public List<Player> Players =>
        Turn.PlayerOrder.Where(id => !id.IsEmpty).Select(id => new Player(Context, id)).ToList();

    /// <summary>(MIG6) AS-IS <c>gameContext.Players_ForTurnPlayer</c> (99 card call sites): all players, TURN
    /// player first — the ordering effect scans rely on.</summary>
    public List<Player> Players_ForTurnPlayer => OrderedFrom(Turn.TurnPlayerId);

    /// <summary>(MIG6) AS-IS <c>gameContext.Players_ForNonTurnPlayer</c>: all players, NON-turn player first.</summary>
    public List<Player> Players_ForNonTurnPlayer => OrderedFrom(Turn.NonTurnPlayerId);

    /// <summary>(MIG6) AS-IS <c>gameContext.TurnPlayer</c> (29 card call sites).</summary>
    public Player? TurnPlayer =>
        Turn.TurnPlayerId is { IsEmpty: false } id ? new Player(Context, id) : null;

    /// <summary>(MIG6) AS-IS <c>gameContext.NonTurnPlayer</c>.</summary>
    public Player? NonTurnPlayer =>
        Turn.NonTurnPlayerId is { IsEmpty: false } id ? new Player(Context, id) : null;

    /// <summary>(MIG6) AS-IS <c>gameContext.TurnPhase</c> (13 card call sites): the AS-IS <see cref="phase"/>
    /// value for the live <see cref="HeadlessPhase"/>. The headless-only phases (Setup/Unsuspend/MemoryPass)
    /// map to their nearest AS-IS phase (Setup→None pre-game, Unsuspend→Active start-of-turn, MemoryPass→Main
    /// end-of-main) — documented, not silent.</summary>
    public phase TurnPhase => Turn.Phase switch
    {
        HeadlessPhase.None => phase.None,
        HeadlessPhase.Active => phase.Active,
        HeadlessPhase.Draw => phase.Draw,
        HeadlessPhase.Breeding => phase.Breeding,
        HeadlessPhase.Main => phase.Main,
        HeadlessPhase.End => phase.End,
        HeadlessPhase.Setup => phase.None,
        HeadlessPhase.Unsuspend => phase.Active,
        HeadlessPhase.MemoryPass => phase.Main,
        _ => phase.None,
    };

    /// <summary>(MIG6) AS-IS <c>gameContext.PermanentsForTurnPlayer</c>: every battle-area permanent of every
    /// player, TURN player first (AS-IS <c>Players_ForTurnPlayer.SelectMany(GetBattleAreaPermanents)</c>).</summary>
    public List<Permanent> PermanentsForTurnPlayer =>
        Players_ForTurnPlayer.SelectMany(player => player.GetBattleAreaPermanents()).ToList();

    /// <summary>(P6 cluster3) AS-IS <c>GameContext.ActiveCardList</c> (GameContext.cs:31) — every live card
    /// in the match (AS-IS: a mutable <c>List&lt;CardSource&gt;</c> populated at game setup). This mirror
    /// GameContext is a stateless VIEW over <see cref="EngineContext"/>, so the list is COMPUTED: every
    /// registered card instance from the substrate card-instance repository, as mirror
    /// <see cref="CardSource"/> views (owner from the instance record — the established PermanentOf idiom,
    /// KeyWordEffects/Save.cs).</summary>
    public List<CardSource> ActiveCardList =>
        Context.CardInstanceRepository.Snapshot()
            .Select(record => new CardSource(Context, record.InstanceId, record.OwnerId))
            .ToList();

    private List<Player> OrderedFrom(HeadlessPlayerId? first)
    {
        List<Player> seats = Players;
        if (first is { IsEmpty: false } head)
        {
            return seats.OrderBy(player => player.PlayerId == head ? 0 : 1).ToList();
        }

        return seats;
    }
}
