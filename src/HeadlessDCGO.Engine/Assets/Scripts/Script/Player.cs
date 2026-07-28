namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Assets.Scripts.Script.PlayerSelection;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>(MIG5 goal-5 surface) Headless mirror of the original <c>Player</c> — the handle a card-effect
/// builder receives for "the owner" / "the opponent" / a scanned seat. No mirror Player class existed before
/// this goal (only <see cref="CardSource"/> and <see cref="Permanent"/> did); each member below cites its
/// AS-IS anchor (<c>Player.cs</c>) and the verified headless delegate it forwards to.
///
/// SCOPE: only the members with a clean, verified delegate are exposed. AS-IS Player members that have NO
/// headless delegate (CanAddMemory — the CannotAddMemory scan is private to the mutation sink; CanReduceCost —
/// no CannotReduceCostKey; IsEmptyFrame/IsBattleAreaFrame — no frame/slot model, zones are lists) are
/// deliberately NOT stubbed here (a compile-error on a missing method is a better card-port signal than a
/// compiles-then-throws surface). Those are tracked as design items MIG5-CANADDMEMORY / MIG5-CANREDUCECOST /
/// MIG5-FRAME-MODEL. (P6 stage A: <see cref="EffectList"/> is now a REAL member — the flip's live collection
/// scan requires the AS-IS shape — backed by the not-yet-existing new-model player-grant store, so it returns
/// empty; design item P6A-PLAYER-EFFECTLIST supersedes MIG5-PLAYER-EFFECTLIST.)</summary>
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

    // (design item PLAYER-EQUALITY) AS-IS `Player : MonoBehaviour` (Player.cs:12) is a per-seat SINGLETON
    // (GameContext.You / GameContext.Opponent, GameContext.cs:63-64; PlayerFromID returns one of those two,
    // :147-160), so every AS-IS `==`/`!=`/`Contains`/`Equals` on Player is reference identity ≡ SEAT identity
    // (GameContext.NonTurnPlayer :82-99 and SwitchTurnPlayer :166-181 are both built on `player != TurnPlayer`).
    // The mirror Player is a lightweight VIEW reconstructed on every access (GameContext.TurnPlayer /
    // NonTurnPlayer / Players allocate a fresh instance per get), so without value equality every such
    // comparison is reference-unequal and silently always-false. Identity = the SEAT (PlayerId) within the same
    // match (Context) — the same idiom as the sibling view CardSource (CardSource.cs:55-63).
    public override bool Equals(object? obj) =>
        obj is Player other && PlayerId.Equals(other.PlayerId) && ReferenceEquals(Context, other.Context);

    public override int GetHashCode() => PlayerId.GetHashCode();

    public static bool operator ==(Player? left, Player? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Player? left, Player? right) => !(left == right);

    #region Selection Queue

    // (isAI restore) AS-IS Player.cs:192-215 `#region Selection Queue`, ported 1:1 so the AS-IS selection
    // transport (`Set*ForPlayer` / `SelectCountEffect.SetCount` -> QueuePlayerSelection -> the coroutine's
    // WaitUntil(HasPlayerSelection) + DequeuePlayerSelection<T>) reads verbatim in the mirror.
    // SUBSTRATE: AS-IS the queue is a plain instance field on the persistent `Player` MonoBehaviour
    // (Player.cs:193); the mirror Player is a transient per-access view, so the live queue is held per
    // (EngineContext, PlayerId) by PlayerSelectionQueueStore — the same pattern as the sibling
    // PlayerEffectListStore in this file.

    Queue<IPlayerSelection> _playerSelectionQueue => PlayerSelectionQueueStore.Get(Context, PlayerId);

    /// <summary>AS-IS <c>Player.QueuePlayerSelection(IPlayerSelection)</c> (Player.cs:195-198).</summary>
    public void QueuePlayerSelection(IPlayerSelection selection)
    {
        _playerSelectionQueue.Enqueue(selection);
    }

    /// <summary>AS-IS <c>Player.DequeuePlayerSelection&lt;T&gt;()</c> (Player.cs:200-210): dequeues one entry and
    /// returns null when it is not a <typeparamref name="T"/> (AS-IS logs a warning there — Debug.LogWarning is
    /// Unity logging, stripped).</summary>
    public T? DequeuePlayerSelection<T>() where T : class, IPlayerSelection
    {
        IPlayerSelection playerSelection = _playerSelectionQueue.Dequeue();
        if (playerSelection is not T)
        {
            return null;
        }

        return playerSelection as T;
    }

    /// <summary>AS-IS <c>Player.HasPlayerSelection()</c> (Player.cs:212-215).</summary>
    public bool HasPlayerSelection()
    {
        return _playerSelectionQueue.Count > 0;
    }

    #endregion

    #region このプレイヤーがあなたかどうか

    /// <summary>(isAI restore) AS-IS <c>[Header("このプレイヤーがあなたかどうか")] public bool isYou;</c>
    /// (Player.cs:527-529) — "is this player YOU", a PLAIN field the original never computes: it is assigned by
    /// the Unity match setup on the local client's seat. Nothing headless assigns it, so it stays false for both
    /// seats, which is the intended state (the mirror has no local-client seat). It is mirrored as a plain field
    /// so the AS-IS `isYou` branches read verbatim.</summary>
    public bool isYou;

    #endregion

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

    /// <summary>(bridge W5) AS-IS <c>Player.HandCards</c> (Player.cs:506, a public list field) — this player's
    /// hand as live <see cref="CardSource"/> views. Same verified zone read
    /// <c>CardEffectCommons.HasMatchConditionOwnersHand</c> uses (IZoneStateReader Hand → CardSource per id);
    /// the AS-IS card idiom is <c>card.Owner.HandCards…</c>, expressed on the mirror as
    /// <c>new Player(card.Context, card.Owner).HandCards</c> (the established BT2_023 <c>.Enemy</c> route —
    /// a bare <see cref="HeadlessPlayerId"/> cannot carry an extension PROPERTY).</summary>
    public List<CardSource> HandCards
    {
        get
        {
            if (Context.ZoneMover is not IZoneStateReader zones)
            {
                return new List<CardSource>();
            }

            return zones.GetCards(PlayerId, ChoiceZone.Hand).ToArray()
                .Select(cardId => new CardSource(Context, cardId, PlayerId, PlayerId))
                .ToList();
        }
    }

    /// <summary>(P6 stage A) AS-IS <c>Player.TrashCards</c> (Player.cs:510, a public list field) — this
    /// player's trash as live <see cref="CardSource"/> views (same zone-read shape as <see cref="HandCards"/>).
    /// Consumed by the flip's live collection scan (AS-IS AutoProcessing.GetSkillInfos, AutoProcessing.cs:817)
    /// and CEntity_EffectController's added-effect scan.</summary>
    public List<CardSource> TrashCards
    {
        get
        {
            if (Context.ZoneMover is not IZoneStateReader zones)
            {
                return new List<CardSource>();
            }

            return zones.GetCards(PlayerId, ChoiceZone.Trash).ToArray()
                .Select(cardId => new CardSource(Context, cardId, PlayerId, PlayerId))
                .ToList();
        }
    }

    /// <summary>(P6 stage A) AS-IS <c>Player.SecurityCards</c> (Player.cs:518, a public list field) — this
    /// player's security stack as live <see cref="CardSource"/> views. Scan sites filter on
    /// <c>IsFlipped</c> themselves (AS-IS GetSkillInfos AutoProcessing.cs:860-863, CEntity_EffectController.cs).</summary>
    public List<CardSource> SecurityCards
    {
        get
        {
            if (Context.ZoneMover is not IZoneStateReader zones)
            {
                return new List<CardSource>();
            }

            return zones.GetCards(PlayerId, ChoiceZone.Security).ToArray()
                .Select(cardId => new CardSource(Context, cardId, PlayerId, PlayerId))
                .ToList();
        }
    }

    /// <summary>(P6C1) AS-IS <c>Player.LibraryCards</c> (Player.cs:514, a public list field) — this player's
    /// deck as live <see cref="CardSource"/> views (same zone-read shape as <see cref="HandCards"/>). Consumed
    /// by the mirror PlayCardClass root resolution (AS-IS CardController.cs:329).</summary>
    public List<CardSource> LibraryCards
    {
        get
        {
            if (Context.ZoneMover is not IZoneStateReader zones)
            {
                return new List<CardSource>();
            }

            return zones.GetCards(PlayerId, ChoiceZone.Library).ToArray()
                .Select(cardId => new CardSource(Context, cardId, PlayerId, PlayerId))
                .ToList();
        }
    }

    // (R4 S3b) AS-IS Player "Action Queue" region (Player.cs:166-190): the main-phase intent queue the
    // TurnStateMachine selection wait drains (QueueMainPhaseAction ← the AS-IS UI click / network packet; the
    // headless producer is TurnFlowDriver's LegalAction conversion). The mirror Player is a per-access VIEW, so
    // the queue lives in a match-scoped store keyed by (EngineContext, PlayerId) — the PlayerEffectListStore
    // pattern.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EngineContext, Dictionary<HeadlessPlayerId, Queue<MainPhaseAction>>> _mainPhaseActionStore = new();

    private Queue<MainPhaseAction> mainPhaseActions
    {
        get
        {
            Dictionary<HeadlessPlayerId, Queue<MainPhaseAction>> byPlayer =
                _mainPhaseActionStore.GetValue(Context, static _ => new Dictionary<HeadlessPlayerId, Queue<MainPhaseAction>>());
            if (!byPlayer.TryGetValue(PlayerId, out Queue<MainPhaseAction>? queue))
            {
                queue = new Queue<MainPhaseAction>();
                byPlayer[PlayerId] = queue;
            }

            return queue;
        }
    }

    /// <summary>(R4 S3b) AS-IS <c>Player.QueueMainPhaseAction</c> (Player.cs:169-172).</summary>
    public void QueueMainPhaseAction(MainPhaseAction action)
    {
        mainPhaseActions.Enqueue(action);
    }

    /// <summary>(R4 S3b) AS-IS <c>Player.DequeueMainPhaseAction</c> (Player.cs:174-182) — null on empty.</summary>
    public MainPhaseAction? DequeueMainPhaseAction()
    {
        if (mainPhaseActions.Count == 0)
        {
            return null;
        }

        return mainPhaseActions.Dequeue();
    }

    /// <summary>(R4 S3b) AS-IS <c>Player.HasMainPhaseAction</c> (Player.cs:184-187).</summary>
    public bool HasMainPhaseAction()
    {
        return mainPhaseActions.Count > 0;
    }

    /// <summary>(R4 S3b-2③) AS-IS <c>Player.ExecutingCards</c> (Player.cs:522, a public list field) — the
    /// resolving option card's parking pile as live views (the <see cref="LibraryCards"/> shape over the
    /// Execution zone). UseOptionClass gates its resolution/trash tail on membership here.</summary>
    public List<CardSource> ExecutingCards
    {
        get
        {
            if (Context.ZoneMover is not IZoneStateReader zones)
            {
                return new List<CardSource>();
            }

            return zones.GetCards(PlayerId, ChoiceZone.Execution).ToArray()
                .Select(cardId => new CardSource(Context, cardId, PlayerId, PlayerId))
                .ToList();
        }
    }

    /// <summary>(R4 S3a) AS-IS <c>Player.CanHatch</c> (Player.cs:1168) — verbatim: a digitama remains to hatch
    /// and the breeding area is empty. <c>DigitamaLibraryCards</c> is the digitama-deck zone read (same shape as
    /// <see cref="LibraryCards"/>).</summary>
    public bool CanHatch => DigitamaLibraryCards.Count >= 1 && GetBreedingAreaPermanents().Count == 0;

    /// <summary>(R4 S3a) AS-IS <c>Player.DigitamaLibraryCards</c> — the digitama deck as live views (the
    /// <see cref="LibraryCards"/> shape over the DigitamaLibrary zone).</summary>
    public List<CardSource> DigitamaLibraryCards
    {
        get
        {
            if (Context.ZoneMover is not IZoneStateReader zones)
            {
                return new List<CardSource>();
            }

            return zones.GetCards(PlayerId, ChoiceZone.DigitamaLibrary).ToArray()
                .Select(cardId => new CardSource(Context, cardId, PlayerId, PlayerId))
                .ToList();
        }
    }

    /// <summary>(R4 S3a) AS-IS <c>Player.CanMove</c> (Player.cs:1172):
    /// <c>GetBreedingAreaPermanents().Count(p =&gt; p.CanMove) &gt;= 1 &amp;&amp;
    /// fieldCardFrames.Count(frame =&gt; frame.IsEmptyFrame()) &gt;= 1</c>. The empty-battle-frame capacity half
    /// is the frame/slot model the mirror does not carry (zones are lists) — same established ADAPTATION as the
    /// mirror <c>Permanent.CanMove</c> (capacity check omitted, design item RD-P6C1-2).</summary>
    public bool CanMove => GetBreedingAreaPermanents().Count(permanent => permanent.CanMove) >= 1;

    /// <summary>(P6C1) AS-IS <c>Player.MaxMemoryCost</c> (Player.cs:1127-1146): the most memory this player can
    /// pay before the gauge pins at the opponent's +10 — AS-IS <c>PlayerID==0 ? |10 − Memory| : |Memory + 10|</c>
    /// on the seat-absolute gauge. Re-expressed on the verified player-relative read (<see cref="MemoryForPlayer"/>,
    /// whose sign mapping is anchored by <see cref="AddMemory"/>: AS-IS P0 gain SUBTRACTS ⇒ P0 favor = −Memory):
    /// both AS-IS branches reduce to <c>|MemoryForPlayer + 10|</c> (favor −10 ⇒ 0 payable, favor +10 ⇒ 20).</summary>
    public int MaxMemoryCost => Math.Abs(MemoryForPlayer + 10);

    // ===== (R6-P / RD-R6-02) AS-IS Player effect-list buckets (Player.cs:918-946) — the effects GRANTED to
    // this player, fed by CardEffectCommons.AddEffectToPlayer. AS-IS these are plain public settable list
    // FIELDS on the (persistent) Player object; the mirror Player is a transient per-access VIEW (a fresh
    // instance on every resolve), so a per-instance field would not survive across two views of the same seat.
    // Backed by a match-scoped store keyed by (EngineContext, PlayerId) — the same idea as
    // CEntity_EffectControllerStore / TurnStateMachine._isExecutingStore. The get returns the LIVE list (so
    // `.Add(getEffect)` appends AS-IS-verbatim); the set REPLACES it (AS-IS EndTurn cleanup reassigns a fresh
    // `new List<…>()`). ADAPTATION: substrate backing only — names, semantics, and merge order are AS-IS 1:1.

    /// <summary>AS-IS <c>Player.PermanentEffects</c> (Player.cs:918).</summary>
    public List<Func<EffectTiming, ICardEffect>> PermanentEffects
    {
        get => PlayerEffectListStore.Get(Context, PlayerId).PermanentEffects;
        set => PlayerEffectListStore.Get(Context, PlayerId).PermanentEffects = value;
    }

    /// <summary>AS-IS <c>Player.UntilEndBattleEffects</c> (Player.cs:922).</summary>
    public List<Func<EffectTiming, ICardEffect>> UntilEndBattleEffects
    {
        get => PlayerEffectListStore.Get(Context, PlayerId).UntilEndBattleEffects;
        set => PlayerEffectListStore.Get(Context, PlayerId).UntilEndBattleEffects = value;
    }

    /// <summary>AS-IS <c>Player.UntilEachTurnEndEffects</c> (Player.cs:926).</summary>
    public List<Func<EffectTiming, ICardEffect>> UntilEachTurnEndEffects
    {
        get => PlayerEffectListStore.Get(Context, PlayerId).UntilEachTurnEndEffects;
        set => PlayerEffectListStore.Get(Context, PlayerId).UntilEachTurnEndEffects = value;
    }

    /// <summary>AS-IS <c>Player.UntilOwnerTurnEndEffects</c> (Player.cs:930).</summary>
    public List<Func<EffectTiming, ICardEffect>> UntilOwnerTurnEndEffects
    {
        get => PlayerEffectListStore.Get(Context, PlayerId).UntilOwnerTurnEndEffects;
        set => PlayerEffectListStore.Get(Context, PlayerId).UntilOwnerTurnEndEffects = value;
    }

    /// <summary>AS-IS <c>Player.UntilOwnerActivePhaseEffects</c> (Player.cs:934).</summary>
    public List<Func<EffectTiming, ICardEffect>> UntilOwnerActivePhaseEffects
    {
        get => PlayerEffectListStore.Get(Context, PlayerId).UntilOwnerActivePhaseEffects;
        set => PlayerEffectListStore.Get(Context, PlayerId).UntilOwnerActivePhaseEffects = value;
    }

    /// <summary>AS-IS <c>Player.UntilOpponentTurnEndEffects</c> (Player.cs:938).</summary>
    public List<Func<EffectTiming, ICardEffect>> UntilOpponentTurnEndEffects
    {
        get => PlayerEffectListStore.Get(Context, PlayerId).UntilOpponentTurnEndEffects;
        set => PlayerEffectListStore.Get(Context, PlayerId).UntilOpponentTurnEndEffects = value;
    }

    /// <summary>AS-IS <c>Player.UntilCalculateFixedCostEffect</c> (Player.cs:942).</summary>
    public List<Func<EffectTiming, ICardEffect>> UntilCalculateFixedCostEffect
    {
        get => PlayerEffectListStore.Get(Context, PlayerId).UntilCalculateFixedCostEffect;
        set => PlayerEffectListStore.Get(Context, PlayerId).UntilCalculateFixedCostEffect = value;
    }

    /// <summary>AS-IS <c>Player.UntilSecurityCheckEndEffects</c> (Player.cs:946).</summary>
    public List<Func<EffectTiming, ICardEffect>> UntilSecurityCheckEndEffects
    {
        get => PlayerEffectListStore.Get(Context, PlayerId).UntilSecurityCheckEndEffects;
        set => PlayerEffectListStore.Get(Context, PlayerId).UntilSecurityCheckEndEffects = value;
    }

    /// <summary>(R6-P / RD-R6-02) AS-IS <c>Player.EffectList(EffectTiming)</c> (Player.cs:830-914) — merge every
    /// player-grant bucket, keeping each delegate that yields a non-null effect at <paramref name="timing"/>, in
    /// the AS-IS bucket order (Permanent, UntilEndBattle, UntilEachTurnEnd, UntilOwnerTurnEnd, UntilOwnerActivePhase,
    /// UntilSecurityCheckEnd, UntilOpponentTurnEnd, UntilCalculateFixedCost), then AS-IS back-fills a null
    /// EffectSourceCard on each with the player's first owned non-token active card (Player.cs:898-911). Consumed by
    /// the live mirror <c>AutoProcessing.GetSkillInfos</c> (AutoProcessing.cs:871+, the BeforePayCost pay-cost path
    /// runs it live via CardController.PayCost) and the continuous scans in Permanent/CardSource.</summary>
    public List<ICardEffect> EffectList(EffectTiming timing)
    {
        List<ICardEffect> PlayerEffects = new List<ICardEffect>();

        foreach (Func<EffectTiming, ICardEffect> GetCardEffect in PermanentEffects)
        {
            if (GetCardEffect(timing) != null)
            {
                PlayerEffects.Add(GetCardEffect(timing));
            }
        }

        foreach (Func<EffectTiming, ICardEffect> GetCardEffect in UntilEndBattleEffects)
        {
            if (GetCardEffect(timing) != null)
            {
                PlayerEffects.Add(GetCardEffect(timing));
            }
        }

        foreach (Func<EffectTiming, ICardEffect> GetCardEffect in UntilEachTurnEndEffects)
        {
            if (GetCardEffect(timing) != null)
            {
                PlayerEffects.Add(GetCardEffect(timing));
            }
        }

        foreach (Func<EffectTiming, ICardEffect> GetCardEffect in UntilOwnerTurnEndEffects)
        {
            if (GetCardEffect(timing) != null)
            {
                PlayerEffects.Add(GetCardEffect(timing));
            }
        }

        foreach (Func<EffectTiming, ICardEffect> GetCardEffect in UntilOwnerActivePhaseEffects)
        {
            if (GetCardEffect(timing) != null)
            {
                PlayerEffects.Add(GetCardEffect(timing));
            }
        }

        foreach (Func<EffectTiming, ICardEffect> GetCardEffect in UntilSecurityCheckEndEffects)
        {
            if (GetCardEffect(timing) != null)
            {
                PlayerEffects.Add(GetCardEffect(timing));
            }
        }

        foreach (Func<EffectTiming, ICardEffect> GetCardEffect in UntilOpponentTurnEndEffects)
        {
            if (GetCardEffect(timing) != null)
            {
                PlayerEffects.Add(GetCardEffect(timing));
            }
        }

        foreach (Func<EffectTiming, ICardEffect> GetCardEffect in UntilCalculateFixedCostEffect)
        {
            if (GetCardEffect(timing) != null)
            {
                PlayerEffects.Add(GetCardEffect(timing));
            }
        }

        foreach (ICardEffect cardEffect in PlayerEffects)
        {
            if (cardEffect.EffectSourceCard == null)
            {
                // AS-IS Player.cs:902 GManager.instance.turnStateMachine.gameContext.ActiveCardList — every live card.
                foreach (CardSource cardSource in new GameContext(Context).ActiveCardList)
                {
                    if (cardSource.Owner == PlayerId && !cardSource.IsToken)
                    {
                        cardEffect.SetEffectSourceCard(cardSource);
                        break;
                    }
                }
            }
        }

        return PlayerEffects;
    }

    /// <summary>(R6-P / RD-R6-02) AS-IS <c>Player.MaxDP_DeleteEffect(maxDP, cardEffect)</c> (Player.cs:1377-1416):
    /// fold every active <c>IChangeDPDeleteEffectMaxDPEffect</c> over each turn-ordered player's field permanents'
    /// and own player-scope effect lists (<c>CanUse(null)</c>-gated), applying <c>GetMaxDP(_maxDP, cardEffect)</c> —
    /// the ceiling on a "delete a Digimon with DP ≤ maxDP" effect. AS-IS 1:1 (Players_ForTurnPlayer scan order).</summary>
    public int MaxDP_DeleteEffect(int maxDP, ICardEffect cardEffect)
    {
        int _maxDP = maxDP;

        foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect1 in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect1 is IChangeDPDeleteEffectMaxDPEffect changeMax && cardEffect1.CanUse(null))
                    {
                        _maxDP = changeMax.GetMaxDP(_maxDP, cardEffect);
                    }
                }
            }

            foreach (ICardEffect cardEffect1 in player.EffectList(EffectTiming.None))
            {
                if (cardEffect1 is IChangeDPDeleteEffectMaxDPEffect changeMax && cardEffect1.CanUse(null))
                {
                    _maxDP = changeMax.GetMaxDP(_maxDP, cardEffect);
                }
            }
        }

        return _maxDP;
    }

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

    /// <summary>(MIG5; R3-W3c-4c B3 flip) AS-IS <c>Player.CanAddSecurity(cardEffect)</c> (Player.cs:1469-1517): the
    /// AS-IS-literal <c>ICannotAddSecurityEffect</c> LIVE scan over <c>Players_ForTurnPlayer</c>'s field permanents'
    /// + own player-scope <c>EffectList(None)</c>, <c>CanUse(null)</c>-gated, calling
    /// <c>cannotAddSecurity(this = the gaining player, cardEffect)</c>. The AS-IS <c>IsSecurityLooking</c> guard
    /// (:1471) is delegated to <see cref="Assets.Scripts.Script.SecurityRuleGateSeam.CanAddSecurity"/>, which since
    /// fidelity defect C reads the live <see cref="GameContext.IsSecurityLooking"/> carrier (was stubbed true). The
    /// causing effect is reconstructed from <paramref name="causeEffectSourceId"/> (the CardEffectCondition reads
    /// only its source card). Was a bare stub with no restriction scan; the factory now produces the kind-class
    /// <c>CannotAddSecurityClass</c> (no ToBinding) so this live scan is the sole reader.</summary>
    public bool CanAddSecurity(HeadlessEntityId? causeEffectSourceId)
    {
        if (!Assets.Scripts.Script.SecurityRuleGateSeam.CanAddSecurity(Context, PlayerId, causeEffectSourceId))
        {
            return false;
        }

        ICardEffect cardEffect = BareCauseEffect.For(Context, causeEffectSourceId ?? default);
        foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect1 in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect1 is ICannotAddSecurityEffect && cardEffect1.CanUse(null)
                        && ((ICannotAddSecurityEffect)cardEffect1).cannotAddSecurity(this, cardEffect))
                    {
                        return false;
                    }
                }
            }

            foreach (ICardEffect cardEffect1 in player.EffectList(EffectTiming.None))
            {
                if (cardEffect1 is ICannotAddSecurityEffect && cardEffect1.CanUse(null)
                    && ((ICannotAddSecurityEffect)cardEffect1).cannotAddSecurity(this, cardEffect))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>(MIG5; fidelity defect C) AS-IS <c>Player.CanReduceSecurity()</c> (Player.cs:1521-1529) — the WHOLE
    /// AS-IS body is <c>if (gameContext.IsSecurityLooking) return false; return true;</c> (AS-IS has no
    /// <c>ICannotReduceSecurityEffect</c>), hosted in the mirror
    /// <see cref="Assets.Scripts.Script.SecurityRuleGateSeam.CanReduceSecurity"/> (was stubbed true; now the real
    /// guard — design item MIG3-CANREDUCESECURITY retired).</summary>
    public bool CanReduceSecurity() =>
        Assets.Scripts.Script.SecurityRuleGateSeam.CanReduceSecurity(Context, PlayerId);

    /// <summary>(P6C3) AS-IS <c>Player.CanReduceCost(targetPermanents, cardSource)</c> (Player.cs:1329-1368,
    /// retires design item MIG5-CANREDUCECOST): the live <c>ICannotReduceCostEffect</c> scan over
    /// <c>Players_ForTurnPlayer</c>'s field permanents' + own player-scope effect lists, <c>CanUse(null)</c>-gated
    /// (AS-IS 1:1 — no membership/Distinct semantics to preserve, it's a short-circuit veto scan).</summary>
    public bool CanReduceCost(List<Permanent> targetPermanents, CardSource cardSource)
    {
        foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICannotReduceCostEffect cannotReduceCost && cardEffect.CanUse(null)
                        && cannotReduceCost.CannotReduceCost(this, targetPermanents, cardSource))
                    {
                        return false;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICannotReduceCostEffect cannotReduceCost && cardEffect.CanUse(null)
                    && cannotReduceCost.CannotReduceCost(this, targetPermanents, cardSource))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>(P6C3) AS-IS <c>Player.CanIgnoreDigivolutionRequirement(targetPermanent, cardSource)</c>
    /// (Player.cs:1422-1465): the live <c>ICannotIgnoreDigivolutionConditionEffect</c> veto scan, same shape
    /// as <see cref="CanReduceCost"/> (<c>Players_ForTurnPlayer</c>'s field permanents' + own player-scope
    /// effect lists, <c>CanUse(null)</c>-gated).</summary>
    public bool CanIgnoreDigivolutionRequirement(Permanent targetPermanent, CardSource cardSource)
    {
        foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICannotIgnoreDigivolutionConditionEffect cannotIgnore && cardEffect.CanUse(null)
                        && cannotIgnore.cannotIgnoreDigivolutionCondition(this, targetPermanent, cardSource))
                    {
                        return false;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICannotIgnoreDigivolutionConditionEffect cannotIgnore && cardEffect.CanUse(null)
                    && cannotIgnore.cannotIgnoreDigivolutionCondition(this, targetPermanent, cardSource))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>(RD-EXT3-03) AS-IS <c>Player.CanTapWhenAbsorbEvolution_CheckAvailability(permanent, cardEffect)</c>
    /// (Player.cs:1180-1246): the availability half of the &lt;Digisorption&gt; suspend-target judgement — a permanent
    /// is tappable when it has a top card, is un-suspended AND <see cref="Permanent.CanSuspend"/>, and sits in ITS
    /// owner's battle-area Digimon list; then the AS-IS-literal <c>ICanSuspendByDigisorptionEffect</c> LIVE scan over
    /// <c>Players_ForTurnPlayer</c>'s field permanents' + own player-scope <c>EffectList(None)</c>, <c>CanUse(null)</c>-
    /// gated, taking only the <c>isCheckAvailability()==true</c> effects — any one whose
    /// <c>canSuspendDigisorption(permanent, cardEffect)</c> holds returns true (opponent-substitute availability);
    /// finally the own-Digimon fallthrough (<c>permanent.TopCard.Owner == this</c>). Substrate: AS-IS
    /// <c>gameContext.Players_ForTurnPlayer</c> → <see cref="GameContext.Players_ForTurnPlayer"/>;
    /// <c>permanent.TopCard.Owner.GetBattleAreaDigimons()</c> → <c>new Player(Context, owner).GetBattleAreaDigimons()</c>;
    /// <c>permanent.TopCard.Owner == this</c> → <c>== PlayerId</c>.</summary>
    public bool CanTapWhenAbsorbEvolution_CheckAvailability(Permanent permanent, ICardEffect cardEffect)
    {
        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                if (!permanent.IsSuspended && permanent.CanSuspend)
                {
                    if (new Player(Context, permanent.TopCard.Owner).GetBattleAreaDigimons().Contains(permanent))
                    {
                        // 吸収進化でタップできるデジモンの条件を変更させる効果
                        foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
                        {
                            foreach (Permanent permanent1 in player.GetFieldPermanents())
                            {
                                // 場のパーマネントの効果
                                foreach (ICardEffect cardEffect1 in permanent1.EffectList(EffectTiming.None))
                                {
                                    if (cardEffect1 is ICanSuspendByDigisorptionEffect)
                                    {
                                        if (cardEffect1.CanUse(null))
                                        {
                                            if (((ICanSuspendByDigisorptionEffect)cardEffect1).isCheckAvailability())
                                            {
                                                if (((ICanSuspendByDigisorptionEffect)cardEffect1).canSuspendDigisorption(permanent, cardEffect))
                                                {
                                                    return true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            // プレイヤーの効果
                            foreach (ICardEffect cardEffect1 in player.EffectList(EffectTiming.None))
                            {
                                if (cardEffect1 is ICanSuspendByDigisorptionEffect)
                                {
                                    if (cardEffect1.CanUse(null))
                                    {
                                        if (((ICanSuspendByDigisorptionEffect)cardEffect1).isCheckAvailability())
                                        {
                                            if (((ICanSuspendByDigisorptionEffect)cardEffect1).canSuspendDigisorption(permanent, cardEffect))
                                            {
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (permanent.TopCard.Owner == PlayerId)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>(RD-EXT3-03) AS-IS <c>Player.CanTapWhenAbsorbEvolution(permanent, cardEffect)</c> (Player.cs:1250-1326):
    /// the EXECUTION half of the &lt;Digisorption&gt; suspend-target judgement — same base gate (top card / un-suspended
    /// &amp; <see cref="Permanent.CanSuspend"/> / owner's battle-area Digimon) and same <c>Players_ForTurnPlayer</c> scan,
    /// but taking the <c>isCheckAvailability()==false</c> effects: the FIRST such effect short-circuits the whole
    /// judgement — <c>canSuspendDigisorption</c> true ⇒ return true, false ⇒ return false (AS-IS opponent-substitute
    /// override), before the own-Digimon fallthrough (<c>permanent.TopCard.Owner == this</c>). Substrate translations
    /// identical to <see cref="CanTapWhenAbsorbEvolution_CheckAvailability"/>.</summary>
    public bool CanTapWhenAbsorbEvolution(Permanent permanent, ICardEffect cardEffect)
    {
        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                if (!permanent.IsSuspended && permanent.CanSuspend)
                {
                    if (new Player(Context, permanent.TopCard.Owner).GetBattleAreaDigimons().Contains(permanent))
                    {
                        // 吸収進化でタップできるデジモンの条件を変更させる効果
                        foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
                        {
                            foreach (Permanent permanent1 in player.GetFieldPermanents())
                            {
                                // 場のパーマネントの効果
                                foreach (ICardEffect cardEffect1 in permanent1.EffectList(EffectTiming.None))
                                {
                                    if (cardEffect1 is ICanSuspendByDigisorptionEffect)
                                    {
                                        if (cardEffect1.CanUse(null))
                                        {
                                            if (!((ICanSuspendByDigisorptionEffect)cardEffect1).isCheckAvailability())
                                            {
                                                if (((ICanSuspendByDigisorptionEffect)cardEffect1).canSuspendDigisorption(permanent, cardEffect))
                                                {
                                                    return true;
                                                }
                                                else
                                                {
                                                    return false;
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            // プレイヤーの効果
                            foreach (ICardEffect cardEffect1 in player.EffectList(EffectTiming.None))
                            {
                                if (cardEffect1 is ICanSuspendByDigisorptionEffect)
                                {
                                    if (cardEffect1.CanUse(null))
                                    {
                                        if (!((ICanSuspendByDigisorptionEffect)cardEffect1).isCheckAvailability())
                                        {
                                            if (((ICanSuspendByDigisorptionEffect)cardEffect1).canSuspendDigisorption(permanent, cardEffect))
                                            {
                                                return true;
                                            }
                                            else
                                            {
                                                return false;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (permanent.TopCard.Owner == PlayerId)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    // ===== match-status =====

    /// <summary>(MIG5) AS-IS <c>Player.SetLose()</c> (Player.cs:119-122): mark this player as having lost —
    /// the same one-way flag <c>TerminalEvaluator</c> reads.</summary>
    public void SetLose() => Context.PlayerStatusController.MarkLose(PlayerId);

    // ===== memory (bridge W4) =====

    /// <summary>(bridge W4) AS-IS <c>Player.MemoryForPlayer</c> — the shared memory gauge read from THIS
    /// player's perspective. The headless gauge is single-signed and turn-player-relative (positive favors
    /// the turn player; the turn-end seam re-signs the gauge at the flip — AS-IS EndTurnProcess mirror), so it is
    /// negated for the non-turn player — the same mapping <c>CardEffectCommons.MemoryForPlayer(card)</c>
    /// and <c>AceOverflowGate.MemoryDelta</c> already use.</summary>
    public int MemoryForPlayer
    {
        get
        {
            int memory = Context.MemoryController.Current.Current;
            return Context.TurnController.Current.TurnPlayerId == PlayerId ? memory : -memory;
        }
    }

    /// <summary>(bridge W4; R3-W3c-4c B3 flip, retires design item MIG5-CANADDMEMORY) AS-IS
    /// <c>Player.CanAddMemory(cardEffect)</c> (Player.cs:1030-1075): the gauge cap (<c>MemoryForPlayer &gt;= 10</c>,
    /// :1032-1035), then the AS-IS-literal <c>ICannotAddMemoryEffect</c> LIVE scan over <c>Players_ForTurnPlayer</c>'s
    /// field permanents' + own player-scope <c>EffectList(None)</c> (:1037-1071), <c>CanUse(null)</c>-gated, calling
    /// <c>cannotAddMemory(this = the gaining player, cardEffect = the causing effect)</c>. Was the registry
    /// player-scope <c>CannotAddMemoryKey</c> binding scan; the factory now produces the kind-class
    /// <c>CannotAddMemoryClass</c> (no ToBinding) so the live scan is the sole reader.</summary>
    public bool CanAddMemory(ICardEffect cardEffect)
    {
        if (MemoryForPlayer >= 10)
        {
            return false;
        }

        foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect1 in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect1 is ICannotAddMemoryEffect && cardEffect1.CanUse(null)
                        && ((ICannotAddMemoryEffect)cardEffect1).cannotAddMemory(this, cardEffect))
                    {
                        return false;
                    }
                }
            }

            foreach (ICardEffect cardEffect1 in player.EffectList(EffectTiming.None))
            {
                if (cardEffect1 is ICannotAddMemoryEffect && cardEffect1.CanUse(null)
                    && ((ICannotAddMemoryEffect)cardEffect1).cannotAddMemory(this, cardEffect))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>(bridge W4) AS-IS <c>Player.AddMemory(plusMemory, cardEffect)</c> (Player.cs:1082-1123),
    /// IEnumerator→Task: no-op on zero (:1084-1087); a GAIN (≥1) with a cause is gated by
    /// <see cref="CanAddMemory"/> (:1089-1100); then the SHARED gauge moves in THIS player's favor (AS-IS
    /// PlayerID==0 subtracts / else adds, :1102-1110) with the ±10 clamp (:1112-1120 — the mirror controller's
    /// Set clamps). Headless turn-player-relative gauge ⇒ "this player's favor" = +plus for the turn player /
    /// −plus otherwise (see <see cref="MemoryForPlayer"/>). The AS-IS trailing <c>memoryObject.SetMemory()</c>
    /// (:1122) is UI (stripped). Applied directly on the memory controller — the AS-IS body writes the gauge
    /// directly too; the sink's AddMemoryKind is NOT used here because its amount convention is raw
    /// (turn-player-relative) and its gain gate keys on the mutation source's owner, not the gaining player
    /// (see docs/audit/rebuild_bridge_w4_notes.md).</summary>
    public Task AddMemory(int plusMemory, ICardEffect cardEffect)
    {
        if (plusMemory == 0)
        {
            return Task.CompletedTask;
        }

        if (plusMemory >= 1)
        {
            if (cardEffect != null)
            {
                if (!CanAddMemory(cardEffect))
                {
                    return Task.CompletedTask;
                }
            }
        }

        int delta = Context.TurnController.Current.TurnPlayerId == PlayerId ? plusMemory : -plusMemory;
        Context.MemoryController.Add(delta);

        return Task.CompletedTask;
    }

    /// <summary>(F1b) AS-IS <c>Player.SetFixedMemory(Memory, cardEffect)</c> (Player.cs:990-1026),
    /// IEnumerator→Task: when this write would RAISE this player's memory (<c>MemoryForPlayer &lt; Memory</c>,
    /// :992) and a causing effect is supplied, the gain must pass <see cref="CanAddMemory"/> or the whole write
    /// is skipped (:994-1002, AS-IS <c>yield break</c>); otherwise the SHARED gauge is SET so this player's
    /// memory reads <paramref name="Memory"/>. AS-IS stores the seat-absolute gauge (PlayerID==0 ⇒ −Memory /
    /// else +Memory, :1005-1013); the mirror gauge is turn-player-relative (see <see cref="MemoryForPlayer"/>),
    /// so it stores +Memory for the turn player / −Memory otherwise — the same mapping <see cref="AddMemory"/>
    /// uses. The AS-IS ±10 clamp (:1015-1023) is applied by the controller's <c>Set</c> (Clamp — same reliance
    /// as <see cref="AddMemory"/>); the trailing <c>memoryObject.SetMemory()</c> (:1025) is UI (stripped).</summary>
    public Task SetFixedMemory(int Memory, ICardEffect cardEffect)
    {
        if (MemoryForPlayer < Memory)
        {
            if (cardEffect != null)
            {
                if (!CanAddMemory(cardEffect))
                {
                    return Task.CompletedTask;
                }
            }
        }

        int gauge = Context.TurnController.Current.TurnPlayerId == PlayerId ? Memory : -Memory;
        Context.MemoryController.Set(gauge);

        return Task.CompletedTask;
    }
}

/// <summary>(bridge W4; batch-3 sibling added, retires the P5-log "no CanAddMemory extension" finding) AS-IS
/// card idiom <c>card.Owner.AddMemory(±N, activateClass)</c> (BT1_114 pattern) and its gate sibling
/// <c>card.Owner.CanAddMemory(activateClass)</c> (BT1_030/BT1_035/BT1_041/BT1_076/BT1_077 pattern): the mirror
/// <c>CardSource.Owner</c> is a bare <see cref="HeadlessPlayerId"/> (no live Player handle at the call site),
/// so both AS-IS instance calls are bridged as extensions — the match context resolves from the causing
/// effect's source card (every AS-IS caller passes the live activateClass) or the ambient match scope (the
/// same source <c>GManager.instance</c> uses), then delegates to the mirror <see cref="Player.AddMemory"/> /
/// <see cref="Player.CanAddMemory"/>.</summary>
public static class PlayerIdAsIsExtensions
{
    public static Task AddMemory(this HeadlessPlayerId player, int plusMemory, ICardEffect cardEffect)
    {
        EngineContext context = cardEffect?.EffectSourceCard?.Context
            ?? AmbientMatchContext.Current
            ?? throw new InvalidOperationException(
                "AddMemory needs a live match context — pass the causing ICardEffect (AS-IS activateClass) " +
                "or call inside a match scope.");
        return new Player(context, player).AddMemory(plusMemory, cardEffect);
    }

    /// <summary>(F1b) AS-IS card idiom <c>card.Owner.SetFixedMemory(N, activateClass)</c> (SetMemoryTo3TamerEffect
    /// / BT17_101 pattern): the bare <see cref="HeadlessPlayerId"/> is bridged to the mirror
    /// <see cref="Player.SetFixedMemory"/> the same way as <see cref="AddMemory"/> — the match context comes from
    /// the causing effect's source card (every AS-IS caller passes the live activateClass) or the ambient scope.</summary>
    public static Task SetFixedMemory(this HeadlessPlayerId player, int Memory, ICardEffect cardEffect)
    {
        EngineContext context = cardEffect?.EffectSourceCard?.Context
            ?? AmbientMatchContext.Current
            ?? throw new InvalidOperationException(
                "SetFixedMemory needs a live match context — pass the causing ICardEffect (AS-IS activateClass) " +
                "or call inside a match scope.");
        return new Player(context, player).SetFixedMemory(Memory, cardEffect);
    }

    public static bool CanAddMemory(this HeadlessPlayerId player, ICardEffect cardEffect)
    {
        EngineContext context = cardEffect?.EffectSourceCard?.Context
            ?? AmbientMatchContext.Current
            ?? throw new InvalidOperationException(
                "CanAddMemory needs a live match context — pass the causing ICardEffect (AS-IS activateClass) " +
                "or call inside a match scope.");
        return new Player(context, player).CanAddMemory(cardEffect);
    }

    /// <summary>(ST1 batch) AS-IS card idiom <c>card.Owner.GetBattleAreaDigimons()</c> (ST1_09 pattern): same
    /// bare-<see cref="HeadlessPlayerId"/>-needs-a-live-<see cref="Player"/>-handle shape as
    /// <see cref="AddMemory"/>/<see cref="CanAddMemory"/> above, no <c>ICardEffect</c> is available at this
    /// AS-IS call site so the match context comes from <see cref="AmbientMatchContext.Current"/> only.</summary>
    public static List<Permanent> GetBattleAreaDigimons(this HeadlessPlayerId player)
    {
        EngineContext context = AmbientMatchContext.Current
            ?? throw new InvalidOperationException(
                "GetBattleAreaDigimons needs a live match context — call inside a match scope.");
        return new Player(context, player).GetBattleAreaDigimons();
    }

    /// <summary>(P6C1) AS-IS card idiom <c>card.Owner.GetBattleAreaPermanents()</c> (BlastDNADigivolution.cs:29
    /// pattern): same ambient-context shape as <see cref="GetBattleAreaDigimons"/>.</summary>
    public static List<Permanent> GetBattleAreaPermanents(this HeadlessPlayerId player)
    {
        EngineContext context = AmbientMatchContext.Current
            ?? throw new InvalidOperationException(
                "GetBattleAreaPermanents needs a live match context — call inside a match scope.");
        return new Player(context, player).GetBattleAreaPermanents();
    }
}

/// <summary>(R6-P / RD-R6-02) Match-scoped backing for the AS-IS <see cref="Player"/> effect-list buckets. AS-IS
/// they are plain settable list fields on the persistent Player; the mirror Player is a transient view, so the
/// live lists live here keyed by (EngineContext, PlayerId) — the same substrate pattern as
/// <c>CEntity_EffectControllerStore</c> and <c>TurnStateMachine._isExecutingStore</c>. Each seat's holder is
/// created lazily with empty lists (AS-IS field initialisers).</summary>
internal sealed class PlayerEffectLists
{
    public List<Func<EffectTiming, ICardEffect>> PermanentEffects = new();
    public List<Func<EffectTiming, ICardEffect>> UntilEndBattleEffects = new();
    public List<Func<EffectTiming, ICardEffect>> UntilEachTurnEndEffects = new();
    public List<Func<EffectTiming, ICardEffect>> UntilOwnerTurnEndEffects = new();
    public List<Func<EffectTiming, ICardEffect>> UntilOwnerActivePhaseEffects = new();
    public List<Func<EffectTiming, ICardEffect>> UntilOpponentTurnEndEffects = new();
    public List<Func<EffectTiming, ICardEffect>> UntilCalculateFixedCostEffect = new();
    public List<Func<EffectTiming, ICardEffect>> UntilSecurityCheckEndEffects = new();
}

internal static class PlayerEffectListStore
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        EngineContext, System.Collections.Concurrent.ConcurrentDictionary<HeadlessPlayerId, PlayerEffectLists>> ByContext = new();

    public static PlayerEffectLists Get(EngineContext context, HeadlessPlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ByContext.GetOrCreateValue(context).GetOrAdd(playerId, static _ => new PlayerEffectLists());
    }
}

/// <summary>(isAI restore) Match-scoped backing for the AS-IS <see cref="Player"/> selection queue
/// (<c>Queue&lt;IPlayerSelection&gt; _playerSelectionQueue</c>, Player.cs:193 — a plain instance field on the
/// persistent AS-IS Player). The mirror Player is a transient view, so the live queue lives here keyed by
/// (EngineContext, PlayerId) — the same substrate pattern as <see cref="PlayerEffectListStore"/> above. Each
/// seat's queue is created lazily and empty, matching the AS-IS field initialiser.</summary>
internal static class PlayerSelectionQueueStore
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        EngineContext,
        System.Collections.Concurrent.ConcurrentDictionary<HeadlessPlayerId, Queue<PlayerSelection.IPlayerSelection>>> ByContext = new();

    public static Queue<PlayerSelection.IPlayerSelection> Get(EngineContext context, HeadlessPlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ByContext.GetOrCreateValue(context)
            .GetOrAdd(playerId, static _ => new Queue<PlayerSelection.IPlayerSelection>());
    }
}

/// <summary>(RD-P6C2-10 / RD-P6C3-D1) MINIMAL mirror of AS-IS <c>FieldCardFrame</c> (Player.cs:1542-1675) —
/// only the surface [Arts Digivolve] (<c>CardEffectFactory.ArtsDigivolveEffect</c>) and [App Fusion]
/// (<c>CardSource.CanAppFusionFromTargetPermanent</c>) consume through <see cref="Permanent.PermanentFrame"/>:
/// the frame's <see cref="FrameID"/> (the field-list index — the established no-slot-model ADAPTATION,
/// RD-P6C1-1/RD-P6C1-2), the frame's owner, the occupying permanent, and the battle-vs-breeding classification.
/// The Unity slot GameObject / Frame_Select / BoxCollider / click / canvas members and the fixed per-player
/// <c>fieldCardFrames</c> slot array are NOT ported (no slot/capacity model — the documented frame-model
/// convention, Permanent.cs:2839-2842). Battle/breeding is answered by ZONE MEMBERSHIP of the occupying
/// permanent — the sanctioned substrate translation of AS-IS <c>FieldCardFrame.isBattleAreaFrameID(FrameID)</c>
/// (which thresholds FrameID against the fixed slot count) — since the mirror has no slot array to threshold.</summary>
public sealed class FieldCardFrame
{
    private readonly EngineContext _context;
    private readonly Permanent? _framePermanent;

    public FieldCardFrame(EngineContext context, HeadlessPlayerId player, int frameID, Permanent? framePermanent)
    {
        _context = context;
        this.player = player;
        FrameID = frameID;
        _framePermanent = framePermanent;
    }

    /// <summary>AS-IS <c>FieldCardFrame.FrameID</c> (Player.cs:1551) — the field-list index (ADAPTATION).</summary>
    public int FrameID { get; }

    /// <summary>AS-IS <c>FieldCardFrame.player</c> (Player.cs:1554, a <c>Player</c>) — the frame's owner id.</summary>
    public HeadlessPlayerId player { get; }

    /// <summary>AS-IS <c>FieldCardFrame.GetFramePermanent()</c> (Player.cs:1578).</summary>
    public Permanent? GetFramePermanent() => _framePermanent;

    /// <summary>AS-IS <c>FieldCardFrame.IsEmptyFrame()</c> (Player.cs:1598).</summary>
    public bool IsEmptyFrame() => GetFramePermanent() == null;

    /// <summary>AS-IS <c>FieldCardFrame.IsBattleAreaFrame()</c> (Player.cs:1561) / the static
    /// <c>isBattleAreaFrameID(FrameID)</c> (:1557). Substrate translation (Permanent.cs:2839-2842 convention):
    /// battle-vs-breeding by ZONE MEMBERSHIP of the occupying permanent — no fixed slot array to threshold
    /// FrameID against. An empty frame (no occupant) is not a battle frame under this reading.</summary>
    public bool IsBattleAreaFrame() =>
        _framePermanent != null && new Player(_context, player).GetBattleAreaPermanents().Contains(_framePermanent);

    /// <summary>AS-IS <c>FieldCardFrame.isBreedingAreaFrame()</c> (Player.cs:1566).</summary>
    public bool isBreedingAreaFrame() => !IsBattleAreaFrame();
}
