namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
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

    // ===== match-status =====

    /// <summary>(MIG5) AS-IS <c>Player.SetLose()</c> (Player.cs:119-122): mark this player as having lost —
    /// the same one-way flag <c>TerminalEvaluator</c> reads.</summary>
    public void SetLose() => Context.PlayerStatusController.MarkLose(PlayerId);

    // ===== memory (bridge W4) =====

    /// <summary>(bridge W4) AS-IS <c>Player.MemoryForPlayer</c> — the shared memory gauge read from THIS
    /// player's perspective. The headless gauge is single-signed and turn-player-relative (positive favors
    /// the turn player; <c>HeadlessMainPhaseFlow</c> re-signs <c>|memory|</c> at turn switch), so it is
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

    /// <summary>(bridge W4, retires design item MIG5-CANADDMEMORY) AS-IS <c>Player.CanAddMemory(cardEffect)</c>
    /// (Player.cs:1030-1075): the gauge cap (<c>MemoryForPlayer &gt;= 10</c>, :1032-1035), then the
    /// ICannotAddMemoryEffect scan over both boards' + players' effects (:1037-1071). The mirror carrier of
    /// that AS-IS interface is the <c>CannotAddMemoryKey</c> player-scope continuous binding
    /// (<c>CardEffectFactory.CannotAddMemoryEffect</c>); this scan reproduces the sink's central AddMemoryKind
    /// gain gate (<c>MatchStateMutationSink.IsPlayerRestricted</c> — private to the sink, and keyed there on
    /// the mutation SOURCE's owner, while AS-IS keys on THIS gaining player) with the true AS-IS causing card
    /// (<c>cardEffect.EffectSourceCard</c>) fed to a <c>CardEffectCondition</c> predicate.</summary>
    public bool CanAddMemory(ICardEffect cardEffect)
    {
        if (MemoryForPlayer >= 10)
        {
            return false;
        }

        foreach (EffectRequest effect in Context.EffectRegistry.GetContinuousEffects(
            new EffectQueryContext(ContinuousRestrictionGate.Scope)))
        {
            IReadOnlyDictionary<string, object?> values = effect.Context.Values;
            if (!(values.TryGetValue(RestrictionHelpers.CannotAddMemoryKey, out object? raw) && raw is true) ||
                !(values.TryGetValue(PlayerScopeContinuousHelpers.PlayerScopeKey, out object? scopeRaw) && scopeRaw is true))
            {
                continue;
            }

            bool playerMatches =
                (values.TryGetValue(PlayerScopeContinuousHelpers.ScopeAnyPlayerKey, out object? anyRaw) && anyRaw is true) ||
                (values.TryGetValue(PlayerScopeContinuousHelpers.ScopePlayerIdKey, out object? pid) && pid is int id && id == PlayerId.Value);
            if (!playerMatches)
            {
                continue;
            }

            // (P1-PG) AS-IS gates the scan with cardEffect1.CanUse(null) — a conditional restriction whose
            // condition is currently false must not block the add.
            if (values.TryGetValue(ContinuousSelfModifierEffect.ConditionKey, out object? condRaw)
                && condRaw is Func<bool> condition && !condition())
            {
                continue;
            }

            // AS-IS cannotAddMemory(this, cardEffect) — the restriction's causing-effect predicate, fed the
            // TRUE causing card. No predicate = block every add.
            if (values.TryGetValue(RestrictionHelpers.CausingEffectPredicateKey, out object? predRaw)
                && predRaw is Func<CardSource, bool> predicate)
            {
                if (cardEffect?.EffectSourceCard is { } causingCard && predicate(causingCard))
                {
                    return false;
                }

                continue;
            }

            return false;
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
