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
/// SCOPE: only accessors with a clean, verified delegate are exposed. AS-IS <c>IsSecurityLooking</c> (10 card
/// call sites) has NO live EngineContext source — it lives on a visibility SNAPSHOT, and the headless model
/// deliberately replaced the AS-IS "IsSelecting/IsSecurityLooking" polling with the choice-pause mechanism
/// (asis-mirror-migration-decision). Not stubbed here (a compile-error is a clearer card-port signal than a
/// throwing property) — design item MIG6-SECURITYLOOKING. Memory / TurnCount accessors land when a witnessed
/// card needs them (design item MIG6-GAMECONTEXT-EXTRA).</summary>
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
