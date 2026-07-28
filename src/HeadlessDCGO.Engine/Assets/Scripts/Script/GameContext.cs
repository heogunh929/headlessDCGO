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
/// • <c>IsSecurityLooking</c> (10 card call sites) — RESOLVED (design item MIG6-SECURITYLOOKING retired, fidelity
///   defect C). AS-IS <c>GameContext.IsSecurityLooking</c> is a mutable <c>{ get; set; }</c> flag whose ENGINE-layer
///   writes live in <c>SelectCardEffect.Activate</c> (SelectCardEffect.cs:380 set / :1008 clear) and which cards
///   (BT14_033/BT14_093/ST10_06/BT16_024) also flip around their own "look at security" windows. Its rule-bearing
///   consumers are <c>Player.CanAddSecurity</c> (:1471) / <c>Player.CanReduceSecurity</c> (:1523), which return
///   FALSE while it is set — previously stubbed as if it were always false, i.e. those two guards were dead.
///   Ported here, on its AS-IS home (GameContext), as a match-scoped mutable box keyed by
///   <see cref="EngineContext"/> — the ESTABLISHED pattern for a mutable AS-IS field on a stateless mirror VIEW
///   (<c>TurnStateMachine.isExecuting</c> / <c>TurnStateMachine.Passed</c>), needed for the same reason: a mirror
///   GameContext instance is created fresh per <c>GManager.instance</c> read, so a per-instance field would not
///   survive the AS-IS write-then-read-elsewhere flow.</summary>
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

    // (fidelity defect C) AS-IS GameContext.IsSecurityLooking — a plain mutable public bool on the per-match
    // GameContext. The mirror GameContext is a per-access VIEW (a fresh instance on every `GManager.instance`
    // /`new GameContext(context)`), so a per-instance field would not survive the AS-IS
    // "SelectCardEffect sets it -> Player.CanAddSecurity/CanReduceSecurity read it" flow. Backed by a
    // match-scoped box keyed by EngineContext — identical to TurnStateMachine.isExecuting / .Passed.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EngineContext, System.Runtime.CompilerServices.StrongBox<bool>> _isSecurityLookingStore = new();

    /// <summary>(fidelity defect C) 1:1 mirror of AS-IS <c>GameContext.IsSecurityLooking</c> — true while an
    /// interactive "look at security" window is open. Written by <c>SelectCardEffect.Activate</c>
    /// (SelectCardEffect.cs:378-381 set / :1008 clear) and by the cards that open their own security-look window;
    /// read by <c>Player.CanAddSecurity</c> (Player.cs:1471) / <c>Player.CanReduceSecurity</c> (:1523), both of
    /// which return false while it is set.</summary>
    public bool IsSecurityLooking
    {
        get => _isSecurityLookingStore.GetValue(Context, static _ => new System.Runtime.CompilerServices.StrongBox<bool>(false)).Value;
        set => _isSecurityLookingStore.GetValue(Context, static _ => new System.Runtime.CompilerServices.StrongBox<bool>(false)).Value = value;
    }

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

    /// <summary>(MIG6; R4 S2) AS-IS <c>gameContext.TurnPhase</c> (13 card call sites): the AS-IS <see cref="phase"/>
    /// value for the live <see cref="HeadlessPhase"/>. Since R4 S2 folded the substrate phase enum to the AS-IS
    /// 6-value model (the former Setup/Unsuspend/MemoryPass splits moved to the <see cref="TurnStepCursor"/>
    /// sub-cursor), this map is now a near-identity — the step position within a phase is invisible to cards, exactly
    /// as in AS-IS (a card sees phase.Active during natural unsuspend, phase.Main during memory pass, phase.None
    /// pre-game).
    /// (R4 P2b) AS-IS <c>GameContext.TurnPhase</c> (GameContext.cs:126) is a plain mutable field; the SETTER mirrors
    /// the engine-layer phase writes — the TurnStateMachine bodies (:554 Active / :666 Draw / :715 Breeding /
    /// :897 Main / :3162 End) and <c>AutoProcessing.EndTurnProcess</c> (:717 End) — delegating to the substrate
    /// <c>TurnController.SetPhase</c>. A phase write is a fresh phase entry, so the <see cref="TurnStepCursor"/>
    /// resets to PhaseStart (the sub-cursor is substrate step state the AS-IS enum does not carry). No CARD writes
    /// TurnPhase in AS-IS (write sites are the turn-flow layer only) — same discipline holds for ports.</summary>
    public phase TurnPhase
    {
        get => Turn.Phase switch
        {
            HeadlessPhase.None => phase.None,
            HeadlessPhase.Active => phase.Active,
            HeadlessPhase.Draw => phase.Draw,
            HeadlessPhase.Breeding => phase.Breeding,
            HeadlessPhase.Main => phase.Main,
            HeadlessPhase.End => phase.End,
            _ => phase.None,
        };
        set => Context.TurnController.SetPhase(value switch
        {
            phase.None => HeadlessPhase.None,
            phase.Active => HeadlessPhase.Active,
            phase.Draw => HeadlessPhase.Draw,
            phase.Breeding => HeadlessPhase.Breeding,
            phase.Main => HeadlessPhase.Main,
            phase.End => HeadlessPhase.End,
            _ => HeadlessPhase.None,
        });
    }

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
