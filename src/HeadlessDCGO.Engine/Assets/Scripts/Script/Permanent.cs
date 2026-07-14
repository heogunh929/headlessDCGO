namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections; // (R1-c) Hashtable — AS-IS HasBlitz/HasEvade/HasBarrier/HasAlliance CanTrigger builders.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Aliased (not a namespace import) to avoid pulling the sibling `...Script.CardEffectFactory` namespace
// into scope, which would clash with the CardEffectFactory type below.
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;


/// <summary>A read-only view of the permanent (digivolution stack) a card belongs to — the headless
/// stand-in for the original <c>Permanent</c> accessed via <c>CardSource.PermanentOfThisCard()</c>.</summary>
public sealed class PermanentView
{
    public PermanentView(DigivolutionStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        Stack = stack;
    }

    public DigivolutionStack Stack { get; }

    /// <summary>The under-cards (digivolution sources) of the permanent — mirrors
    /// <c>Permanent.DigivolutionCards</c>. <c>.Count</c> is the source count.</summary>
    public IReadOnlyList<StackedCard> DigivolutionCards => Stack.UnderCards;

    public bool IsEmpty => Stack.IsEmpty;

    /// <summary>The top card's instance id (the battling Digimon) — mirrors <c>Permanent.TopCard</c>.</summary>
    public HeadlessEntityId TopInstanceId => Stack.Cards.Count > 0 ? Stack.Cards[^1].InstanceId : default;
}


/// <summary>Minimal headless mirror of the original <c>Permanent</c> — used only for the signature of
/// card <c>permanentCondition</c> predicates. Player-scope effects scope to the owner's cards directly, so
/// the predicate body is not invoked by the headless evaluation (it exists for 1:1 source fidelity).</summary>
/// <summary>(PRIM-W5-0) A battle-area permanent view — the member surface card predicates read off
/// <c>permanent.*</c>. Backed by the engine: <see cref="TopCard"/> reuses <see cref="CardSource"/> for the
/// card-view members, DP folds continuous modifiers, and digivolution sources come from the stack.</summary>
public sealed class Permanent
{
    private readonly EngineContext _context;

    public Permanent(EngineContext context, HeadlessEntityId instanceId, HeadlessPlayerId ownerId, ChoiceZone? snapshotZone = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        InstanceId = instanceId;
        OwnerId = ownerId;
        SnapshotZone = snapshotZone;
    }

    /// <summary>(R1-a) AS-IS <c>new Permanent(id)</c> — the original resolves the owning player internally from
    /// the card object; the mirror resolves <see cref="OwnerId"/> from the instance record (falling back to the
    /// controller/owner-less default for an abstract fixture that never registered one). Lets a DP reader spell
    /// the AS-IS surface <c>new Permanent(context, id).DP</c> without threading an owner.</summary>
    public Permanent(EngineContext context, HeadlessEntityId instanceId)
        : this(
            context ?? throw new ArgumentNullException(nameof(context)),
            instanceId,
            context.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? rec) && rec is not null
                ? rec.OwnerId
                : default)
    {
    }

    public HeadlessEntityId InstanceId { get; }

    public HeadlessPlayerId OwnerId { get; }

    /// <summary>(D-2) The PRE-removal field zone of a card that has ALREADY left the field, captured from the
    /// driving leave event's <c>ZoneFrom</c>. Non-null only on the transient subject view an OnLeaveFieldAnyone
    /// gate builds (<see cref="CardEffectCommons.CanTriggerOnPermanentLeave"/>): the AS-IS leave batch is stacked
    /// while the leaving permanent is STILL on the battle area (CardController.cs:3748, before RemoveField), so its
    /// <c>IsPermanentExistsOnOpponentBattleAreaDigimon</c> gate reads TRUE — but headless has already moved the
    /// card to the trash by collect time, so a LIVE zone read would read FALSE. When set, the field-membership
    /// checks (<see cref="CardEffectCommons.IsPermanentExistsOnBattleArea"/> /
    /// <see cref="CardEffectCommons.IsPermanentExistsOnBreedingArea"/>) answer from this snapshot instead of the
    /// live zone, reproducing the AS-IS pre-removal truth. Null for every normally-constructed permanent, so no
    /// other gate is affected.</summary>
    public ChoiceZone? SnapshotZone { get; }

    // (EFFECT-MODEL REBUILD, design item CARDSOURCE-EQUALITY) AS-IS relies on stable per-permanent object identity
    // for `==`/`!=`/`Contains` (CanActivate's `currentPermanent != PermanentWhenTriggered`, factory
    // `permanent == targetPermanent`, `PermanentsForTurnPlayer.Contains(p)`, …). The mirror Permanent is a VIEW
    // reconstructed on every access, so without value equality every such comparison is reference-unequal and
    // silently wrong. Identity = the permanent's top-card INSTANCE (InstanceId) within the same match (_context).
    // NOTE: a permanent whose top changes (de-digivolve) gets a new InstanceId — same-moment comparisons (the
    // common case) are correct; the AS-IS across-time `PermanentWhenTriggered` edge is handled by the InstanceId
    // capture at trigger time (the trigger snapshot stores the then-top id), so this identity is sufficient.
    public override bool Equals(object? obj) =>
        obj is Permanent other && InstanceId.Equals(other.InstanceId) && ReferenceEquals(_context, other._context);

    public override int GetHashCode() => InstanceId.GetHashCode();

    public static bool operator ==(Permanent? left, Permanent? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Permanent? left, Permanent? right) => !(left == right);

    /// <summary>The top (battling) card of this permanent as a <see cref="CardSource"/>.</summary>
    public CardSource TopCard => new(_context, InstanceId, OwnerId);

    /// <summary>(R1-a) AS-IS <c>Permanent.HasDP</c> (Permanent.cs:146-189): only a (treated-as) Digimon has DP,
    /// and a Digi-Egg without printed DP has none, and no active <c>IDontHaveDPEffect</c> strips it. The scan is
    /// the AS-IS live enumeration over EVERY player's field permanents' EffectList (NOT ForTurnPlayer, and NO
    /// player-level EffectList loop — verbatim AS-IS shape).</summary>
    public bool HasDP
    {
        get
        {
            if (!IsDigimon)
            {
                return false;
            }

            if (!TopCard.HasDP && TopCard.IsDigiEgg)
            {
                return false;
            }

            #region Effect of not having DP
            foreach (Player player in new GameContext(_context).Players)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    foreach (ICardEffect cardEffect1 in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect1 is IDontHaveDPEffect)
                        {
                            if (cardEffect1.CanUse(null))
                            {
                                if (((IDontHaveDPEffect)cardEffect1).DontHaveDP(this))
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            return true;
        }
    }

    /// <summary>(MIG2 substrate guard) Whether ANY dp value is defined for this permanent (instance or printed).
    /// AS-IS real Digimon always print DP, so its DP-rule predicates never meet a DP-less Digimon; headless
    /// abstract fixtures do — the D-2 sweep decision ("only when DP is actually DEFINED") is preserved by
    /// gating the rule predicates on this.</summary>
    public bool IsDpDefined =>
        (_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
            && i.Metadata.TryGetValue("dp", out object? raw) && raw is int)
        || TopCard.HasDP;

    /// <summary>(R1-a) AS-IS <c>Permanent.GetDP(ignorePermanent)</c> (Permanent.cs:327-497): the <see cref="DP"/>
    /// computation with an OPTIONAL permanent whose effects are skipped (an attacker/blocker computing DP without
    /// its own about-to-leave contribution). Identical to <see cref="DP"/> EXCEPT (a) the field-permanent loop
    /// skips <paramref name="ignorePermanent"/>, and (b) the NotIsUpDown group is folded in
    /// <c>OrderBy(ActivatedTime)</c> order (the <see cref="DP"/> property omits the ordering — verbatim AS-IS
    /// quirk). ADAPTATION: AS-IS <c>TopCard.CanNotBeAffected(cardEffect)</c> → the established
    /// <c>CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId)</c> id-adaptation on the mirror CardSource.</summary>
    public int GetDP(Permanent ignorePermanent = null)
    {
        int DP = -1;

        if (HasDP)
        {
            DP = BaseDP;

            #region DP By Effect

            List<ICardEffect> cardEffects_ChangeDP = new List<ICardEffect>();

            foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
            {
                #region Effects of permanents in play
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    if (ignorePermanent != null)
                    {
                        if (permanent == ignorePermanent)
                            continue;
                    }
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IChangeDPEffect)
                        {
                            if (cardEffect.CanUse(null))
                            {
                                if (((IChangeDPEffect)cardEffect).PermanentCondition(this))
                                {
                                    if (((IChangeDPEffect)cardEffect).IsMinusDP())
                                    {
                                        if (this.ImmuneFromDPMinus(cardEffect))
                                        {
                                            continue;
                                        }
                                    }

                                    if (!TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId))
                                    {
                                        cardEffects_ChangeDP.Add(cardEffect);
                                    }
                                }
                            }
                        }
                    }
                }
                #endregion

                #region Effects of face up security
                foreach (CardSource cardSource in player.SecurityCards)
                {
                    if (cardSource.IsFlipped)
                        continue;

                    foreach (ICardEffect cardEffect in cardSource.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IChangeDPEffect)
                        {
                            if (cardEffect.CanUse(null))
                            {
                                if (((IChangeDPEffect)cardEffect).PermanentCondition(this))
                                {
                                    if (((IChangeDPEffect)cardEffect).IsMinusDP())
                                    {
                                        if (this.ImmuneFromDPMinus(cardEffect))
                                        {
                                            continue;
                                        }
                                    }

                                    if (!TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId))
                                    {
                                        cardEffects_ChangeDP.Add(cardEffect);
                                    }
                                }
                            }
                        }
                    }
                }
                #endregion

                #region Player effect
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IChangeDPEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            if (((IChangeDPEffect)cardEffect).PermanentCondition(this))
                            {
                                if (((IChangeDPEffect)cardEffect).IsMinusDP())
                                {
                                    if (this.ImmuneFromDPMinus(cardEffect))
                                    {
                                        continue;
                                    }
                                }

                                if (!TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId))
                                {
                                    cardEffects_ChangeDP.Add(cardEffect);
                                }
                            }
                        }
                    }
                }
                #endregion
            }

            List<ICardEffect> cardEffects_ChangeDP_isUpDown = new List<ICardEffect>();
            List<ICardEffect> cardEffects_ChangeDP_NotIsUpDown = new List<ICardEffect>();

            foreach (ICardEffect cardEffect in cardEffects_ChangeDP)
            {
                if (cardEffect is IChangeDPEffect)
                {
                    if (cardEffect.CanUse(null))
                    {
                        if (((IChangeDPEffect)cardEffect).IsUpDown())
                        {
                            cardEffects_ChangeDP_isUpDown.Add(cardEffect);
                        }

                        else
                        {
                            cardEffects_ChangeDP_NotIsUpDown.Add(cardEffect);
                        }
                    }
                }
            }

            foreach (ICardEffect cardEffect in cardEffects_ChangeDP_isUpDown)
            {
                if (cardEffect is IChangeDPEffect)
                {
                    if (cardEffect.CanUse(null))
                    {
                        DP = ((IChangeDPEffect)cardEffect).GetDP(DP, this);
                    }
                }
            }

            DP += LinkedDP;

            foreach (ICardEffect cardEffect in cardEffects_ChangeDP_NotIsUpDown.OrderBy(cardEffect => cardEffect.ActivatedTime))
            {
                if (cardEffect is IChangeDPEffect)
                {
                    if (cardEffect.CanUse(null))
                    {
                        DP = ((IChangeDPEffect)cardEffect).GetDP(DP, this);
                    }
                }
            }
            #endregion

            #region DP Boosts
            foreach (DPBoost boost in Boosts)
            {
                DP += boost.DP;
            }
            #endregion

            if (DP < 0)
            {
                DP = 0;
            }
        }

        return DP;
    }

    /// <summary>(R1-a) AS-IS <c>Permanent.DP</c> (Permanent.cs:499-668): -1 when the permanent has no DP at all
    /// (<see cref="HasDP"/> false — the <c>IsNotHavingDP</c> rule marker). Otherwise seeds from <see cref="BaseDP"/>
    /// and folds every active <c>IChangeDPEffect</c> — scanned LIVE over each turn-ordered player's field
    /// permanents, FACE-UP security cards, and the player itself — as the isUpDown group, then <c>+= LinkedDP</c>,
    /// then the NotIsUpDown group (NO ActivatedTime ordering here — see <see cref="GetDP"/>), then the per-card
    /// <see cref="Boosts"/>, clamped at 0. ADAPTATION as documented on <see cref="GetDP"/>.</summary>
    public int DP
    {
        get
        {
            int DP = -1;

            if (HasDP)
            {
                DP = BaseDP;

                #region DP By Effect

                List<ICardEffect> cardEffects_ChangeDP = new List<ICardEffect>();

                foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
                {
                    #region Effects of permanents in play
                    foreach (Permanent permanent in player.GetFieldPermanents())
                    {
                        foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                        {
                            if (cardEffect is IChangeDPEffect)
                            {
                                if (cardEffect.CanUse(null))
                                {
                                    if (((IChangeDPEffect)cardEffect).PermanentCondition(this))
                                    {
                                        if (((IChangeDPEffect)cardEffect).IsMinusDP())
                                        {
                                            if (this.ImmuneFromDPMinus(cardEffect))
                                            {
                                                continue;
                                            }
                                        }

                                        if (!TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId))
                                        {
                                            cardEffects_ChangeDP.Add(cardEffect);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    #endregion

                    #region Effects of face up security
                    foreach (CardSource cardSource in player.SecurityCards)
                    {
                        if (cardSource.IsFlipped)
                            continue;

                        foreach (ICardEffect cardEffect in cardSource.EffectList(EffectTiming.None))
                        {
                            if (cardEffect is IChangeDPEffect)
                            {
                                if (cardEffect.CanUse(null))
                                {
                                    if (((IChangeDPEffect)cardEffect).PermanentCondition(this))
                                    {
                                        if (((IChangeDPEffect)cardEffect).IsMinusDP())
                                        {
                                            if (this.ImmuneFromDPMinus(cardEffect))
                                            {
                                                continue;
                                            }
                                        }

                                        if (!TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId))
                                        {
                                            cardEffects_ChangeDP.Add(cardEffect);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    #endregion

                    #region Player effect
                    foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IChangeDPEffect)
                        {
                            if (cardEffect.CanUse(null))
                            {
                                if (((IChangeDPEffect)cardEffect).PermanentCondition(this))
                                {
                                    if (((IChangeDPEffect)cardEffect).IsMinusDP())
                                    {
                                        if (this.ImmuneFromDPMinus(cardEffect))
                                        {
                                            continue;
                                        }
                                    }

                                    if (!TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId))
                                    {
                                        cardEffects_ChangeDP.Add(cardEffect);
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                }

                List<ICardEffect> cardEffects_ChangeDP_isUpDown = new List<ICardEffect>();
                List<ICardEffect> cardEffects_ChangeDP_NotIsUpDown = new List<ICardEffect>();

                foreach (ICardEffect cardEffect in cardEffects_ChangeDP)
                {
                    if (cardEffect is IChangeDPEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            if (((IChangeDPEffect)cardEffect).IsUpDown())
                            {
                                cardEffects_ChangeDP_isUpDown.Add(cardEffect);
                            }

                            else
                            {
                                cardEffects_ChangeDP_NotIsUpDown.Add(cardEffect);
                            }
                        }
                    }
                }

                foreach (ICardEffect cardEffect in cardEffects_ChangeDP_isUpDown)
                {
                    if (cardEffect is IChangeDPEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            DP = ((IChangeDPEffect)cardEffect).GetDP(DP, this);
                        }
                    }
                }

                DP += LinkedDP;

                foreach (ICardEffect cardEffect in cardEffects_ChangeDP_NotIsUpDown)
                {
                    if (cardEffect is IChangeDPEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            DP = ((IChangeDPEffect)cardEffect).GetDP(DP, this);
                        }
                    }
                }
                #endregion

                #region DP Boosts
                foreach (DPBoost boost in Boosts)
                {
                    DP += boost.DP;
                }
                #endregion

                if (DP < 0)
                {
                    DP = 0;
                }
            }

            return DP;
        }
    }

    /// <summary>(A3 / P6C3 re-fold) Mirror of <c>Permanent.Level</c> (Permanent.cs:48-102): seeds from the
    /// top card's (already card-level-folded) level, then EVERY active
    /// <see cref="IChangePermanentLevelEffect"/> transforms it — the AS-IS scan over all field permanents'
    /// and players' EffectList (the flip's live enumeration replaces the retired registry-key fold).
    /// The mirror keeps its -1 no-level sentinel (AS-IS 1145140; consumers guard on HasLevel first).</summary>
    public int Level
    {
        get
        {
            int level = TopCard.Level;
            foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IChangePermanentLevelEffect transform && cardEffect.CanUse(null))
                        {
                            level = transform.GetPermanentLevel(level, this);
                        }
                    }
                }

                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IChangePermanentLevelEffect transform && cardEffect.CanUse(null))
                    {
                        level = transform.GetPermanentLevel(level, this);
                    }
                }
            }

            return level;
        }
    }

    public bool HasNoDigivolutionCards => DigivolutionCards.Count == 0;

    #region トークンかどうか
    /// <summary>(R1-c) AS-IS <c>Permanent.IsToken</c> (Permanent.cs:3416-3430).</summary>
    public bool IsToken
    {
        get
        {
            if (TopCard != null)
            {
                if (TopCard.IsToken)
                {
                    return true;
                }
            }

            return false;
        }
    }
    #endregion

    #region Is a Digimon card
    /// <summary>(R1-c) AS-IS <c>Permanent.IsDigimon</c> (Permanent.cs:3438-3511) — the single chokepoint: a
    /// face-down top is NOT a Digimon; a printed Digimon OR Digi-Egg IS; else an active
    /// <c>ITreatAsDigimonEffect</c> accepts it — first over the TOP card's own effects, then over each
    /// turn-ordered player's field permanents (scanned via <see cref="EffectList_Added"/>, NOT
    /// <see cref="EffectList"/>, because the latter re-checks IsDigimon and would stack-overflow — AS-IS
    /// comment preserved) and the player itself. ADAPTATION:
    /// <c>gameContext.Players_ForTurnPlayer</c> → <c>new GameContext(_context).Players_ForTurnPlayer</c>.</summary>
    public bool IsDigimon
    {
        get
        {
            if (TopCard != null)
            {
                if (TopCard.IsFlipped)
                    return false;

                if (TopCard.IsDigimon || TopCard.IsDigiEgg)
                {
                    return true;
                }

                #region Effect on TopCard
                foreach (ICardEffect cardEffect in TopCard.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ITreatAsDigimonEffect)
                    {
                        if (cardEffect.CanTrigger(null))
                        {
                            if (((ITreatAsDigimonEffect)cardEffect).IsDigimon(this))
                            {
                                return true;
                            }
                        }
                    }
                }
                #endregion

                #region Effect of treating it as a Digimon
                foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
                {
                    foreach (Permanent permanent in player.GetFieldPermanents())
                    {
                        #region Effects of permanents in play
                        foreach (ICardEffect cardEffect in permanent.EffectList_Added(EffectTiming.None))//This can never be EffectList, as EffectList_forCard checks Isdigimon and this causes a stack overflow
                        {
                            if (cardEffect is ITreatAsDigimonEffect)
                            {
                                if (cardEffect.CanTrigger(null))
                                {
                                    if (((ITreatAsDigimonEffect)cardEffect).IsDigimon(this))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                        #endregion
                    }

                    #region player effect
                    foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is ITreatAsDigimonEffect)
                        {
                            if (cardEffect.CanTrigger(null))
                            {
                                if (((ITreatAsDigimonEffect)cardEffect).IsDigimon(this))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    #endregion
                }
                #endregion
            }

            return false;
        }
    }
    #endregion

    #region Is a Tamer card
    /// <summary>(R1-c) AS-IS <c>Permanent.IsTamer</c> (Permanent.cs:3515-3532): a face-down top is not a Tamer.</summary>
    public bool IsTamer
    {
        get
        {
            if (TopCard != null)
            {
                if (TopCard.IsFlipped)
                    return false;

                if (TopCard.IsTamer)
                {
                    return true;
                }
            }

            return false;
        }
    }
    #endregion

    #region Is an Option card
    /// <summary>(R1-c) AS-IS <c>Permanent.IsOption</c> (Permanent.cs:3536-3550): a DualCard is never an option
    /// while it is a permanent.</summary>
    public bool IsOption
    {
        get
        {
            if (TopCard != null)
            {
                if (TopCard.IsOption && !TopCard.IsDigimon) //DualCard is never an option while it is a permanent
                {
                    return true;
                }
            }

            return false;
        }
    }
    #endregion

    // ===== (R1-c) keyword-predicate getters (AS-IS Permanent.cs) ==============================================
    // Each getter is the AS-IS body verbatim (scan scope / interface / gate order / quirks preserved per getter —
    // NOT uniformised). Two established substrate adaptations apply throughout:
    //   * GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer → new GameContext(_context).Players_ForTurnPlayer
    //   * TopCard.CanNotBeAffected(cardEffect) → TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId)
    // and one new to HasBlocker: GManager.instance.attackProcess → AttackProcess.For(_context) (the per-context
    // instance accessor). Types outside this file's namespace (ActivateClass/CannotBlockClass in ...Script.CardEffects,
    // AttackProcess in ...Script) are fully qualified to avoid importing namespaces that clash with local type names.

    #region Unblockable
    /// <summary>(R1-c) AS-IS <c>Permanent.IsUnblockable</c> (Permanent.cs:2376-2393).</summary>
    public bool IsUnblockable
    {
        get
        {
            foreach (ICardEffect cardEffect in this.EffectList(EffectTiming.None))
            {
                if (cardEffect is HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.CannotBlockClass)
                {
                    if (cardEffect.EffectName == "Unblockable")
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
    #endregion

    #region Has Blocker
    /// <summary>(R1-c) AS-IS <c>Permanent.HasBlocker</c> (Permanent.cs:2397-2482): the attacking-Digimon-Collision
    /// short-circuit, then an <c>IBlockerEffect</c> scan over each turn-ordered player's field permanents,
    /// face-up security, and the player itself.</summary>
    public bool HasBlocker
    {
        get
        {
            #region if attacking digimon has collision
            if (HeadlessDCGO.Engine.Assets.Scripts.Script.AttackProcess.For(_context).ActiveAttack() && CardEffectCommons.IsPermanentExistsOnBattleArea(this))
            {
                Permanent attackingPermanent = HeadlessDCGO.Engine.Assets.Scripts.Script.AttackProcess.For(_context).AttackingPermanent;

                if (attackingPermanent != null
                    && attackingPermanent.TopCard !=null
                    && attackingPermanent.TopCard.Owner != TopCard.Owner
                    && attackingPermanent.HasCollision)
                {
                    HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ActivateClass fakeCollisionClass = new();
                    fakeCollisionClass.SetUpICardEffect("Collision", _ => true, attackingPermanent.TopCard);

                    if (!TopCard.CanNotBeAffected(fakeCollisionClass.EffectSourceCard?.InstanceId))//Check can be affected by opponent's Digimon effects
                        return true;
                }
            }
            #endregion

            foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
            {
                #region Effects of permanents in play
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IBlockerEffect)
                        {
                            if (cardEffect.CanTrigger(null))
                            {
                                if (((IBlockerEffect)cardEffect).IsBlocker(this))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                #endregion

                #region Effects of faceup security
                foreach (CardSource source in player.SecurityCards)
                {
                    if (source.IsFlipped)
                        continue;

                    foreach (ICardEffect cardEffect in source.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IBlockerEffect)
                        {
                            if (cardEffect.CanTrigger(null))
                            {
                                if (((IBlockerEffect)cardEffect).IsBlocker(this))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                #endregion

                #region プレイヤーの効果
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IBlockerEffect)
                    {
                        if (cardEffect.CanTrigger(null))
                        {
                            if (((IBlockerEffect)cardEffect).IsBlocker(this))
                            {
                                return true;
                            }
                        }
                    }
                }
                #endregion
            }

            return false;
        }
    }
    #endregion

    #region Has Jamming
    /// <summary>(R1-c) AS-IS <c>Permanent.HasJamming</c> (Permanent.cs:2486-2536): an
    /// <c>ICanNotBeDestroyedByBattleEffect</c> named "Jamming" (with its PermanentCondition) over each
    /// turn-ordered player's field permanents and the player (NO security scan — AS-IS omits it).</summary>
    public bool HasJamming
    {
        get
        {
            foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    #region 場のパーマネントの効果
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is ICanNotBeDestroyedByBattleEffect)
                        {
                            if (cardEffect.CanTrigger(null))
                            {
                                if (cardEffect.EffectName == "Jamming")
                                {
                                    if (((ICanNotBeDestroyedByBattleEffect)cardEffect).PermanentCondition(this))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                }

                #region プレイヤーの効果
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanNotBeDestroyedByBattleEffect)
                    {
                        if (cardEffect.CanTrigger(null))
                        {
                            if (cardEffect.EffectName == "Jamming")
                            {
                                if (((ICanNotBeDestroyedByBattleEffect)cardEffect).PermanentCondition(this))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                #endregion
            }

            return false;
        }
    }
    #endregion

    #region Has Ice Clad
    /// <summary>(R1-c) AS-IS <c>Permanent.HasIceclad</c> (Permanent.cs:2540-2581): an <c>IIcecladEffect</c> scan.
    /// QUIRK preserved: the first inner loop iterates <c>EffectList(None)</c> (THIS permanent's own effects,
    /// re-scanned once per turn-player), NOT the player's field permanents.</summary>
    public bool HasIceclad
    {
        get
        {
            foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
            {
                #region Ice clad permanent effects
                foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
                {
                    if (cardEffect is IIcecladEffect)
                    {
                        if (cardEffect.CanTrigger(null))
                        {
                            if (((IIcecladEffect)cardEffect).HasIceclad(this))
                            {
                                return true;
                            }
                        }
                    }
                }
                #endregion

                #region Ice clad Player Effects
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IIcecladEffect)
                    {
                        if (cardEffect.CanTrigger(null))
                        {
                            if (((IIcecladEffect)cardEffect).HasIceclad(this))
                            {
                                return true;
                            }
                        }
                    }
                }
                #endregion
            }

            return false;
        }
    }
    #endregion

    #region Whether this permanent has Pierce
    /// <summary>(R1-c) AS-IS <c>Permanent.HasPierce</c> (Permanent.cs:2585-2613): a Digimon whose own
    /// <c>OnDetermineDoSecurityCheck</c> effects include an active "Pierce"/"Piercing" ActivateICardEffect.</summary>
    public bool HasPierce
    {
        get
        {
            if (IsDigimon)
            {
                foreach (ICardEffect cardEffect in EffectList(EffectTiming.OnDetermineDoSecurityCheck))
                {
                    if (cardEffect is ActivateICardEffect)
                    {
                        if (cardEffect.CanTrigger(CardEffectCommons.PierceCheckHashtableOfPermanent(this)))
                        {
                            if (cardEffect.EffectName == "Pierce")
                            {
                                return true;
                            }

                            if (cardEffect.EffectName == "Piercing")
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
    #endregion

    #region Has Reboot
    /// <summary>(R1-c) AS-IS <c>Permanent.HasReboot</c> (Permanent.cs:2617-2683): an <c>IRebootEffect</c> scan over
    /// each turn-ordered player's field permanents, face-up security, and the player.</summary>
    public bool HasReboot
    {
        get
        {
            foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    #region 場のパーマネントの効果
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IRebootEffect)
                        {
                            if (cardEffect.CanTrigger(null))
                            {
                                if (((IRebootEffect)cardEffect).HasReboot(this))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    #endregion
                }

                #region Effects of faceup security
                foreach (CardSource source in player.SecurityCards)
                {
                    if (source.IsFlipped)
                        continue;

                    foreach (ICardEffect cardEffect in source.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IRebootEffect)
                        {
                            if (cardEffect.CanTrigger(null))
                            {
                                if (((IRebootEffect)cardEffect).HasReboot(this))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                #endregion

                #region プレイヤーの効果
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IRebootEffect)
                    {
                        if (cardEffect.CanTrigger(null))
                        {
                            if (((IRebootEffect)cardEffect).HasReboot(this))
                            {
                                return true;
                            }
                        }
                    }
                }
                #endregion
            }

            return false;
        }
    }
    #endregion

    #region Has Raid
    /// <summary>(R1-c) AS-IS <c>Permanent.HasRaid</c> (Permanent.cs:2687-2704): a "Raid" ActivateICardEffect among
    /// this permanent's own <c>OnAllyAttack</c> effects.</summary>
    public bool HasRaid
    {
        get
        {
            foreach (ICardEffect cardEffect in this.EffectList(EffectTiming.OnAllyAttack))
            {
                if (cardEffect is ActivateICardEffect)
                {
                    if (cardEffect.EffectName == "Raid")
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
    #endregion

    #region Has Rush
    /// <summary>(R1-c) AS-IS <c>Permanent.HasRush</c> (Permanent.cs:2708-2774): an <c>IRushEffect</c> scan over each
    /// turn-ordered player's field permanents, face-up security, and the player.</summary>
    public bool HasRush
    {
        get
        {
            foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    #region 場のパーマネントの効果
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IRushEffect)
                        {
                            if (cardEffect.CanTrigger(null))
                            {
                                if (((IRushEffect)cardEffect).HasRush(this))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    #endregion
                }

                #region Effects of Face-up Security
                foreach (CardSource source in player.SecurityCards)
                {
                    if (source.IsFlipped)
                        continue;

                    foreach (ICardEffect cardEffect in source.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IRushEffect)
                        {
                            if (cardEffect.CanTrigger(null))
                            {
                                if (((IRushEffect)cardEffect).HasRush(this))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                #endregion

                #region プレイヤーの効果
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IRushEffect)
                    {
                        if (cardEffect.CanTrigger(null))
                        {
                            if (((IRushEffect)cardEffect).HasRush(this))
                            {
                                return true;
                            }
                        }
                    }
                }
                #endregion
            }

            return false;
        }
    }
    #endregion

    #region Has Retaliation
    /// <summary>(R1-c) AS-IS <c>Permanent.HasRetaliation</c> (Permanent.cs:2778-2789).</summary>
    public bool HasRetaliation
    {
        get
        {
            if (RetaliationCount >= 1)
            {
                return true;
            }

            return false;
        }
    }
    #endregion

    #region Retaliation Count
    /// <summary>(R1-c) AS-IS <c>Permanent.RetaliationCount</c> (Permanent.cs:2793-2815): the count of active
    /// "Retaliation" ActivateICardEffects among this permanent's own <c>OnDestroyedAnyone</c> effects.</summary>
    public int RetaliationCount
    {
        get
        {
            int count = 0;

            foreach (ICardEffect cardEffect in EffectList(EffectTiming.OnDestroyedAnyone))
            {
                if (cardEffect is ActivateICardEffect)
                {
                    if (cardEffect.EffectName == "Retaliation")
                    {
                        if (cardEffect.CanTrigger(CardEffectCommons.OnDeletionCheckHashtableOfPermanent(this)))
                        {
                            count++;
                        }
                    }
                }
            }

            return count;
        }
    }
    #endregion

    #region HasAscension
    /// <summary>(R1-c) AS-IS <c>Permanent.HasAscension</c> (Permanent.cs:2819-2839).</summary>
    public bool HasAscension
    {
        get
        {
            foreach (ICardEffect cardEffect in EffectList(EffectTiming.OnDestroyedAnyone))
            {
                if (cardEffect is ActivateICardEffect)
                {
                    if (cardEffect.EffectName == "Ascension")
                    {
                        if (cardEffect.CanTrigger(CardEffectCommons.OnDeletionCheckHashtableOfPermanent(this)))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
    #endregion

    #region Has Fortitude
    /// <summary>(R1-c) AS-IS <c>Permanent.HasFortitude</c> (Permanent.cs:2843-2863).</summary>
    public bool HasFortitude
    {
        get
        {
            foreach (ICardEffect cardEffect in EffectList(EffectTiming.OnDestroyedAnyone))
            {
                if (cardEffect is ActivateICardEffect)
                {
                    if (cardEffect.EffectName == "Fortitude")
                    {
                        if (cardEffect.CanTrigger(CardEffectCommons.OnDeletionCheckHashtableOfPermanent(this)))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
    #endregion

    #region Has Blitz
    /// <summary>(R1-c) AS-IS <c>Permanent.HasBlitz</c> (Permanent.cs:2867-2890): a permanent whose own
    /// <c>OnEnterFieldAnyone</c> effects include one that triggers under the WhenDigivolution OR OnPlay check
    /// and whose description contains "Blitz".</summary>
    public bool HasBlitz
    {
        get
        {
            foreach (ICardEffect cardEffect in EffectList(EffectTiming.OnEnterFieldAnyone))
            {
                if (cardEffect is ActivateICardEffect)
                {
                    if (cardEffect.CanTrigger(CardEffectCommons.WhenDigivolutionCheckHashtableOfPermanent(this)) || cardEffect.CanTrigger(CardEffectCommons.OnPlayCheckHashtableOfPermanent(this)))
                    {
                        if (!string.IsNullOrEmpty(cardEffect.EffectDiscription))
                        {
                            if (cardEffect.EffectDiscription.Contains("Blitz"))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
    #endregion

    #region Has Evade
    /// <summary>(R1-c) AS-IS <c>Permanent.HasEvade</c> (Permanent.cs:2894-2919).</summary>
    public bool HasEvade
    {
        get
        {
            foreach (ICardEffect cardEffect in this.EffectList(EffectTiming.WhenPermanentWouldBeDeleted))
            {
                if (cardEffect is ActivateICardEffect)
                {
                    Hashtable hashtable = new Hashtable()
                    {
                        {"Permanents", new List<Permanent>() { this }}
                    };

                    if (cardEffect.CanTrigger(hashtable))
                    {
                        if (cardEffect.EffectName == "Evade")
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
    #endregion

    #region Has Mind Link
    /// <summary>(R1-c) AS-IS <c>Permanent.HasMindLink</c> (Permanent.cs:2923-2946).</summary>
    public bool HasMindLink
    {
        get
        {
            foreach (ICardEffect cardEffect in EffectList(EffectTiming.OnDeclaration))
            {
                if (cardEffect is ActivateICardEffect)
                {
                    if (cardEffect.CanTrigger(null))
                    {
                        if (!String.IsNullOrEmpty(cardEffect.EffectDiscription))
                        {
                            if (cardEffect.EffectDiscription.Contains("Mind Link"))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
    #endregion

    #region Has Barrier
    /// <summary>(R1-c) AS-IS <c>Permanent.HasBarrier</c> (Permanent.cs:2950-2974).</summary>
    public bool HasBarrier
    {
        get
        {
            foreach (ICardEffect cardEffect in this.EffectList(EffectTiming.WhenPermanentWouldBeDeleted))
            {
                if (cardEffect is ActivateICardEffect)
                {
                    Hashtable hashtable = new Hashtable();
                    hashtable.Add("Permanents", new List<Permanent>() { this });
                    hashtable.Add("battle", new IBattle(null, null, null));

                    if (cardEffect.CanTrigger(hashtable))
                    {
                        if (cardEffect.EffectName == "Barrier")
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
    #endregion

    #region Has Alliance
    /// <summary>(R1-c) AS-IS <c>Permanent.HasAlliance</c> (Permanent.cs:2978-3039): a "Alliance"
    /// <c>OnAllyAttack</c> effect (triggered with this as AttackingPermanent) over each turn-ordered player's
    /// field permanents, face-up security, and the player.</summary>
    public bool HasAlliance
    {
        get
        {
            Hashtable hashtable = new Hashtable(){
                {"AttackingPermanent", this}
            };

            foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    #region Permanent Effects
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.OnAllyAttack))
                    {
                        if (cardEffect.EffectName == "Alliance")
                        {
                            if (cardEffect.CanTrigger(hashtable))
                            {
                                return true;
                            }
                        }
                    }
                    #endregion
                }

                #region Effects of faceup security
                foreach (CardSource source in player.SecurityCards)
                {
                    if (source.IsFlipped)
                        continue;

                    foreach (ICardEffect cardEffect in source.EffectList(EffectTiming.OnAllyAttack))
                    {
                        if (cardEffect.EffectName == "Alliance")
                        {
                            if (cardEffect.CanTrigger(hashtable))
                            {
                                return true;
                            }
                        }
                    }
                }
                #endregion

                #region Player Effects
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.OnAllyAttack))
                {
                    if (cardEffect.EffectName == "Alliance")
                    {
                        if (cardEffect.CanTrigger(hashtable))
                        {
                            return true;
                        }
                    }
                }
                #endregion
            }

            return false;
        }
    }
    #endregion

    #region Has Collision
    /// <summary>(R1-c) AS-IS <c>Permanent.HasCollision</c> (Permanent.cs:3043-3108): an <c>ICollisionEffect</c>
    /// scan over each turn-ordered player's <c>OnCounterTiming</c> field permanents, face-up security, and the
    /// player.</summary>
    public bool HasCollision
    {
        get
        {
            foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    #region Permanent Effects
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.OnCounterTiming))
                    {
                        if (cardEffect is ICollisionEffect)
                        {
                            if (cardEffect.CanTrigger(null))
                            {
                                if (((ICollisionEffect)cardEffect).HasCollision(this))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    #endregion
                }

                #region Effects of faceup security
                foreach (CardSource source in player.SecurityCards)
                {
                    if (source.IsFlipped)
                        continue;

                    foreach (ICardEffect cardEffect in source.EffectList(EffectTiming.OnCounterTiming))
                    {
                        if (cardEffect is ICollisionEffect)
                        {
                            if (cardEffect.CanTrigger(null))
                            {
                                if (((ICollisionEffect)cardEffect).HasCollision(this))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                #endregion

                #region Player Effects
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.OnCounterTiming))
                {
                    if (cardEffect is ICollisionEffect)
                    {
                        if (cardEffect.CanTrigger(null))
                        {
                            if (((ICollisionEffect)cardEffect).HasCollision(this))
                            {
                                return true;
                            }
                        }
                    }
                }
                #endregion
            }

            return false;
        }
    }
    #endregion

    #region Has Partition
    /// <summary>(R1-c) AS-IS <c>Permanent.HasPartition</c> (Permanent.cs:3113-3129).</summary>
    public bool HasPartition
    {
        get
        {
            foreach (ICardEffect cardEffect in this.EffectList(EffectTiming.WhenPermanentWouldBeDeleted))
            {
                if (cardEffect is ActivateICardEffect)
                {
                    if (cardEffect.EffectName == "Partition")
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
    #endregion

    #region Has Scapegoat
    /// <summary>(R1-c) AS-IS <c>Permanent.HasScapegoat</c> (Permanent.cs:3134-3151).</summary>
    public bool HasScapegoat
    {
        get
        {
            foreach (ICardEffect cardEffect in this.EffectList(EffectTiming.WhenPermanentWouldBeDeleted))
            {
                if (cardEffect is ActivateICardEffect)
                {
                    if (cardEffect.EffectName == "<Scapegoat>")
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
    #endregion

    #region 消滅時効化を持つか
    /// <summary>(R1-c) AS-IS <c>Permanent.HasOnDeletionEffect</c> (Permanent.cs:3155-3178).</summary>
    public bool HasOnDeletionEffect
    {
        get
        {
            foreach (ICardEffect cardEffect in EffectList(EffectTiming.OnDestroyedAnyone))
            {
                if (cardEffect is ActivateICardEffect)
                {
                    if (cardEffect.CanTrigger(CardEffectCommons.OnDeletionCheckHashtableOfPermanent(this)))
                    {
                        if (!string.IsNullOrEmpty(cardEffect.EffectDiscription))
                        {
                            if (cardEffect.IsOnDeletion)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
    #endregion

    /// <summary>AS-IS <c>Permanent.IsSuspended</c> is a public FIELD (Permanent.cs:1956) — readable AND
    /// assignable. Get = the sink's <c>isSuspended</c> instance flag. (P6C1) Set = the AS-IS direct-assignment
    /// idiom (e.g. the PlayCardClass <c>playFailed</c> snapshot RESTORE, CardController.cs:945): a raw state
    /// write on the same flag — deliberately NO CanNotSuspend/Unsuspend gate and NO OnTapped/OnUntapped
    /// emission, exactly like the AS-IS field assignment (effect-driven suspends go through the sink's
    /// Suspend/Unsuspend kinds instead).</summary>
    public bool IsSuspended
    {
        get =>
            _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
            && i.Metadata.TryGetValue("isSuspended", out object? raw) && raw is bool b && b;
        set
        {
            if (_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null)
            {
                var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                {
                    ["isSuspended"] = value,
                };
                _context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
            }
        }
    }

    /// <summary>(P6C1) AS-IS <c>Permanent.oldIsTapped_playCard</c> (Permanent.cs:45, a public auto-property):
    /// the pre-play suspension snapshot the play pipeline stamps on every non-turn-player field permanent and
    /// restores on a failed play (CardController.cs:432/:945; also read by BT8_102/RB1_023/BT16_046). The AS-IS
    /// carrier is a field on the persistent Unity component; the mirror <see cref="Permanent"/> is a transient
    /// VIEW, so the value lives in a per-match store keyed by <see cref="InstanceId"/> (same substrate pattern
    /// as <c>CEntity_EffectControllerStore</c>). Default false, as an unset AS-IS bool field.</summary>
    public bool oldIsTapped_playCard
    {
        get => _context.TryGetService(out OldIsTappedPlayCardStore? store) && store is not null
            && store.Values.TryGetValue(InstanceId, out bool old) && old;
        set
        {
            if (!_context.TryGetService(out OldIsTappedPlayCardStore? store) || store is null)
            {
                store = new OldIsTappedPlayCardStore();
                _context.RegisterService(store);
            }

            store.Values[InstanceId] = value;
        }
    }

    /// <summary>(P6C1) Per-match backing store for <see cref="oldIsTapped_playCard"/>.</summary>
    private sealed class OldIsTappedPlayCardStore
    {
        public Dictionary<HeadlessEntityId, bool> Values { get; } = new Dictionary<HeadlessEntityId, bool>();
    }

    /// <summary>(P6C3) AS-IS <c>Permanent.IsDestroyedByBattle</c> (Permanent.cs:3666, a public
    /// auto-property the battle pipeline stamps on a battle loser and the WhenDeleteOpponentDigimon* /
    /// Pierce gates read). Mirror carrier = the instance <c>deletedByBattle</c> metadata flag the live
    /// substrate battle pipeline ALREADY stamps (<see cref="Headless.Runtime.BattleResolver.DeletedByBattleKey"/>)
    /// — so a gate reading this view sees exactly the live pipeline's answer, and the Hashtable builders'
    /// AS-IS-verbatim <c>{ IsDestroyedByBattle = true }</c> writes land on the same shared flag.</summary>
    public bool IsDestroyedByBattle
    {
        get => _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null
            && record.Metadata.TryGetValue(Headless.Runtime.BattleResolver.DeletedByBattleKey, out object? raw) && raw is true;
        set
        {
            if (_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null)
            {
                var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                {
                    [Headless.Runtime.BattleResolver.DeletedByBattleKey] = value,
                };
                _context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
            }
        }
    }

    /// <summary>(P6C3) 1:1 of AS-IS <c>Permanent.CanSuspend</c> (Permanent.cs:3698-3742): NO active
    /// <see cref="ICanNotSuspendEffect"/> (scanned over every field permanent's and player's EffectList,
    /// <c>CanUse(null)</c>-gated) forbids suspending THIS permanent. AS-IS iterates
    /// <c>gameContext.Players</c> (seat order — an order-insensitive any-match).</summary>
    public bool CanSuspend
    {
        get
        {
            foreach (Player player in new GameContext(_context).Players)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is ICanNotSuspendEffect gate && cardEffect.CanUse(null) && gate.CanNotSuspend(this))
                        {
                            return false;
                        }
                    }
                }

                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanNotSuspendEffect gate && cardEffect.CanUse(null) && gate.CanNotSuspend(this))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    /// <summary>The digivolution (under-)cards of this permanent (mirror of <c>DigivolutionCards</c>).</summary>
    public IReadOnlyList<CardSource> DigivolutionCards
    {
        get
        {
            DigivolutionStack stack = DigivolutionStackReader.Read(_context.CardInstanceRepository, _context.CardRepository, InstanceId);
            return stack.UnderCards.Select(u => new CardSource(_context, u.InstanceId, OwnerId)).ToArray();
        }
    }

    /// <summary>(P6 stage A) AS-IS <c>Permanent.cardSources</c> (Permanent.cs:880) — ALL cards of this
    /// permanent: the top card, the digivolution sources AND the linked cards
    /// (AS-IS <c>DigivolutionCards = cardSources.Filter(c != TopCard &amp;&amp; !LinkedCards.Contains(c))</c>,
    /// Permanent.cs:888). Order mirrors the AS-IS list: top card at index 0 (AS-IS
    /// <c>AddDigivolutionCardsTop</c> inserts new sources at index 1 — directly under the top), then the
    /// digivolution sources TOP-MOST FIRST (the reverse of the substrate <see cref="DigivolutionCards"/> view,
    /// whose stack read yields bottom→top), then the linked cards (AS-IS AddLinkCard appends). Consumed by the
    /// flip's per-card effect-membership scans (CEntity_EffectController.GetCardEffects,
    /// AS-IS CEntity_EffectController.cs:29-168) and gates like <c>cardSources.Contains(card)</c>.</summary>
    public List<CardSource> cardSources
    {
        get
        {
            var all = new List<CardSource> { TopCard };
            IReadOnlyList<CardSource> under = DigivolutionCards;
            for (int i = under.Count - 1; i >= 0; i--)
            {
                all.Add(under[i]);
            }

            all.AddRange(LinkedCards);
            return all;
        }
    }

    // ===== (P6 stage A) AS-IS Permanent.EffectList family (Permanent.cs:1373-1573) — the flip's live
    // per-permanent effect enumeration (consumed by AutoProcessing.GetSkillInfos, AS-IS AutoProcessing.cs:795).

    /// <summary>AS-IS <c>Permanent.EffectList(EffectTiming)</c> (Permanent.cs:1373-1376).</summary>
    public List<ICardEffect> EffectList(EffectTiming timing)
    {
        return EffectList_ForCard(timing, TopCard);
    }

    /// <summary>AS-IS <c>Permanent.EffectList_Added(EffectTiming)</c> (Permanent.cs:1380-1492) — the effects
    /// GRANTED to this permanent (AS-IS UntilOwnerDrawPhase/UntilOwnerTurnEnd/UntilEachTurnEnd/…/
    /// PermanentEffects buckets fed by GiveEffectToPermanent). The mirror has NO new-model permanent-grant
    /// store yet: every current grant lowers to a substrate <c>EffectBinding</c> (GiveEffectToPermanent bridge
    /// → registry) which the legacy gates read — so the NEW-model list is empty today. design item
    /// P6A-PERMANENT-EFFECTLIST-ADDED (docs/audit/rebuild_p6_stageA_notes.md). (AS-IS tail backfills
    /// <c>SetEffectSourceCard(TopCard)</c> + <c>SetIsInheritedEffect(false)</c> on each granted effect —
    /// preserved here for when the store lands.)</summary>
    public List<ICardEffect> EffectList_Added(EffectTiming timing)
    {
        _ = timing;
        return new List<ICardEffect>();
    }

    /// <summary>AS-IS <c>Permanent.EffectList_ForCard(EffectTiming, CardSource)</c> (Permanent.cs:1495-1573):
    /// the per-card membership split — a flipped source contributes nothing; a NON-top source requires a
    /// Digimon host and contributes only its <c>IsInheritedEffect</c> effects (plus <c>IsLinkedEffect</c>
    /// effects of a linked card); the TOP card contributes only its non-inherited, non-linked effects. Then
    /// the granted effects (<see cref="EffectList_Added"/>) and the <c>SetEffectSourceCard</c> back-fill.</summary>
    public List<ICardEffect> EffectList_ForCard(EffectTiming timing, CardSource _cardSource)
    {
        List<ICardEffect> _EffectList = new List<ICardEffect>();

        if (TopCard != null && _cardSource != null)
        {
            foreach (CardSource cardSource in cardSources)
            {
                if (cardSource != null)
                {
                    if (!cardSource.IsFlipped)
                    {
                        bool isTopCard = cardSource == TopCard;

                        if (!isTopCard)
                        {
                            if (!IsDigimon)
                            {
                                continue;
                            }
                        }

                        foreach (ICardEffect cardEffect in cardSource.cEntity_EffectController.GetCardEffects(timing, cardSource))
                        {
                            if (cardEffect != null)
                            {
                                #region Entity, Inherited and Link effects

                                if (cardEffect.IsInheritedEffect && !isTopCard)
                                {
                                    _EffectList.Add(cardEffect);
                                    continue;
                                }

                                if (cardEffect.IsLinkedEffect && cardSource.IsLinked)
                                {
                                    _EffectList.Add(cardEffect);
                                    continue;
                                }

                                if (isTopCard && !cardEffect.IsInheritedEffect && !cardEffect.IsLinkedEffect)
                                {
                                    _EffectList.Add(cardEffect);
                                }

                                #endregion
                            }
                        }
                    }
                }
            }

            foreach (ICardEffect cardEffect in EffectList_Added(timing))
            {
                if (cardEffect != null)
                {
                    _EffectList.Add(cardEffect);
                }
            }

            foreach (ICardEffect cardEffect in _EffectList)
            {
                if (cardEffect != null)
                {
                    if (cardEffect.EffectSourceCard == null)
                    {
                        cardEffect.SetEffectSourceCard(_cardSource);
                    }
                }
            }
        }

        return _EffectList;
    }

    /// <summary>(R1-a) AS-IS <c>Permanent.BaseDP</c> (Permanent.cs:193-322): the origin DP — seeds from
    /// <c>TopCard.BaseCardDP + TopCard.BaseDP</c> then folds every active <c>IChangeBaseDPEffect</c> (scanned LIVE
    /// over each turn-ordered player's field permanents and the player itself — NO security-card scan, unlike
    /// <see cref="DP"/>) as the isUpDown group, then the NotIsUpDown group in <c>OrderBy(ActivatedTime)</c> order,
    /// clamped at 0. NO LinkedDP and NO Boosts (those are DP-only). ADAPTATION as documented on
    /// <see cref="GetDP"/>.</summary>
    public int BaseDP
    {
        get
        {
            int BaseDP = 0;

            if (HasDP)
            {
                BaseDP = TopCard.BaseCardDP;
                BaseDP += TopCard.BaseDP;

                #region 基礎DPを変更する効果

                List<ICardEffect> cardEffects_ChangeDP = new List<ICardEffect>();

                foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
                {
                    foreach (Permanent permanent in player.GetFieldPermanents())
                    {
                        #region 場のパーマネントの効果
                        foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                        {
                            if (cardEffect is IChangeBaseDPEffect)
                            {
                                if (cardEffect.CanUse(null))
                                {
                                    if (((IChangeBaseDPEffect)cardEffect).PermanentCondition(this))
                                    {
                                        if (((IChangeBaseDPEffect)cardEffect).IsMinusDP())
                                        {
                                            if (this.ImmuneFromDPMinus(cardEffect))
                                            {
                                                continue;
                                            }
                                        }

                                        if (!TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId))
                                        {
                                            cardEffects_ChangeDP.Add(cardEffect);
                                        }
                                    }
                                }
                            }
                        }
                        #endregion
                    }

                    #region プレイヤーの効果
                    foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IChangeBaseDPEffect)
                        {
                            if (cardEffect.CanUse(null))
                            {
                                if (((IChangeBaseDPEffect)cardEffect).PermanentCondition(this))
                                {
                                    if (((IChangeBaseDPEffect)cardEffect).IsMinusDP())
                                    {
                                        if (this.ImmuneFromDPMinus(cardEffect))
                                        {
                                            continue;
                                        }
                                    }

                                    if (!TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId))
                                    {
                                        cardEffects_ChangeDP.Add(cardEffect);
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                }

                List<ICardEffect> cardEffects_ChangeDP_isUpDown = new List<ICardEffect>();
                List<ICardEffect> cardEffects_ChangeDP_NotIsUpDown = new List<ICardEffect>();

                foreach (ICardEffect cardEffect in cardEffects_ChangeDP)
                {
                    if (cardEffect is IChangeBaseDPEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            if (((IChangeBaseDPEffect)cardEffect).IsUpDown())
                            {
                                cardEffects_ChangeDP_isUpDown.Add(cardEffect);
                            }

                            else
                            {
                                cardEffects_ChangeDP_NotIsUpDown.Add(cardEffect);
                            }
                        }
                    }
                }

                foreach (ICardEffect cardEffect in cardEffects_ChangeDP_isUpDown)
                {
                    if (cardEffect is IChangeBaseDPEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            BaseDP = ((IChangeBaseDPEffect)cardEffect).GetDP(BaseDP, this);
                        }
                    }
                }

                foreach (ICardEffect cardEffect in cardEffects_ChangeDP_NotIsUpDown.OrderBy(cardEffect => cardEffect.ActivatedTime))
                {
                    if (cardEffect is IChangeBaseDPEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            BaseDP = ((IChangeBaseDPEffect)cardEffect).GetDP(BaseDP, this);
                        }
                    }
                }

                #endregion

                if (BaseDP < 0)
                {
                    BaseDP = 0;
                }
            }

            return BaseDP;
        }
    }

    /// <summary>(R1-a) AS-IS <c>Permanent.LinkedDP</c> (Permanent.cs:670, a <c>{ get; set; }</c> auto-property):
    /// the accumulated DP of this permanent's attached link cards, folded into <see cref="DP"/> between the
    /// isUpDown and NotIsUpDown groups. ADAPTATION: the AS-IS field maps to the SAME instance metadata key
    /// (<see cref="LinkHelpers.LinkedDpKey"/>) that <see cref="LinkHelpers"/> AddLink/RemoveLink already write, so
    /// existing link records are visible and an AS-IS <c>LinkedDP +=/-=</c> lands on the shared value.</summary>
    public int LinkedDP
    {
        get =>
            _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
                ? LinkHelpers.ReadLinkedDp(i.Metadata)
                : 0;
        set
        {
            if (_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null)
            {
                var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                {
                    [LinkHelpers.LinkedDpKey] = value,
                };
                _context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
            }
        }
    }

    /// <summary>(R1-a) AS-IS <c>Permanent.Boosts</c> (Permanent.cs:672, a <c>List&lt;DPBoost&gt;</c> field) folded
    /// into <see cref="DP"/> at the very end (<c>foreach (DPBoost boost in Boosts) DP += boost.DP</c>). ADAPTATION:
    /// the AS-IS in-memory list maps to a read view over the id→dp instance metadata
    /// (<see cref="DpBoostHelpers.DpBoostsKey"/>) that <see cref="DpBoostHelpers"/> AddBoost/RemoveBoost manage;
    /// the fold only reads <c>boost.DP</c>, so the reconstructed <see cref="DPBoost"/> carries a null Condition.</summary>
    public List<DPBoost> Boosts
    {
        get
        {
            var boosts = new List<DPBoost>();
            if (_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
                && i.Metadata.TryGetValue(DpBoostHelpers.DpBoostsKey, out object? raw)
                && raw is IReadOnlyDictionary<string, int> map)
            {
                foreach (KeyValuePair<string, int> pair in map)
                {
                    boosts.Add(new DPBoost(pair.Key, pair.Value, null));
                }
            }

            return boosts;
        }
    }

    /// <summary>(R1-a) AS-IS nested <c>Permanent.DPBoost</c> (Permanent.cs:687-699): a named additive DP boost.</summary>
    public class DPBoost
    {
        public DPBoost(string id, int dp, Func<bool> cond)
        {
            ID = id;
            DP = dp;
            Condition = cond;
        }

        public string ID = "";
        public int DP = 0;
        public Func<bool> Condition = null;
    }

    /// <summary>(R1-a) AS-IS <c>Permanent.ImmuneFromDPMinus(ICardEffect)</c> (Permanent.cs:703-742): true when
    /// some active <c>IImmuneFromDPMinusEffect</c> (scanned LIVE over EVERY player's field permanents AND the
    /// player itself — AS-IS iterates <c>Players</c>, seat order, an order-insensitive any-match) grants THIS
    /// permanent immunity from <paramref name="cardEffect"/>'s DP-minus.</summary>
    public bool ImmuneFromDPMinus(ICardEffect cardEffect)
    {
        foreach (Player player in new GameContext(_context).Players)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect1 in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect1 is IImmuneFromDPMinusEffect)
                    {
                        if (cardEffect1.CanUse(null))
                        {
                            if (((IImmuneFromDPMinusEffect)cardEffect1).ImmuneFromDPMinus(this, cardEffect))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            foreach (ICardEffect cardEffect1 in player.EffectList(EffectTiming.None))
            {
                if (cardEffect1 is IImmuneFromDPMinusEffect)
                {
                    if (cardEffect1.CanUse(null))
                    {
                        if (((IImmuneFromDPMinusEffect)cardEffect1).ImmuneFromDPMinus(this, cardEffect))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    #region Number of sheets to undergo security check
    /// <summary>(R1-b) AS-IS <c>Permanent.InvertSecutiryValue</c> (Permanent.cs:1670-1729): folds every active
    /// <c>IInvertSAttackEffect</c> (scanned LIVE over each turn-ordered player's field permanents AND the player
    /// itself), gated by <c>CanUse(null) &amp;&amp; !TopCard.CanNotBeAffected</c>, then clamps to [-1,1]. ADAPTATION
    /// as documented on <see cref="GetDP"/>: <c>gameContext.Players_ForTurnPlayer</c> →
    /// <c>new GameContext(_context).Players_ForTurnPlayer</c>; <c>TopCard.CanNotBeAffected(cardEffect)</c> →
    /// <c>TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId)</c>; <c>Mathf.Clamp</c> →
    /// <c>Math.Clamp</c>.</summary>
    public int InvertSecutiryValue
    {
        get
        {
            int Invert = 0;

            List<ICardEffect> cardEffects_InvertStrike = new List<ICardEffect>();

            foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    #region Effects of permanents in play
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IInvertSAttackEffect)
                        {
                            if (cardEffect.CanUse(null))
                            {
                                if (!TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId))
                                {
                                    cardEffects_InvertStrike.Add(cardEffect);
                                }
                            }
                        }
                    }
                    #endregion
                }

                #region player effect
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IInvertSAttackEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            if (!TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId))
                            {
                                cardEffects_InvertStrike.Add(cardEffect);
                            }
                        }
                    }
                }
                #endregion
            }

            foreach (ICardEffect cardEffect in cardEffects_InvertStrike)
            {
                Invert = ((IInvertSAttackEffect)cardEffect).InversionValue(this, Invert);
            }

            return Math.Clamp(Invert, -1, 1);
        }
    }

    /// <summary>(R1-b) AS-IS <c>Permanent.SecurityAttackChanges</c> (Permanent.cs:1731-1802): the list of DELTA
    /// values contributed by each active <c>IChangeSAttackEffect</c> whose <c>isUpDown()==UpDownValue</c> (scanned
    /// LIVE over each turn-ordered player's field permanents + the player, gated by
    /// <c>CanUse(null) &amp;&amp; !TopCard.CanNotBeAffected</c>), each measured as <c>GetSAttack(1,this,0) - 1</c>
    /// when non-zero. ADAPTATION as documented on <see cref="InvertSecutiryValue"/>.</summary>
    public List<int> SecurityAttackChanges
    {
        get
        {
            List<int> SecurityAttackChanges = new List<int>();

            int Strike = 1;

            List<ICardEffect> cardEffects_ChangeDirectStrike = new List<ICardEffect>();

            foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    #region 場のパーマネントの効果
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IChangeSAttackEffect)
                        {
                            if (cardEffect.CanUse(null))
                            {
                                if (!TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId))
                                {
                                    if (((IChangeSAttackEffect)cardEffect).isUpDown() == CalculateOrder.UpDownValue)
                                    {
                                        cardEffects_ChangeDirectStrike.Add(cardEffect);
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                }

                #region プレイヤーの効果
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IChangeSAttackEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            if (!TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId))
                            {
                                if (((IChangeSAttackEffect)cardEffect).isUpDown() == CalculateOrder.UpDownValue)
                                {
                                    cardEffects_ChangeDirectStrike.Add(cardEffect);
                                }
                            }
                        }
                    }
                }
                #endregion
            }

            foreach (ICardEffect cardEffect in cardEffects_ChangeDirectStrike)
            {
                if (cardEffect is IChangeSAttackEffect)
                {
                    if (cardEffect.CanUse(null))
                    {
                        int Strike1 = ((IChangeSAttackEffect)cardEffect).GetSAttack(Strike, this, 0);

                        if (Strike1 != Strike)
                        {
                            SecurityAttackChanges.Add(Strike1 - Strike);
                        }
                    }
                }
            }

            return SecurityAttackChanges;
        }
    }

    /// <summary>(R1-b) AS-IS <c>Permanent.HasSecurityAttackChanges</c> (Permanent.cs:1805-1815): false for a
    /// non-Digimon, else true when <see cref="SecurityAttackChanges"/> has at least one entry.</summary>
    public bool HasSecurityAttackChanges
    {
        get
        {
            if (!IsDigimon)
            {
                return false;
            }

            return SecurityAttackChanges.Count >= 1;
        }
    }

    /// <summary>(R1-b) AS-IS <c>Permanent.Strike_AllowMinus</c> (Permanent.cs:1818-1936): the number of security
    /// cards checked (allowing a negative intermediate). Seeds a constant <c>1</c>, collects every active
    /// <c>IChangeSAttackEffect</c> (scanned LIVE over each turn-ordered player's field permanents + the player)
    /// gated by <c>PermanentCondition(this) &amp;&amp; CanUse(null) &amp;&amp; !TopCard.CanNotBeAffected</c> — note the
    /// PermanentCondition-FIRST gate order (asymmetric vs <see cref="SecurityAttackChanges"/>, preserved verbatim) —
    /// then splits by <c>isUpDown()</c> and folds in the order UpToConstant → UpDownValue → DownToConstant, each via
    /// <c>GetSAttack(Strike,this,InvertSecutiryValue)</c>. ADAPTATION as documented on
    /// <see cref="InvertSecutiryValue"/>.</summary>
    public int Strike_AllowMinus
    {
        get
        {
            int Strike = 1;

            #region Effect of changing the number of sheets to undergo security check

            List<ICardEffect> cardEffects_ChangeDirectStrike = new List<ICardEffect>();

            foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    #region Effects of permanents in play
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IChangeSAttackEffect)
                        {
                            if (((IChangeSAttackEffect)cardEffect).PermanentCondition(this))
                            {
                                if (cardEffect.CanUse(null))
                                {
                                    if (!TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId))
                                    {
                                        cardEffects_ChangeDirectStrike.Add(cardEffect);
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                }

                #region player effect
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IChangeSAttackEffect)
                    {
                        if (((IChangeSAttackEffect)cardEffect).PermanentCondition(this))
                        {
                            if (cardEffect.CanUse(null))
                            {
                                if (!TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId))
                                {
                                    cardEffects_ChangeDirectStrike.Add(cardEffect);
                                }
                            }
                        }
                    }
                }
                #endregion
            }

            List<ICardEffect> cardEffects_ChangeDirectStrike_UpToConstant = new List<ICardEffect>();
            List<ICardEffect> cardEffects_ChangeDirectStrike_UpDownValue = new List<ICardEffect>();
            List<ICardEffect> cardEffects_ChangeDirectStrike_DownToConstant = new List<ICardEffect>();

            foreach (ICardEffect cardEffect in cardEffects_ChangeDirectStrike)
            {
                if (cardEffect is IChangeSAttackEffect)
                {
                    if (cardEffect.CanUse(null))
                    {
                        switch (((IChangeSAttackEffect)cardEffect).isUpDown())
                        {
                            case CalculateOrder.UpToConstant:
                                cardEffects_ChangeDirectStrike_UpToConstant.Add(cardEffect);
                                break;

                            case CalculateOrder.UpDownValue:
                                cardEffects_ChangeDirectStrike_UpDownValue.Add(cardEffect);
                                break;

                            case CalculateOrder.DownToConstant:
                                cardEffects_ChangeDirectStrike_DownToConstant.Add(cardEffect);
                                break;
                        }
                    }
                }
            }

            foreach (ICardEffect cardEffect in cardEffects_ChangeDirectStrike_UpToConstant)
            {
                if (cardEffect is IChangeSAttackEffect)
                {
                    if (cardEffect.CanUse(null))
                    {
                        Strike = ((IChangeSAttackEffect)cardEffect).GetSAttack(Strike, this, InvertSecutiryValue);
                    }
                }
            }

            foreach (ICardEffect cardEffect in cardEffects_ChangeDirectStrike_UpDownValue)
            {
                if (cardEffect is IChangeSAttackEffect)
                {
                    if (cardEffect.CanUse(null))
                    {
                        Strike = ((IChangeSAttackEffect)cardEffect).GetSAttack(Strike, this, InvertSecutiryValue);
                    }
                }
            }

            foreach (ICardEffect cardEffect in cardEffects_ChangeDirectStrike_DownToConstant)
            {
                if (cardEffect is IChangeSAttackEffect)
                {
                    if (cardEffect.CanUse(null))
                    {
                        Strike = ((IChangeSAttackEffect)cardEffect).GetSAttack(Strike, this, InvertSecutiryValue);
                    }
                }
            }
            #endregion

            return Strike;
        }
    }

    /// <summary>(R1-b) AS-IS <c>Permanent.Strike</c> (Permanent.cs:1938-1951): <see cref="Strike_AllowMinus"/>
    /// clamped at 0.</summary>
    public int Strike
    {
        get
        {
            int Strike = Strike_AllowMinus;

            if (Strike < 0)
            {
                Strike = 0;
            }

            return Strike;
        }
    }
    #endregion

    // ===== (MIG2) link / rule-process members (AS-IS Permanent.cs) =============================================

    /// <summary>(MIG2) AS-IS <c>Permanent.LinkedCards</c> (Permanent.cs:1041) as live views (newest first —
    /// the substrate list mirrors the AS-IS insert-at-0 ordering).</summary>
    public List<CardSource> LinkedCards
    {
        get
        {
            if (!_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? host) || host is null)
            {
                return new List<CardSource>();
            }

            return LinkHelpers.ReadLinkedCardIds(host.Metadata)
                .Select(id => new CardSource(
                    _context,
                    id,
                    _context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? link) && link is not null
                        ? link.OwnerId
                        : OwnerId))
                .ToList();
        }
    }

    /// <summary>(MIG2) AS-IS <c>Permanent.LinkedMax</c> (Permanent.cs:896): base 1 folded with active
    /// <c>IChangeLinkMaxEffect</c>s (the M-4 continuous linkedMaxDelta fold).</summary>
    public int LinkedMax => LinkHelpers.ResolveLinkedMax(_context, InstanceId);

    /// <summary>(MIG2) AS-IS <c>Permanent.HasNoLinkCards</c> (Permanent.cs:3958).</summary>
    public bool HasNoLinkCards => LinkedCards.Count == 0;

    /// <summary>(MIG2) AS-IS <c>Permanent.IsPlaceToTrashDueToNotHavingDP</c> (Permanent.cs:3694, default true;
    /// effects may clear the flag).</summary>
    public bool IsPlaceToTrashDueToNotHavingDP =>
        !(_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
            && i.Metadata.TryGetValue(GameFlowProcessor.PlaceToTrashDueToNoDpKey, out object? optOut) && optOut is false);

    /// <summary>(MIG2) AS-IS <c>Permanent.IsPlayedOptionPermanent</c> (Permanent.cs:3946, default false — an
    /// Option a card effect legitimately keeps on the battle area).</summary>
    public bool IsPlayedOptionPermanent =>
        _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? p) && p is not null
            && p.Metadata.TryGetValue(GameFlowProcessor.IsPlayedOptionPermanentKey, out object? played) && played is true;

    /// <summary>(MIG2) AS-IS <c>Permanent.CanBeDestroyed()</c> (Permanent.cs:3186-3229): no active
    /// <c>ICanNotBeDestroyedEffect</c> protects this permanent — the same Delete/Prevent replacement set the
    /// mutation sink consults (<c>IsDeletionPreventedByContinuous</c>), evaluated predicate-side so the DP-0
    /// rule never re-selects a protected Digimon.</summary>
    public bool CanBeDestroyed()
    {
        ContinuousEvaluationResult result = ContinuousScopeEvaluation.EvaluateForCard(
            _context, ContinuousRestrictionGate.Scope, InstanceId);
        foreach (ReplacementEffect replacement in result.Replacements)
        {
            if (replacement.EventKind == ReplacementEventKind.Delete && replacement.ActionKind == ReplacementActionKind.Prevent)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>(batch-3) AS-IS <c>Permanent.CanSelectBySkill(ICardEffect skill)</c> (Permanent.cs:1648-1673):
    /// TRUE unless some usable <c>ICanNotSelectBySkillEffect</c>'s JOINT predicate matches (this candidate,
    /// the selecting skill's source). Delegates to the SAME verified true-scan
    /// <c>SelectPermanentEffect.IsUntargetableBySkill</c> uses internally
    /// (<see cref="Headless.Runtime.RestrictionScan"/> over <see cref="RestrictionHelpers.CannotBeSelectedBySkillKey"/>
    /// — the d-remediation joint-migration carrier of AS-IS <c>ICanNotSelectBySkillEffect</c>), fed the TRUE
    /// causing skill source (<c>skill.EffectSourceCard</c>) exactly as AS-IS threads <c>skill</c>. Added for the
    /// verbatim card idiom <c>permanent.CanSelectBySkill(activateClass)</c> inside select pre-check predicates
    /// (first caller BT2_097; AS-IS folds this into the same <c>CanSelectPermanentCondition</c> the
    /// SelectPermanentEffect ALSO applies internally — both sites now evaluate the same scan, matching AS-IS's
    /// double evaluation).</summary>
    public bool CanSelectBySkill(ICardEffect skill)
    {
        return !Headless.Runtime.RestrictionScan.IsRestricted(
            _context,
            RestrictionHelpers.CannotBeSelectedBySkillKey,
            InstanceId,
            skill?.EffectSourceCard?.InstanceId ?? default);
    }

    /// <summary>(MIG2) AS-IS <c>Permanent.RemoveLinkedCard(cardSource, removeCount, trashCard)</c>
    /// (Permanent.cs:1306-1348). A direct removal does NOT open the OnLinkCardDiscarded window — the batch
    /// window is <see cref="ITrashLinkCards"/>' job (CardController.cs:5314). With <paramref name="removeCount"/>
    /// &gt; 0 the OWNER SELECTS which link cards to trash (AS-IS SelectCardEffect, mode Discard, root Custom =
    /// LinkedCards, canEndNotMax:false): the substrate opens the card choice and parks (request-id prefix
    /// <see cref="AutoProcessing.LinkTrimRequestIdPrefix"/>); MetadataActionProcessor routes each pick through
    /// ITrashLinkCards — the AS-IS Mode.Discard linked-card branch (SelectCardEffect.cs:715-724).</summary>
    public async Task RemoveLinkedCard(CardSource? cardSource, int removeCount = 0, bool trashCard = true, CancellationToken cancellationToken = default)
    {
        if (cardSource is not null && LinkedCards.Any(linked => linked.InstanceId == cardSource.InstanceId))
        {
            await LinkHelpers.RemoveLinkCardAsync(
                _context.CardInstanceRepository, _context.ZoneMover, InstanceId, cardSource.InstanceId,
                trash: trashCard, gameEventQueue: null, cancellationToken).ConfigureAwait(false);
        }

        if (removeCount > 0)
        {
            List<CardSource> linked = LinkedCards;
            int maxCount = Math.Min(removeCount, linked.Count);
            if (maxCount <= 0)
            {
                return;
            }

            ChoiceCandidate[] candidates = linked
                .Select(card => EffectChoiceHelpers.Candidate(
                    card.InstanceId, card.InstanceId.Value, Headless.Choices.ChoiceZone.LinkedCards, isSelectable: true, OwnerId))
                .ToArray();
            ChoiceRequest request = EffectChoiceHelpers.CreateCardRequest(
                OwnerId,
                $"Select {maxCount} card to trash.",
                maxCount,
                maxCount,
                canSkip: false,
                Headless.Choices.ChoiceZone.LinkedCards,
                candidates);
            _context.ChoiceController.RequestChoice(
                request,
                new HeadlessEntityId($"{Assets.Scripts.Script.AutoProcessing.LinkTrimRequestIdPrefix}{InstanceId.Value}"));
        }
    }

    // ===== (MIG4 goal-4 slice 1) AS-IS Permanent instance-method surface — the AS-IS methods card ports call
    // (permanent.DiscardEvoRoots() / AddDigivolutionCardsTop() / AddLinkCard() …), each delegating to the
    // verified headless helper so a local-LLM card port is a mechanical mirror. No current caller — an additive
    // AS-IS surface; unsupported AS-IS branches throw with a design item rather than fabricate behavior.

    /// <summary>(MIG4) AS-IS <c>Permanent.DiscardEvoRoots(ignoreOverflow, putToTrash)</c> (Permanent.cs:106-142):
    /// trash this permanent's digivolution sources AND link cards, applying the ACE-Overflow penalty to both
    /// first (unless <paramref name="ignoreOverflow"/>). Delegates to
    /// <see cref="DeletionSourceTrash.TrashEvoSourcesAsync"/> (the putToTrash==true path every headless deletion
    /// call site uses, always gameEventQueue:null — AS-IS's own trash-add is direct, no OnDigivolutionCardDiscarded).
    /// The AS-IS <c>putToTrash == false</c> RETURN variant has no headless bare-detach primitive — design item
    /// MIG4-DISCARDEVOROOTS-PUTTOTRASH.</summary>
    public async Task DiscardEvoRoots(bool ignoreOverflow = false, bool putToTrash = true, CancellationToken cancellationToken = default)
    {
        if (!putToTrash)
        {
            throw new NotSupportedException(
                "Permanent.DiscardEvoRoots(putToTrash: false) has no headless primitive yet — design item MIG4-DISCARDEVOROOTS-PUTTOTRASH.");
        }

        await DeletionSourceTrash.TrashEvoSourcesAsync(
            _context.CardInstanceRepository,
            _context.ZoneMover,
            InstanceId,
            gameEventQueue: null,
            cancellationToken: cancellationToken,
            memory: _context.MemoryController,
            turnPlayer: _context.TurnController.Current.TurnPlayerId,
            ignoreOverflow: ignoreOverflow).ConfigureAwait(false);
    }

    /// <summary>(MIG4) AS-IS <c>Permanent.AddDigivolutionCardsTop(added, cardEffect)</c> (Permanent.cs:1064-1123):
    /// move each card off its current zone and insert it just under the top card. AS-IS per-card
    /// <c>cardSources.Insert(1, ...)</c> REVERSES the batch's relative order under the top (last processed ends
    /// up highest) — replicated by reversing before <see cref="DigivolutionStackHelpers.AddSourcesTopAsync"/>'s
    /// single ordered prepend. The <c>!this.IsToken &amp;&amp; !card.IsToken</c> guard (:1088) is preserved (a token
    /// host/card is still pulled off its zone but never attached). AS-IS fires ONE OnAddDigivolutionCards for the
    /// whole batch; a batch spanning &gt;1 live zone splits into one emit per zone group — design item
    /// MIG4-ADDDIGI-MULTIZONE-EMIT.</summary>
    public async Task AddDigivolutionCardsTop(
        IReadOnlyList<CardSource> addedDigivolutionCards,
        HeadlessEntityId? causeEffectSourceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addedDigivolutionCards);
        if (addedDigivolutionCards.Count == 0)
        {
            return;
        }

        bool hostIsToken = this.IsToken;
        var attachable = new List<HeadlessEntityId>();
        foreach (CardSource card in addedDigivolutionCards)
        {
            await DetachEmbeddedSourceOrLinkAsync(card, cancellationToken).ConfigureAwait(false);

            if (!hostIsToken && !card.IsToken)
            {
                attachable.Add(card.InstanceId);
            }
            else
            {
                await WithdrawToNoneAsync(card, cancellationToken).ConfigureAwait(false);
            }
        }

        attachable.Reverse();

        foreach (IGrouping<Headless.Choices.ChoiceZone, HeadlessEntityId> group in attachable.GroupBy(id => CurrentZoneOf(OwnerId, id)))
        {
            await DigivolutionStackHelpers.AddSourcesTopAsync(
                _context.CardInstanceRepository,
                _context.ZoneMover,
                InstanceId,
                group.ToArray(),
                group.Key,
                cancellationToken: cancellationToken,
                onceFlags: _context.OnceFlags,
                gameEventQueue: _context.GameEventQueue,
                causeSourceId: causeEffectSourceId ?? default).ConfigureAwait(false);
        }
    }

    /// <summary>(MIG4) AS-IS <c>Permanent.AddDigivolutionCardsBottom(added, cardEffect, skipEffectAndActivateSkill,
    /// isFacedown)</c> (Permanent.cs:1133-1227): move each card off its current zone and append it to the bottom
    /// of the stack (loop-order append, no reversal — unlike Top). Same token guard and multi-zone-emit design
    /// item as Top. AS-IS <c>isFacedown</c> (SetReverse the buried source) has no headless AddSources face write
    /// — design item MIG4-ADDDIGI-FACEDOWN.</summary>
    public async Task AddDigivolutionCardsBottom(
        IReadOnlyList<CardSource> addedDigivolutionCards,
        HeadlessEntityId? causeEffectSourceId,
        bool skipEffectAndActivateSkill = false,
        bool isFacedown = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addedDigivolutionCards);
        if (addedDigivolutionCards.Count == 0)
        {
            return;
        }

        if (isFacedown)
        {
            throw new NotSupportedException(
                "Permanent.AddDigivolutionCardsBottom(isFacedown: true) has no headless primitive yet — design item MIG4-ADDDIGI-FACEDOWN.");
        }

        bool hostIsToken = this.IsToken;
        var toAttach = new List<HeadlessEntityId>();
        foreach (CardSource card in addedDigivolutionCards)
        {
            await DetachEmbeddedSourceOrLinkAsync(card, cancellationToken).ConfigureAwait(false);

            if (!hostIsToken && !card.IsToken)
            {
                toAttach.Add(card.InstanceId);
            }
            else
            {
                await WithdrawToNoneAsync(card, cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (IGrouping<Headless.Choices.ChoiceZone, HeadlessEntityId> group in toAttach.GroupBy(id => CurrentZoneOf(OwnerId, id)))
        {
            await DigivolutionStackHelpers.AddSourcesBottomAsync(
                _context.CardInstanceRepository,
                _context.ZoneMover,
                InstanceId,
                group.ToArray(),
                group.Key,
                cancellationToken: cancellationToken,
                onceFlags: _context.OnceFlags,
                gameEventQueue: _context.GameEventQueue,
                causeSourceId: causeEffectSourceId ?? default,
                skipEffectAndActivateSkill: skipEffectAndActivateSkill).ConfigureAwait(false);
        }
    }

    /// <summary>(MIG4) AS-IS <c>Permanent.AddLinkCard(addedLinkCard, cardEffect)</c> (Permanent.cs:1237-1294):
    /// attach a link card to this permanent. Delegates to <see cref="LinkHelpers.AddLinkCardAsync"/> (excess-trim
    /// + attach + WhenLinked emit; its LinkedMax&gt;1 owner-selection is pre-existing design item MIG2-ADDLINK-SELECT).
    /// The <c>!this.IsToken &amp;&amp; !addedLinkCard.IsToken</c> guard (:1261) is preserved.</summary>
    public async Task AddLinkCard(
        CardSource addedLinkCard,
        HeadlessEntityId? causeEffectSourceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addedLinkCard);
        _ = causeEffectSourceId; // AS-IS AddLinkCard's cardEffect is unused past the (stripped) UI — kept for surface parity.

        await DetachEmbeddedSourceOrLinkAsync(addedLinkCard, cancellationToken).ConfigureAwait(false);

        if (this.IsToken || addedLinkCard.IsToken)
        {
            await WithdrawToNoneAsync(addedLinkCard, cancellationToken).ConfigureAwait(false);
            return;
        }

        Headless.Choices.ChoiceZone fromZone = CurrentZoneOf(addedLinkCard.Owner, addedLinkCard.InstanceId);
        await LinkHelpers.AddLinkCardAsync(
            _context.CardInstanceRepository,
            _context.ZoneMover,
            InstanceId,
            addedLinkCard.InstanceId,
            fromZone,
            gameEventQueue: _context.GameEventQueue,
            cancellationToken: cancellationToken,
            context: _context).ConfigureAwait(false);
    }

    /// <summary>(MIG4) AS-IS <c>Permanent.RemoveCardSource(cardSource)</c> (Permanent.cs:1297-1302): a bare
    /// removal from this permanent's stack list (AS-IS `cardSources.Remove(cardSource)`) — NO zone move, NO
    /// trash/trigger. Delegates to <see cref="DigivolutionStackHelpers.PlaySpecificSourceAsync"/> with
    /// <c>destination: ChoiceZone.None</c> (its documented detach-only mode: remove from sourceIds, skip the
    /// physical move). No-ops silently if the card is not one of this permanent's sources (List.Remove parity).</summary>
    public async Task RemoveCardSource(CardSource cardSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cardSource);
        await DigivolutionStackHelpers.PlaySpecificSourceAsync(
            _context.CardInstanceRepository,
            _context.ZoneMover,
            InstanceId,
            cardSource.InstanceId,
            Headless.Choices.ChoiceZone.None,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>(MIG4) AS-IS <c>CardObjectController.RemoveFromAllArea</c> (CardObjectController.cs:370-404) the
    /// add-methods' shared pre-step: scan every permanent, and if the card is embedded there detach it (a link
    /// via RemoveLinkedCard(trashCard:false), a buried source via the bare RemoveCardSource) BEFORE the physical
    /// zone move the helper performs. AS-IS also strips a permanent's OWN live top out of its stack (re-parent /
    /// demote) when the added card is currently some permanent's battling top — an identity-corrupting edge the
    /// headless model (permanent id == top identity) cannot express — design item MIG4-DETACH-LIVE-TOP (throws).</summary>
    private async Task DetachEmbeddedSourceOrLinkAsync(CardSource card, CancellationToken cancellationToken)
    {
        PermanentView host = card.PermanentOfThisCard();
        if (host.IsEmpty)
        {
            return;
        }

        if (host.TopInstanceId == card.InstanceId)
        {
            throw new NotSupportedException(
                $"'{card.InstanceId.Value}' is currently a permanent's own live top card — no headless primitive " +
                "re-parents/demotes it (AS-IS IPlacePermanentToDigivolutionCards / RemoveDigivolveRootEffect) — " +
                "design item MIG4-DETACH-LIVE-TOP.");
        }

        if (_context.CardInstanceRepository.TryGetInstance(host.TopInstanceId, out CardInstanceRecord? hostRecord) && hostRecord is not null
            && LinkHelpers.ReadLinkedCardIds(hostRecord.Metadata).Contains(card.InstanceId))
        {
            await LinkHelpers.RemoveLinkCardAsync(
                _context.CardInstanceRepository,
                _context.ZoneMover,
                host.TopInstanceId,
                card.InstanceId,
                trash: false,
                gameEventQueue: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        await DigivolutionStackHelpers.PlaySpecificSourceAsync(
            _context.CardInstanceRepository,
            _context.ZoneMover,
            host.TopInstanceId,
            card.InstanceId,
            Headless.Choices.ChoiceZone.None,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>AS-IS RemoveFromAllArea's unconditional physical-zone withdrawal: pull a card out of whatever
    /// concrete zone it currently sits in, even when the caller ultimately does not attach it (the token
    /// guards still ran the withdrawal before deciding not to attach).</summary>
    private async Task WithdrawToNoneAsync(CardSource card, CancellationToken cancellationToken)
    {
        Headless.Choices.ChoiceZone from = CurrentZoneOf(card.Owner, card.InstanceId);
        if (from != Headless.Choices.ChoiceZone.None)
        {
            await _context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(card.Owner, card.InstanceId, from, Headless.Choices.ChoiceZone.None),
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The concrete zone <paramref name="cardId"/> currently sits in for <paramref name="owner"/> (or
    /// <see cref="Headless.Choices.ChoiceZone.None"/> if none) — the live fromZone a card's unknown AS-IS origin
    /// needs.</summary>
    private Headless.Choices.ChoiceZone CurrentZoneOf(HeadlessPlayerId owner, HeadlessEntityId cardId)
    {
        var zones = (IZoneStateReader)_context.ZoneMover;
        foreach (KeyValuePair<Headless.Choices.ChoiceZone, IReadOnlyList<HeadlessEntityId>> pair in zones.Snapshot(owner))
        {
            if (pair.Value.Contains(cardId))
            {
                return pair.Key;
            }
        }

        return Headless.Choices.ChoiceZone.None;
    }
}

