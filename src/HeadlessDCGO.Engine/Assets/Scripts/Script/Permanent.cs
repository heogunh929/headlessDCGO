namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections; // (R1-c) Hashtable — AS-IS HasBlitz/HasEvade/HasBarrier/HasAlliance CanTrigger builders.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
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
    /// other gate is affected. (RD-JOGRESS-P2 #2 field-visibility asymmetry — the jogress temp-material reuses this
    /// snapshot to answer battle-area membership over a hand-resident card; docs/audit/rebuild_p6_cluster1_notes.md.)</summary>
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

    /// <summary>(RD-P6C2-10 / RD-P6C3-D1) AS-IS <c>Permanent.PermanentFrame</c> (Permanent.cs:27-43): the field
    /// frame this permanent occupies. AS-IS finds this permanent's slot via
    /// <c>Array.IndexOf(TopCard.Owner.FieldPermanents, this)</c> and returns <c>fieldCardFrames[index]</c>.
    /// The mirror has no fixed parallel slot arrays (no slot/capacity model — RD-P6C1-1/RD-P6C1-2), so the frame
    /// is materialised on demand: <c>FrameID</c> = this permanent's index in the owner's occupied field-permanent
    /// list (<c>GetFieldPermanents()</c> = battle ++ breeding) — the established field-list-index ADAPTATION —
    /// carrying the owner and this permanent as the occupant. Minimal surface consumed by [Arts Digivolve]
    /// (<see cref="CardSource.CanPlayCardTargetFrame"/>) and [App Fusion]
    /// (<c>CardSource.CanAppFusionFromTargetPermanent</c>).</summary>
    public FieldCardFrame? PermanentFrame
    {
        get
        {
            if (TopCard != null)
            {
                List<Permanent> fieldPermanents = new Player(_context, OwnerId).GetFieldPermanents();
                int index = fieldPermanents.FindIndex(permanent => permanent == this);

                if (0 <= index && index <= fieldPermanents.Count - 1)
                {
                    return new FieldCardFrame(_context, OwnerId, index, this);
                }
            }

            return null;
        }
    }

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

                                    if (!TopCard.CanNotBeAffected(cardEffect))
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
                    // (SEC-FaceUpSecuritySource) AS-IS `!cardSource.IsFlipped` face gate — the mirror tracks a
                    // security card's face state as the dedicated substrate flag (SecurityFaceState), disjoint from
                    // the field-ACE `isFlipped`, so ride the SAME face-state read FoldLinkedMax uses (NewModel
                    // ContinuousScan): a FACE-DOWN / default security card is not a continuous-effect source.
                    if (!Headless.Runtime.SecurityFaceState.IsFaceUpInSecurity(_context, cardSource.InstanceId))
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

                                    if (!TopCard.CanNotBeAffected(cardEffect))
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

                                if (!TopCard.CanNotBeAffected(cardEffect))
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
            // SUBSTRATE: the AS-IS DP fold evaluates each IChangeDPEffect's CanUse → CheckEffectDisabledClass,
            // which reads game state through the process-global GManager.instance; the mirror resolves that from
            // AmbientMatchContext, so scope the match for the whole fold (a caller already in the same scope
            // re-enters harmlessly) — same idiom as CardSource.GetPayingCostWithBaseCost / NewModelContinuousScan.
            using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(_context);

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

                                        if (!TopCard.CanNotBeAffected(cardEffect))
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
                        // (SEC-FaceUpSecuritySource) AS-IS `!cardSource.IsFlipped` face gate — ride the SecurityFaceState
                        // face-state read (as FoldLinkedMax does); a face-down/default security card is not a source.
                        if (!Headless.Runtime.SecurityFaceState.IsFaceUpInSecurity(_context, cardSource.InstanceId))
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

                                        if (!TopCard.CanNotBeAffected(cardEffect))
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

                                    if (!TopCard.CanNotBeAffected(cardEffect))
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

    #region Whether this permanent cannot return to hand
    /// <summary>(RD-EXT3-04) AS-IS <c>Permanent.CannotReturnToHand(ICardEffect cardEffect)</c>
    /// (Permanent.cs:744-781): the aggregate ICannotReturnToHand scan — TRUE if ANY live
    /// <see cref="ICannotReturnToHandEffect"/> (on any player's field permanents' [None] effects OR any player's
    /// own [None] effects) forbids THIS permanent from returning to hand for <paramref name="cardEffect"/>.
    /// Verbatim scan scope/order; substrate: <c>GManager.instance.turnStateMachine.gameContext.Players</c> →
    /// <c>new GameContext(_context).Players</c>.</summary>
    public bool CannotReturnToHand(ICardEffect cardEffect)
    {
        foreach (Player player in new GameContext(_context).Players)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect1 in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect1 is ICannotReturnToHandEffect)
                    {
                        if (cardEffect1.CanUse(null))
                        {
                            if (((ICannotReturnToHandEffect)cardEffect1).CannotReturnToHand(this, cardEffect))
                            {
                                return true;
                            }
                        }
                    }
                }

                foreach (ICardEffect cardEffect1 in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect1 is ICannotReturnToHandEffect)
                    {
                        if (cardEffect1.CanUse(null))
                        {
                            if (((ICannotReturnToHandEffect)cardEffect1).CannotReturnToHand(this, cardEffect))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }
    #endregion

    #region Can be returned to deck?
    /// <summary>AS-IS <c>Permanent.CannotReturnToLibrary(ICardEffect cardEffect)</c> (Permanent.cs:785-822): the
    /// aggregate ICannotReturnToLibrary scan — TRUE if ANY live <see cref="ICannotReturnToLibraryEffect"/> (on any
    /// player's field permanents' [None] effects OR any player's own [None] effects) forbids THIS permanent from
    /// returning to the deck for <paramref name="cardEffect"/>. Verbatim scan scope/order (structural sibling of
    /// <see cref="CannotReturnToHand"/>, same nested-loop quirk); substrate:
    /// <c>GManager.instance.turnStateMachine.gameContext.Players</c> → <c>new GameContext(_context).Players</c>.</summary>
    public bool CannotReturnToLibrary(ICardEffect cardEffect)
    {
        foreach (Player player in new GameContext(_context).Players)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect1 in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect1 is ICannotReturnToLibraryEffect)
                    {
                        if (cardEffect1.CanUse(null))
                        {
                            if (((ICannotReturnToLibraryEffect)cardEffect1).CannotReturnToLibrary(this, cardEffect))
                            {
                                return true;
                            }
                        }
                    }
                }

                foreach (ICardEffect cardEffect1 in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect1 is ICannotReturnToLibraryEffect)
                    {
                        if (cardEffect1.CanUse(null))
                        {
                            if (((ICannotReturnToLibraryEffect)cardEffect1).CannotReturnToLibrary(this, cardEffect))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }
    #endregion

    // ===== (R1-c) keyword-predicate getters (AS-IS Permanent.cs) ==============================================
    // Each getter is the AS-IS body verbatim (scan scope / interface / gate order / quirks preserved per getter —
    // NOT uniformised). Two established substrate adaptations apply throughout:
    //   * GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer → new GameContext(_context).Players_ForTurnPlayer
    //   * TopCard.CanNotBeAffected(cardEffect) → TopCard.CanNotBeAffected(cardEffect)
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

                    if (!TopCard.CanNotBeAffected(fakeCollisionClass))//Check can be affected by opponent's Digimon effects
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
    /// battle pipeline ALREADY stamps (<see cref="DeletedByBattleMetadataKey"/> — re-homed here from the retired
    /// substrate <c>BattleResolver.DeletedByBattleKey</c>, string value <c>"deletedByBattle"</c> unchanged, and
    /// already the class-local constant this file's top-swap marker strip uses) — so a gate reading this view
    /// sees exactly the live pipeline's answer, and the Hashtable builders' AS-IS-verbatim
    /// <c>{ IsDestroyedByBattle = true }</c> writes land on the same shared flag.</summary>
    public bool IsDestroyedByBattle
    {
        get => _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null
            && record.Metadata.TryGetValue(DeletedByBattleMetadataKey, out object? raw) && raw is true;
        set
        {
            if (_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null)
            {
                var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                {
                    [DeletedByBattleMetadataKey] = value,
                };
                _context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
            }
        }
    }

    // ===== (R3-A) DestroyPermanentsClass deletion-snapshot members (AS-IS Permanent.cs public fields the
    // Destroy() sequence marks/records). The five "JustBeforeRemoveField" carriers are backed by the
    // instance-metadata keys re-homed into this class (below), so the mirror DestroyPermanentsClass writer and
    // every OnDeletion reader agree on one carrier — no invention.
    // willBeRemoveField / DestroyingEffect use per-match stores (the PermanentJustBeforeRemoveFieldStore pattern). =====

    /// <summary>(R3-A) AS-IS <c>Permanent.willBeRemoveField</c> (a transient bool the Destroy() PRE cut-in reads to
    /// decide which marked permanents actually leave). Instance-metadata flag (the <c>IsBeingRevealed</c> pattern).</summary>
    public bool willBeRemoveField
    {
        get => _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null
            && record.Metadata.TryGetValue("willBeRemoveField", out object? raw) && raw is true;
        set
        {
            if (_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null)
            {
                var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                {
                    ["willBeRemoveField"] = value,
                };
                _context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
            }
        }
    }

    /// <summary>(R3-A) AS-IS <c>Permanent.DestroyingEffect</c> (the ICardEffect that deleted this permanent, read by
    /// OnDeletion responses). Per-match store keyed by InstanceId (the PermanentJustBeforeRemoveFieldStore pattern) —
    /// holds the live effect reference like the AS-IS auto-property.</summary>
    public ICardEffect? DestroyingEffect
    {
        get => _context.TryGetService(out DestroyingEffectStore? store) && store is not null
            && store.Values.TryGetValue(InstanceId, out ICardEffect? effect) ? effect : null;
        set
        {
            if (!_context.TryGetService(out DestroyingEffectStore? store) || store is null)
            {
                store = new DestroyingEffectStore();
                _context.RegisterService(store);
            }

            store.Values[InstanceId] = value;
        }
    }

    private sealed class DestroyingEffectStore
    {
        public Dictionary<HeadlessEntityId, ICardEffect?> Values { get; } = new Dictionary<HeadlessEntityId, ICardEffect?>();
    }

    /// <summary>AS-IS <c>Permanent.HandBounceEffect</c> (Permanent.cs:3678, default null) — the ICardEffect that
    /// returned this permanent to hand, recorded by <c>HandBounceClaass.Bounce</c> (:2783). Per-match store keyed
    /// by InstanceId (the <see cref="DestroyingEffect"/> pattern), holding the live effect reference like the AS-IS
    /// auto-property.</summary>
    public ICardEffect? HandBounceEffect
    {
        get => _context.TryGetService(out HandBounceEffectStore? store) && store is not null
            && store.Values.TryGetValue(InstanceId, out ICardEffect? effect) ? effect : null;
        set
        {
            if (!_context.TryGetService(out HandBounceEffectStore? store) || store is null)
            {
                store = new HandBounceEffectStore();
                _context.RegisterService(store);
            }

            store.Values[InstanceId] = value;
        }
    }

    private sealed class HandBounceEffectStore
    {
        public Dictionary<HeadlessEntityId, ICardEffect?> Values { get; } = new Dictionary<HeadlessEntityId, ICardEffect?>();
    }

    /// <summary>AS-IS <c>Permanent.LibraryBounceEffect</c> (Permanent.cs:3682, default null) — the ICardEffect that
    /// returned this permanent to the deck, recorded by <c>DeckBottomBounceClass.DeckBounce</c> (:2394) /
    /// <c>DeckTopBounceClass.DeckBounce</c> (:2560). Per-match store keyed by InstanceId (the
    /// <see cref="DestroyingEffect"/> pattern).</summary>
    public ICardEffect? LibraryBounceEffect
    {
        get => _context.TryGetService(out LibraryBounceEffectStore? store) && store is not null
            && store.Values.TryGetValue(InstanceId, out ICardEffect? effect) ? effect : null;
        set
        {
            if (!_context.TryGetService(out LibraryBounceEffectStore? store) || store is null)
            {
                store = new LibraryBounceEffectStore();
                _context.RegisterService(store);
            }

            store.Values[InstanceId] = value;
        }
    }

    private sealed class LibraryBounceEffectStore
    {
        public Dictionary<HeadlessEntityId, ICardEffect?> Values { get; } = new Dictionary<HeadlessEntityId, ICardEffect?>();
    }

    // --- (LEAVE-PLAY re-migration) The five "just before remove field" instance-metadata keys + their default
    // readers, re-homed here from the retired substrate `CardLeavePlayCleanup` into their AS-IS 원가 (the AS-IS
    // `Permanent` public fields below, DCGO/Assets/Scripts/Script/Permanent.cs:3702-3722). String values are
    // UNCHANGED, so already-stamped records keep matching. The retired file's OTHER members are RETIRED, not
    // re-homed: `RecordParametersJustBeforeRemoveField` duplicated the AS-IS record-parameters block that is now
    // LIVE as the mirror `DestroyPermanentsClass.Destroy` port (CardController.cs:5665-5690 / :6038-6063 —
    // it writes through the SETTERS below); `SnapshotPostReplacementKeywords` / `OnDeleted` had zero live
    // callers; `OnLeftPlay` was an empty no-op after the (③-B) registry-binding-drop retirement; and
    // `PermanentJustBeforeRemoveFieldKey` had zero readers (the live identity surface is the AS-IS
    // `CardSource.PermanentJustBeforeRemoveField` store, CardSource.cs:2289).
    /// <summary>Instance metadata: effective DP recorded just before the card left the field (-1 = none).</summary>
    internal const string DpJustBeforeRemoveFieldKey = "dpJustBeforeRemoveField";
    /// <summary>Instance metadata: permanent level just before leaving (only when the top card HAS a level).</summary>
    internal const string LevelJustBeforeRemoveFieldKey = "levelJustBeforeRemoveField";
    /// <summary>Instance metadata: printed play cost just before leaving (only when the top card HAS one).</summary>
    internal const string CostJustBeforeRemoveFieldKey = "costJustBeforeRemoveField";
    /// <summary>Instance metadata: the top card's live name list just before leaving.</summary>
    internal const string CardNamesJustBeforeRemoveFieldKey = "cardNamesJustBeforeRemoveField";
    /// <summary>Instance metadata: the top card's live trait list just before leaving.</summary>
    internal const string CardTraitsJustBeforeRemoveFieldKey = "cardTraitsJustBeforeRemoveField";

    /// <summary>AS-IS JustBeforeRemoveField int-field default (-1 when never recorded).</summary>
    private static int ReadJustBeforeInt(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is int value ? value : -1;

    /// <summary>AS-IS JustBeforeRemoveField list-field default (empty when never recorded).</summary>
    private static IReadOnlyList<string> ReadJustBeforeStrings(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is IEnumerable<string> values
            ? values.ToArray()
            : Array.Empty<string>();

    /// <summary>(R3-A) AS-IS <c>Permanent.DPJustBeforeRemoveField</c> (default -1). Backed by the shared
    /// snapshot key so OnDeletion DP-comparison predicates read the same value the writer stamped.</summary>
    public int DPJustBeforeRemoveField
    {
        get => _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null
            ? ReadJustBeforeInt(record.Metadata, DpJustBeforeRemoveFieldKey) : -1;
        set => WriteJustBeforeKey(DpJustBeforeRemoveFieldKey, value);
    }

    /// <summary>(R3-A) AS-IS <c>Permanent.LevelJustBeforeRemoveField</c> (default -1; consumers gate on &gt; 0).</summary>
    public int LevelJustBeforeRemoveField
    {
        get => _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null
            ? ReadJustBeforeInt(record.Metadata, LevelJustBeforeRemoveFieldKey) : -1;
        set => WriteJustBeforeKey(LevelJustBeforeRemoveFieldKey, value);
    }

    /// <summary>(R3-A) AS-IS <c>Permanent.CostJustBeforeRemoveField</c> (default -1).</summary>
    public int CostJustBeforeRemoveField
    {
        get => _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null
            ? ReadJustBeforeInt(record.Metadata, CostJustBeforeRemoveFieldKey) : -1;
        set => WriteJustBeforeKey(CostJustBeforeRemoveFieldKey, value);
    }

    /// <summary>(R3-A) AS-IS <c>Permanent.CardNamesJustBeforeRemoveField</c> (default empty).</summary>
    public List<string> CardNamesJustBeforeRemoveField
    {
        get => _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null
            ? ReadJustBeforeStrings(record.Metadata, CardNamesJustBeforeRemoveFieldKey).ToList() : new List<string>();
        set => WriteJustBeforeKey(CardNamesJustBeforeRemoveFieldKey, value?.ToArray() ?? Array.Empty<string>());
    }

    /// <summary>(R3-A) AS-IS <c>Permanent.CardTraitsJustBeforeRemoveField</c> (default empty).</summary>
    public List<string> CardTraitsJustBeforeRemoveField
    {
        get => _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null
            ? ReadJustBeforeStrings(record.Metadata, CardTraitsJustBeforeRemoveFieldKey).ToList() : new List<string>();
        set => WriteJustBeforeKey(CardTraitsJustBeforeRemoveFieldKey, value?.ToArray() ?? Array.Empty<string>());
    }

    private void WriteJustBeforeKey(string key, object? value)
    {
        if (_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null)
        {
            var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
            {
                [key] = value,
            };
            _context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
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

    /// <summary>1:1 mirror of AS-IS <c>Permanent.DigivolutionCards</c> (Permanent.cs:888):
    /// <c>cardSources.Filter(cs =&gt; cs != TopCard &amp;&amp; !LinkedCards.Contains(cs))</c>.
    /// ORDER (AS-IS): the AS-IS <c>cardSources</c> list holds the top card at index 0 and every new source is
    /// either <c>Insert(1, …)</c>-ed (AddDigivolutionCardsTop, Permanent.cs:1090) or <c>Add(…)</c>-ed
    /// (AddDigivolutionCardsBottom, Permanent.cs:1192), so the filtered list runs TOP→BOTTOM:
    /// index 0 = the source directly under the top card, the LAST entry = the DigiEgg.
    /// The substrate stack read (<see cref="DigivolutionStackReader"/>) yields the under-cards
    /// BOTTOM→TOP (<c>UnderCards[0]</c> = the DigiEgg), so it is walked in reverse here — the projection is
    /// the only place the substrate convention is translated, and every consumer sees the AS-IS order.</summary>
    public IReadOnlyList<CardSource> DigivolutionCards
    {
        get
        {
            DigivolutionStack stack = DigivolutionStackReader.Read(_context.CardInstanceRepository, _context.CardRepository, InstanceId);
            IReadOnlyList<StackedCard> under = stack.UnderCards;
            var topDown = new CardSource[under.Count];
            for (int i = 0; i < under.Count; i++)
            {
                topDown[i] = new CardSource(_context, under[under.Count - 1 - i].InstanceId, OwnerId);
            }

            return topDown;
        }
    }

    /// <summary>(coverage-exemplar BT25_096 one-surface read-side fold — permitted) 1:1 mirror of AS-IS
    /// <c>Permanent.HasFaceDownDigivolutionCards</c> (Permanent.cs:3954): <c>DigivolutionCards.Any(x =&gt; x.IsFlipped)</c>.
    /// Read-only, derived from the already-mirrored <see cref="DigivolutionCards"/> + <see cref="CardSource.IsFlipped"/>
    /// (the AS-IS face-down instance flag); inert to existing behaviour (no writer added). Used by the BT25_096 /
    /// BT25_041 / ST24_01 face-down-source Tamer families.</summary>
    public bool HasFaceDownDigivolutionCards => DigivolutionCards.Any(x => x.IsFlipped);

    /// <summary>AS-IS <c>Permanent.DigivolutionCardsColors</c> (Permanent.cs:3962-3990): the distinct colours
    /// carried by this permanent's non-flipped digivolution cards (each card's <c>CardColors</c> and
    /// <c>DualCardColors</c>). Substrate translation only: the mirror colour view is the string list, folded back
    /// to the AS-IS <c>CardColor</c> enum via the established <see cref="CardSource.ToCardColorList"/>
    /// reconciliation (CardSource.cs:188 — the mirror string values are exactly the enum names). Consumed by
    /// e.g. AD1_013's ESS DP fold (<c>.Count</c>).</summary>
    public List<CardColor> DigivolutionCardsColors
    {
        get
        {
            List<CardColor> cardColors = new List<CardColor>();

            foreach (CardSource cardSource in DigivolutionCards)
            {
                if (cardSource.IsFlipped)
                {
                    continue;
                }

                foreach (CardColor cardColor in CardSource.ToCardColorList(cardSource.CardColors))
                {
                    if (!cardColors.Contains(cardColor))
                    {
                        cardColors.Add(cardColor);
                    }
                }

                foreach (CardColor cardColor in CardSource.ToCardColorList(cardSource.DualCardColors))
                {
                    if (!cardColors.Contains(cardColor))
                    {
                        cardColors.Add(cardColor);
                    }
                }
            }

            return cardColors;
        }
    }

    /// <summary>(P6 stage A) AS-IS <c>Permanent.cardSources</c> (Permanent.cs:880) — ALL cards of this
    /// permanent: the top card, the digivolution sources AND the linked cards
    /// (AS-IS <c>DigivolutionCards = cardSources.Filter(c != TopCard &amp;&amp; !LinkedCards.Contains(c))</c>,
    /// Permanent.cs:888). Order mirrors the AS-IS list: top card at index 0 (AS-IS
    /// <c>AddDigivolutionCardsTop</c> inserts new sources at index 1 — directly under the top), then the
    /// digivolution sources TOP-MOST FIRST (exactly the order <see cref="DigivolutionCards"/> already
    /// reports), then the linked cards (AS-IS AddLinkCard appends). Consumed by the
    /// flip's per-card effect-membership scans (CEntity_EffectController.GetCardEffects,
    /// AS-IS CEntity_EffectController.cs:29-168) and gates like <c>cardSources.Contains(card)</c>.</summary>
    public List<CardSource> cardSources
    {
        get
        {
            var all = new List<CardSource> { TopCard };
            all.AddRange(DigivolutionCards);
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

    /// <summary>(W3 / P6A-PERMANENT-EFFECTLIST-ADDED resolved) AS-IS <c>Permanent.EffectList_Added(EffectTiming)</c>
    /// (Permanent.cs:1380-1492) VERBATIM — the effects GRANTED to this permanent, held in the per-duration buckets
    /// (<see cref="UntilOwnerDrawPhaseEffects"/> … <see cref="UntilEndAttackEffects"/>) fed by
    /// <see cref="CardEffectCommons.AddEffectToPermanent"/>. Each bucket holds a <c>Func&lt;EffectTiming,ICardEffect&gt;</c>
    /// (the <see cref="CardEffectCommons.GetCardEffectByEffectTiming"/> selector) that yields its effect ONLY when
    /// re-queried at the grant timing (else null), so a bucket surfaces its effect only at that timing. The buckets
    /// are enumerated in the AS-IS order, each non-null result added, then null-filtered, then each surviving effect's
    /// null <c>EffectSourceCard</c> is back-filled with <see cref="TopCard"/> and <c>SetIsInheritedEffect(false)</c> is
    /// stamped. The buckets are backed by a match-scoped <see cref="PermanentEffectListStore"/> (the Permanent is a
    /// per-access VIEW; see the bucket properties). Consumed via <see cref="EffectList_ForCard"/> →
    /// <see cref="EffectList(EffectTiming)"/> by the live continuous scans (which query timing None / continuous
    /// timings — an activated grant selected at e.g. OnEndBattle yields null there and is not surfaced) and by the
    /// mirror <c>AutoProcessing.GetSkillInfos</c> window collection (the intended consumer).</summary>
    public List<ICardEffect> EffectList_Added(EffectTiming timing)
    {
        List<ICardEffect> _EffectList = new List<ICardEffect>();

        if (TopCard != null)
        {
            foreach (Func<EffectTiming, ICardEffect> GetCardEffect in UntilOwnerDrawPhaseEffects)
            {
                ICardEffect cardEffect = GetCardEffect(timing);

                if (cardEffect != null)
                {
                    _EffectList.Add(cardEffect);
                }
            }

            foreach (Func<EffectTiming, ICardEffect> GetCardEffect in UntilOwnerTurnEndEffects)
            {
                ICardEffect cardEffect = GetCardEffect(timing);

                if (cardEffect != null)
                {
                    _EffectList.Add(cardEffect);
                }
            }

            foreach (Func<EffectTiming, ICardEffect> GetCardEffect in UntilEachTurnEndEffects)
            {
                ICardEffect cardEffect = GetCardEffect(timing);

                if (cardEffect != null)
                {
                    _EffectList.Add(cardEffect);
                }
            }

            foreach (Func<EffectTiming, ICardEffect> GetCardEffect in UntilOpponentTurnEndEffects)
            {
                ICardEffect cardEffect = GetCardEffect(timing);

                if (cardEffect != null)
                {
                    _EffectList.Add(cardEffect);
                }
            }

            foreach (Func<EffectTiming, ICardEffect> GetCardEffect in UntilEndBattleEffects)
            {
                ICardEffect cardEffect = GetCardEffect(timing);

                if (cardEffect != null)
                {
                    _EffectList.Add(cardEffect);
                }
            }

            foreach (Func<EffectTiming, ICardEffect> GetCardEffect in UntilOwnerTurnStartEffects)
            {
                ICardEffect cardEffect = GetCardEffect(timing);

                if (cardEffect != null)
                {
                    _EffectList.Add(cardEffect);
                }
            }

            foreach (Func<EffectTiming, ICardEffect> GetCardEffect in UntilNextUntapEffects)
            {
                ICardEffect cardEffect = GetCardEffect(timing);

                if (cardEffect != null)
                {
                    _EffectList.Add(cardEffect);
                }
            }

            foreach (Func<EffectTiming, ICardEffect> GetCardEffect in PermanentEffects)
            {
                ICardEffect cardEffect = GetCardEffect(timing);

                if (cardEffect != null)
                {
                    _EffectList.Add(cardEffect);
                }
            }

            foreach (Func<EffectTiming, ICardEffect> GetCardEffect in UntilEndAttackEffects)
            {
                ICardEffect cardEffect = GetCardEffect(timing);

                if (cardEffect != null)
                {
                    _EffectList.Add(cardEffect);
                }
            }

            _EffectList = _EffectList.Filter(cardEffect => cardEffect != null);

            foreach (ICardEffect cardEffect in _EffectList)
            {
                if (cardEffect != null)
                {
                    if (cardEffect.EffectSourceCard == null)
                    {
                        cardEffect.SetEffectSourceCard(TopCard);
                    }

                    cardEffect.SetIsInheritedEffect(false);
                }
            }
        }

        return _EffectList;
    }

    // ===== (W3 / P6A-PERMANENT-EFFECTLIST-ADDED) AS-IS Permanent duration buckets (Permanent.cs:1575-1608) — the
    // per-permanent granted-effect store fed by CardEffectCommons.AddEffectToPermanent and enumerated by
    // EffectList_Added above. AS-IS these are plain public settable list FIELDS on the persistent Permanent; the
    // mirror Permanent is a transient per-access VIEW (a fresh instance on every access, keyed on the top-card
    // InstanceId — see the Equals note), so a per-instance field would not survive across two views of the same
    // permanent. Backed by a match-scoped store keyed by (EngineContext, top-card InstanceId) — the same substrate
    // pattern as CEntity_EffectControllerStore / PlayerEffectListStore. The get returns the LIVE list (so `.Add(...)`
    // appends AS-IS-verbatim); the set REPLACES it (the AS-IS turn-end / attack-end cleanups reassign a fresh
    // `new List<…>()`). ADAPTATION: substrate backing only — names, element type, and merge order are AS-IS 1:1.
    // NOTE: keyed on the top-card InstanceId, so a permanent whose top changes (de-digivolve) sees a fresh (empty)
    // holder — the same across-time identity boundary documented on Equals; AS-IS grants outlive a top change on the
    // persistent object. No current caller relies on that edge (only same-turn OnEndBattle grants exist today).

    /// <summary>AS-IS <c>Permanent.UntilOwnerTurnEndEffects</c> (Permanent.cs:1575).</summary>
    public List<Func<EffectTiming, ICardEffect>> UntilOwnerTurnEndEffects
    {
        get => PermanentEffectListStore.Get(_context, InstanceId).UntilOwnerTurnEndEffects;
        set => PermanentEffectListStore.Get(_context, InstanceId).UntilOwnerTurnEndEffects = value;
    }

    /// <summary>AS-IS <c>Permanent.UntilOwnerDrawPhaseEffects</c> (Permanent.cs:1579).</summary>
    public List<Func<EffectTiming, ICardEffect>> UntilOwnerDrawPhaseEffects
    {
        get => PermanentEffectListStore.Get(_context, InstanceId).UntilOwnerDrawPhaseEffects;
        set => PermanentEffectListStore.Get(_context, InstanceId).UntilOwnerDrawPhaseEffects = value;
    }

    /// <summary>AS-IS <c>Permanent.UntilEachTurnEndEffects</c> (Permanent.cs:1583).</summary>
    public List<Func<EffectTiming, ICardEffect>> UntilEachTurnEndEffects
    {
        get => PermanentEffectListStore.Get(_context, InstanceId).UntilEachTurnEndEffects;
        set => PermanentEffectListStore.Get(_context, InstanceId).UntilEachTurnEndEffects = value;
    }

    /// <summary>AS-IS <c>Permanent.UntilOpponentTurnEndEffects</c> (Permanent.cs:1587).</summary>
    public List<Func<EffectTiming, ICardEffect>> UntilOpponentTurnEndEffects
    {
        get => PermanentEffectListStore.Get(_context, InstanceId).UntilOpponentTurnEndEffects;
        set => PermanentEffectListStore.Get(_context, InstanceId).UntilOpponentTurnEndEffects = value;
    }

    /// <summary>AS-IS <c>Permanent.UntilEndBattleEffects</c> (Permanent.cs:1591).</summary>
    public List<Func<EffectTiming, ICardEffect>> UntilEndBattleEffects
    {
        get => PermanentEffectListStore.Get(_context, InstanceId).UntilEndBattleEffects;
        set => PermanentEffectListStore.Get(_context, InstanceId).UntilEndBattleEffects = value;
    }

    /// <summary>AS-IS <c>Permanent.UntilEndAttackEffects</c> (Permanent.cs:1595).</summary>
    public List<Func<EffectTiming, ICardEffect>> UntilEndAttackEffects
    {
        get => PermanentEffectListStore.Get(_context, InstanceId).UntilEndAttackEffects;
        set => PermanentEffectListStore.Get(_context, InstanceId).UntilEndAttackEffects = value;
    }

    /// <summary>AS-IS <c>Permanent.UntilOwnerTurnStartEffects</c> (Permanent.cs:1599).</summary>
    public List<Func<EffectTiming, ICardEffect>> UntilOwnerTurnStartEffects
    {
        get => PermanentEffectListStore.Get(_context, InstanceId).UntilOwnerTurnStartEffects;
        set => PermanentEffectListStore.Get(_context, InstanceId).UntilOwnerTurnStartEffects = value;
    }

    /// <summary>AS-IS <c>Permanent.UntilNextUntapEffects</c> (Permanent.cs:1603).</summary>
    public List<Func<EffectTiming, ICardEffect>> UntilNextUntapEffects
    {
        get => PermanentEffectListStore.Get(_context, InstanceId).UntilNextUntapEffects;
        set => PermanentEffectListStore.Get(_context, InstanceId).UntilNextUntapEffects = value;
    }

    /// <summary>AS-IS <c>Permanent.PermanentEffects</c> (Permanent.cs:1607).</summary>
    public List<Func<EffectTiming, ICardEffect>> PermanentEffects
    {
        get => PermanentEffectListStore.Get(_context, InstanceId).PermanentEffects;
        set => PermanentEffectListStore.Get(_context, InstanceId).PermanentEffects = value;
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

                                        if (!TopCard.CanNotBeAffected(cardEffect))
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

                                    if (!TopCard.CanNotBeAffected(cardEffect))
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
    /// (<see cref="LinkedDpKey"/>) that the link attach/detach helpers below already write, so existing link
    /// records are visible and an AS-IS <c>LinkedDP +=/-=</c> lands on the shared value.</summary>
    public int LinkedDP
    {
        get =>
            _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
                ? ReadLinkedDp(i.Metadata)
                : 0;
        set
        {
            if (_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null)
            {
                var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                {
                    [LinkedDpKey] = value,
                };
                _context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
            }
        }
    }

    /// <summary>(R1-a) AS-IS <c>Permanent.Boosts</c> (Permanent.cs:672, a <c>List&lt;DPBoost&gt;</c> field) folded
    /// into <see cref="DP"/> at the very end (<c>foreach (DPBoost boost in Boosts) DP += boost.DP</c>). ADAPTATION:
    /// the AS-IS in-memory list maps to a read view over the id→dp instance metadata
    /// (<see cref="DpBoostsKey"/>) that <see cref="AddBoost"/> / <see cref="RemoveBoost"/> manage;
    /// the fold only reads <c>boost.DP</c>, so the reconstructed <see cref="DPBoost"/> carries a null Condition.</summary>
    public List<DPBoost> Boosts
    {
        get
        {
            var boosts = new List<DPBoost>();
            if (_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
                && i.Metadata.TryGetValue(DpBoostsKey, out object? raw)
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

    /// <summary>Instance-metadata key holding the id→dp boost map (Dictionary&lt;string,int&gt;) that backs the
    /// AS-IS <c>List&lt;DPBoost&gt; Boosts</c> field. Re-homed here from the retired substrate DpBoostHelpers —
    /// string value unchanged (<c>"dpBoosts"</c>).</summary>
    public const string DpBoostsKey = "dpBoosts";

    /// <summary>(R1-a) AS-IS <c>Permanent.AddBoost(DPBoost)</c> (Permanent.cs:674-681): upsert by ID — if a boost
    /// with the same ID is already present its DP is REPLACED, otherwise the boost is appended. ADAPTATION: the
    /// AS-IS list mutation writes the id→dp map on the instance metadata that <see cref="Boosts"/> reads back
    /// (the AS-IS DP fold consults only <c>boost.DP</c>, never <c>Condition</c>, so the Condition is not stored).</summary>
    public void AddBoost(DPBoost boost)
    {
        ArgumentNullException.ThrowIfNull(boost);
        ArgumentException.ThrowIfNullOrWhiteSpace(boost.ID);
        if (!_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        Dictionary<string, int> boosts = ReadBoosts(record.Metadata);
        boosts[boost.ID] = boost.DP;
        WriteBoosts(record, boosts);
    }

    /// <summary>(R1-a) AS-IS <c>Permanent.RemoveBoost(string ID)</c> (Permanent.cs:682-686): remove the boost with
    /// that ID (no-op when absent).</summary>
    public void RemoveBoost(string ID)
    {
        if (string.IsNullOrWhiteSpace(ID)
            || !_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        Dictionary<string, int> boosts = ReadBoosts(record.Metadata);
        if (boosts.Remove(ID))
        {
            WriteBoosts(record, boosts);
        }
    }

    private static Dictionary<string, int> ReadBoosts(IReadOnlyDictionary<string, object?> metadata) =>
        metadata.TryGetValue(DpBoostsKey, out object? raw) && raw is IReadOnlyDictionary<string, int> existing
            ? new Dictionary<string, int>(existing, StringComparer.Ordinal)
            : new Dictionary<string, int>(StringComparer.Ordinal);

    private void WriteBoosts(CardInstanceRecord record, Dictionary<string, int> boosts)
    {
        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
        if (boosts.Count == 0)
        {
            metadata.Remove(DpBoostsKey);
        }
        else
        {
            metadata[DpBoostsKey] = boosts;
        }

        _context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
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
    /// <c>TopCard.CanNotBeAffected(cardEffect)</c>; <c>Mathf.Clamp</c> →
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
                                if (!TopCard.CanNotBeAffected(cardEffect))
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
                            if (!TopCard.CanNotBeAffected(cardEffect))
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
                                if (!TopCard.CanNotBeAffected(cardEffect))
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
                            if (!TopCard.CanNotBeAffected(cardEffect))
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
            // (RD-R4P-STRIKESEED substrate adaptation) AS-IS seeds a literal `int Strike = 1` — a real Digimon has
            // no printed strike, so the check-count is always the constant 1 folded with live IChangeSAttackEffect.
            // The mirror substrate stamps an attacker's resolved check-count as the "strike" instance metadata
            // (SecurityResolver.StrikeKey) — e.g. a Piercing/multi-strike attacker whose extra checks were resolved
            // upstream. Seed the fold from that substrate base (default 1, the AS-IS constant) exactly as the mirror
            // LinkMax/DP readers fold live effects onto a substrate-resolved base. Production never writes the key, so
            // a real game seeds the AS-IS 1 — behaviour-identical (shadow IDENTICAL).
            int Strike = ReadSubstrateStrikeSeed();

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
                                    if (!TopCard.CanNotBeAffected(cardEffect))
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
                                if (!TopCard.CanNotBeAffected(cardEffect))
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

    /// <summary>(RD-R4P-STRIKESEED) The substrate base for <see cref="Strike_AllowMinus"/>: the attacker's resolved
    /// security-check count stamped as the <c>"strike"</c> instance metadata (SecurityResolver.StrikeKey), clamped at
    /// 0. Defaults to the AS-IS literal-1 seed when absent (every production attacker, which never carries the key).</summary>
    private int ReadSubstrateStrikeSeed()
    {
        if (_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record)
            && record is not null
            && record.Metadata.TryGetValue("strike", out object? raw)
            && raw is not null)
        {
            switch (raw)
            {
                case int i:
                    return Math.Max(0, i);
                case long l when l >= int.MinValue && l <= int.MaxValue:
                    return Math.Max(0, (int)l);
                case double d when d >= int.MinValue && d <= int.MaxValue && d % 1 == 0:
                    return Math.Max(0, (int)d);
                case string s when int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed):
                    return Math.Max(0, parsed);
            }
        }

        return 1;
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

            return ReadLinkedCardIds(host.Metadata)
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
    /// <c>IChangeLinkMaxEffect</c>s (the new-model FoldLinkedMax scan).
    /// (RD-P6B-16 RETIRED — C5-1, 2026-07-24) The legacy <c>linkedMaxDelta</c> pre-fold the retired substrate
    /// helper ran first was an AS-IS-ABSENT union scaffold with ZERO producers (BIT-IDENTICAL removal), so the
    /// sole path is the AS-IS one: base <see cref="ReadLinkedMax"/> folded by
    /// <see cref="NewModelContinuousScan.FoldLinkedMax"/>.</summary>
    public int LinkedMax
    {
        get
        {
            int baseMax = _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? host) && host is not null
                ? ReadLinkedMax(host.Metadata)
                : DefaultLinkedMax;
            return NewModelContinuousScan.FoldLinkedMax(_context, InstanceId, baseMax);
        }
    }

    /// <summary>(MIG2) AS-IS <c>Permanent.HasNoLinkCards</c> (Permanent.cs:3958).</summary>
    public bool HasNoLinkCards => LinkedCards.Count == 0;

    /// <summary>(MIG2) AS-IS <c>Permanent.IsPlaceToTrashDueToNotHavingDP</c> (Permanent.cs:3694, default true;
    /// effects may clear the flag). (BT7_087) WRITE side: the AS-IS mutable field is stamped by the
    /// treat-a-Tamer-as-a-Digimon-and-digivolve block (BT7_087 :235 clears to false around
    /// <c>DigivolveIntoHandOrTrashCard</c>, :259 restores true). The setter stamps the metadata key the getter
    /// reads (default-true is the ABSENT/true state, false is the explicit opt-out) via the established
    /// per-flag CardInstanceRecord.Metadata write idiom (Permanent.cs WriteJustBeforeKey / CardEffectCommons
    /// IsPlayedOptionPermanentKey :421).</summary>
    public bool IsPlaceToTrashDueToNotHavingDP
    {
        get => !(_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
            && i.Metadata.TryGetValue(PlaceToTrashDueToNoDpKey, out object? optOut) && optOut is false);
        set
        {
            if (_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null)
            {
                var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                {
                    [PlaceToTrashDueToNoDpKey] = value,
                };
                _context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
            }
        }
    }

    /// <summary>(MIG2) AS-IS <c>Permanent.IsPlayedOptionPermanent</c> (Permanent.cs:3946, default false — an
    /// Option a card effect legitimately keeps on the battle area).</summary>
    public bool IsPlayedOptionPermanent =>
        _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? p) && p is not null
            && p.Metadata.TryGetValue(IsPlayedOptionPermanentKey, out object? played) && played is true;

    /// <summary>(R1-d) AS-IS <c>Permanent.CanBeDestroyed()</c> (Permanent.cs:3186-3229): TRUE unless some usable
    /// <c>ICanNotBeDestroyedEffect</c> (scanned over every turn-ordered player's field permanents and the player
    /// itself, <c>CanUse(null)</c>-gated) protects THIS permanent. Established substrate ADAPTATION:
    /// <c>gameContext.Players_ForTurnPlayer</c> → <c>new GameContext(_context).Players_ForTurnPlayer</c>.</summary>
    public bool CanBeDestroyed()
    {
        #region 消滅しない効果
        foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                #region 場のパーマネントの効果
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanNotBeDestroyedEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            if (((ICanNotBeDestroyedEffect)cardEffect).CanNotBeDestroyed(this))
                            {
                                return false;
                            }
                        }
                    }
                }
                #endregion
            }

            #region プレイヤーの効果
            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICanNotBeDestroyedEffect)
                {
                    if (cardEffect.CanUse(null))
                    {
                        if (((ICanNotBeDestroyedEffect)cardEffect).CanNotBeDestroyed(this))
                        {
                            return false;
                        }
                    }
                }
            }
            #endregion
        }
        #endregion

        return true;
    }

    /// <summary>(R1-d) AS-IS <c>Permanent.CanSelectBySkill(ICardEffect skill)</c> (Permanent.cs:1648-1673):
    /// TRUE unless some usable <c>ICanNotSelectBySkillEffect</c>'s JOINT predicate <c>CanNotSelectBySkill(this,
    /// skill)</c> matches, scanned over every player's field permanents' <c>EffectList(None)</c>. AS-IS iterates
    /// <c>gameContext.Players</c> (FULL roster, seat order) — an order-insensitive any-match. ADAPTATION:
    /// <c>gameContext.Players</c> → <c>new GameContext(_context).Players</c>.</summary>
    public bool CanSelectBySkill(ICardEffect skill)
    {
        foreach (Player player in new GameContext(_context).Players)
        {
            #region Effects of field permanents
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanNotSelectBySkillEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            if (((ICanNotSelectBySkillEffect)cardEffect).CanNotSelectBySkill(this, skill))
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            #endregion
        }

        return true;
    }

    #region Immune From De-Digivolve
    /// <summary>(R3-W3c-4c B5) AS-IS <c>Permanent.ImmuneFromDeDigivolve()</c> (Permanent.cs:826-847): TRUE when
    /// some usable <c>IImmuneFromDeDigivolveEffect</c> on any player's field permanents' <c>EffectList(None)</c>
    /// declares THIS permanent immune (<c>ImmuneDeDigivolve(this)</c>). AS-IS iterates <c>gameContext.Players</c>
    /// (FULL roster; order-insensitive any-match) — ADAPTATION <c>new GameContext(_context).Players</c>. The
    /// continuous de-digivolve immunity is subject-only (no causing effect), matching the AS-IS signature.</summary>
    public bool ImmuneFromDeDigivolve()
    {
        foreach (Player player in new GameContext(_context).Players)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect1 in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect1 is IImmuneFromDeDigivolveEffect)
                    {
                        if (cardEffect1.CanUse(null))
                        {
                            if (((IImmuneFromDeDigivolveEffect)cardEffect1).ImmuneDeDigivolve(this))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }
    #endregion

    #region Immune From Trashing Stack
    /// <summary>(R3-W3c B6) AS-IS <c>Permanent.ImmuneFromStackTrashing(ICardEffect)</c> (Permanent.cs:853-876):
    /// TRUE when some usable <c>IImmuneFromStackTrashingEffect</c> on any player's field permanents'
    /// <c>EffectList(None)</c> declares THIS permanent immune from digivolution-stack trashing by the causing
    /// <paramref name="effect"/> (<c>ImmuneStackTrashing(this, effect)</c>). AS-IS iterates
    /// <c>gameContext.Players</c> (FULL roster; order-insensitive any-match) — ADAPTATION
    /// <c>new GameContext(_context).Players</c>. The causing effect is threaded to the kind-class
    /// <c>EffectCondition</c> (e.g. <c>IsOpponentEffect</c>), matching the AS-IS signature.</summary>
    public bool ImmuneFromStackTrashing(ICardEffect effect)
    {
        foreach (Player player in new GameContext(_context).Players)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect1 in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect1 is IImmuneFromStackTrashingEffect)
                    {
                        if (cardEffect1.CanUse(null))
                        {
                            if (((IImmuneFromStackTrashingEffect)cardEffect1).ImmuneStackTrashing(this, effect))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }
    #endregion

    // ===== (R1-d) restriction / immunity predicate getters (AS-IS Permanent.cs) ================================
    // Each member is the AS-IS body verbatim (scan scope / interface / gate order / quirk preserved per member —
    // NOT uniformised). Established substrate ADAPTATIONs applied throughout (same as the R1-b/R1-c clusters):
    //   * GManager.instance.turnStateMachine.gameContext.{Players,Players_ForTurnPlayer,TurnPlayer}
    //       → new GameContext(_context).{Players,Players_ForTurnPlayer,TurnPlayer}
    //   * GManager.instance.attackProcess → HeadlessDCGO.Engine.Assets.Scripts.Script.AttackProcess.For(_context)
    //   * TopCard.CanNotBeAffected(cardEffect) → TopCard.CanNotBeAffected(cardEffect)
    //   * TopCard.Owner is a HeadlessPlayerId here (AS-IS: a Player) → new Player(_context, TopCard.Owner) for the
    //     Player surface (Enemy / Get{Battle,Breeding}AreaPermanents); Player comparisons compare .PlayerId.
    //   * Frame model absent (design item RD-P6C1-1 / MIG5-FRAME-MODEL): AS-IS `PermanentFrame.IsBattleAreaFrame()`
    //     → CardEffectCommons.IsPermanentExistsOnBattleArea(this) (zone membership); AS-IS `isBreedingAreaFrame()`
    //     → GetBreedingAreaPermanents() membership; the `fieldCardFrames` empty-battle-slot capacity check has no
    //     mirror (design item RD-P6C1-2 / RD-JOGRESS-P2 #4 full-field capacity corner — the established
    //     no-capacity convention (BT1_089) omits it; docs/audit/rebuild_p6_cluster1_notes.md).
    //   * AS-IS `EnterFieldTurnCount == TurnCount` (summoning sickness) → the established `enteredThisTurn` boolean
    //     substrate flag (MatchStateMutationSink.EnteredThisTurnKey), read via EnteredThisTurn below.

    #region 宣言可能な起動型効果があるか
    /// <summary>(R1-d) AS-IS <c>Permanent.CanDeclareSkill()</c> (Permanent.cs:1613).</summary>
    public bool CanDeclareSkill() => CanDeclareSkillList().Count > 0;
    #endregion

    #region 宣言可能な起動型効果リスト
    /// <summary>(R1-d) AS-IS <c>Permanent.CanDeclareSkillList()</c> (Permanent.cs:1617-1636): the usable
    /// <c>OnDeclaration</c> activated effects of this permanent's top card.</summary>
    public List<ICardEffect> CanDeclareSkillList()
    {
        List<ICardEffect> CanDeclareSkillList = new List<ICardEffect>();

        if (TopCard != null)
        {
            foreach (ICardEffect _cardEffect in EffectList(EffectTiming.OnDeclaration))
            {
                if (_cardEffect is ActivateICardEffect)
                {
                    if (_cardEffect.CanUse(null))
                    {
                        CanDeclareSkillList.Add(_cardEffect);
                    }
                }
            }
        }

        return CanDeclareSkillList;
    }
    #endregion

    /// <summary>(R1-d) The mirror carrier of AS-IS <c>Permanent.EnterFieldTurnCount == TurnCount</c> (summoning
    /// sickness): the established <c>enteredThisTurn</c> boolean metadata flag stamped by the play/move/fusion
    /// paths (<see cref="Headless.Services.ZoneMoveMetadataKeys.EnteredThisTurnKey"/>) and read verbatim by the
    /// existing attack validator. TRUE == entered the field this turn (AS-IS EnterFieldTurnCount == TurnCount).</summary>
    private bool EnteredThisTurn =>
        _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? e) && e is not null
        && e.Metadata.TryGetValue(Headless.Services.ZoneMoveMetadataKeys.EnteredThisTurnKey, out object? raw) && raw is true;

    /// <summary>(R4 S3b-2①) AS-IS <c>Permanent.EnterFieldTurnCount</c> (a plain mutable int the executor writes:
    /// PlayPermanentClass :1387 <c>= TurnCount</c> on play, :1500 <c>= -1</c> on jogress) — the mirror WRITE
    /// surface over the established <c>enteredThisTurn</c> boolean carrier: <c>value == current turn</c> ⇒ true
    /// (summoning-sick), anything else (the jogress −1) ⇒ false. The getter re-derives the AS-IS comparison shape
    /// (this-turn ⇒ the current turn number, else −1 — sufficient for every AS-IS <c>== TurnCount</c> read).</summary>
    public int EnterFieldTurnCount
    {
        get => EnteredThisTurn ? _context.TurnController.Current.TurnNumber : -1;
        set
        {
            if (_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null)
            {
                var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                {
                    [Headless.Services.ZoneMoveMetadataKeys.EnteredThisTurnKey] = value == _context.TurnController.Current.TurnNumber,
                };
                _context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
            }
        }
    }

    #region Whether this permanent can unsuspend
    /// <summary>(R1-d) AS-IS <c>Permanent.CanUnsuspend</c> (Permanent.cs:1962-2006): TRUE unless a usable
    /// <c>ICanNotUnsuspendEffect</c> (over every player's field permanents and the player) forbids it.</summary>
    public bool CanUnsuspend
    {
        get
        {
            foreach (Player player in new GameContext(_context).Players)
            {
                #region Effects of field permanents
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is ICanNotUnsuspendEffect)
                        {
                            if (cardEffect.CanUse(null))
                            {
                                if (((ICanNotUnsuspendEffect)cardEffect).CanNotUnsuspend(this))
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
                #endregion

                #region Effects of players
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanNotUnsuspendEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            if (((ICanNotUnsuspendEffect)cardEffect).CanNotUnsuspend(this))
                            {
                                return false;
                            }
                        }
                    }
                }
                #endregion
            }

            return true;
        }
    }
    #endregion

    #region このパーマネントが移動できるかどうか
    /// <summary>(R1-d) AS-IS <c>Permanent.CanMove</c> (Permanent.cs:2010-2086). Frame ADAPTATIONs per the section
    /// header (breeding-frame → GetBreedingAreaPermanents membership; empty-battle-slot capacity check omitted,
    /// RD-P6C1-2).</summary>
    public bool CanMove
    {
        get
        {
            if (TopCard != null)
            {
                #region the effects of permanents
                if (new GameContext(_context).Players
                    .Map(player => player.GetFieldPermanents())
                    .Flat()
                    .Map(permanent => permanent.EffectList(EffectTiming.None))
                    .Flat()
                    .Some(cardEffect => cardEffect is ICanNotMoveEffect
                        && cardEffect.CanUse(null)
                        && ((ICanNotMoveEffect)cardEffect).CanNotMove(TopCard, null)))
                {
                    return false;
                }
                #endregion

                #region the effects of players
                if (new GameContext(_context).Players
                        .Map(player => player.EffectList(EffectTiming.None))
                        .Flat()
                        .Some(cardEffect => cardEffect is ICanNotMoveEffect
                            && cardEffect.CanUse(null)
                            && ((ICanNotMoveEffect)cardEffect).CanNotMove(TopCard, null)))
                {
                    return false;
                }
                #endregion

                #region the effects of itself
                // (AS-IS-dead) `this == null` is Unity's destroyed-object check; a plain mirror instance is never
                // null, so this branch is inert — preserved verbatim for structural fidelity.
                if (this == null)
                {
                    if (EffectList(EffectTiming.None)
                            .Some(cardEffect => cardEffect is ICanNotMoveEffect
                                && cardEffect.CanUse(null)
                                && ((ICanNotMoveEffect)cardEffect).CanNotMove(TopCard, null)))
                    {
                        return false;
                    }
                }
                #endregion

                // ADAPTATION: AS-IS TopCard.PermanentOfThisCard().PermanentFrame.isBreedingAreaFrame() → this
                // permanent's membership in the owner's breeding area (frame model absent, RD-P6C1-1).
                if (new Player(_context, TopCard.Owner).GetBreedingAreaPermanents().Contains(this))
                {
                    if (!new Player(_context, TopCard.Owner).GetBreedingAreaPermanents().Contains(this))
                    {
                        return false;
                    }
                }
                else
                {
                    if (new Player(_context, TopCard.Owner).GetBreedingAreaPermanents().Count > 0)
                        return false;
                }

                if (!IsDigimon)
                    return false;

                if (TopCard.IsDigiEgg && DP <= 0)
                    return false;

                // ADAPTATION: AS-IS `TurnPlayer.fieldCardFrames.Count(empty && battleArea) == 0 → false` — the
                // frame-capacity model has no mirror (RD-P6C1-2); the established no-capacity convention (BT1_089)
                // treats a battle slot as always available, so this guard is omitted.
            }
            else
            {
                return false;
            }

            return true;
        }
    }
    #endregion

    #region このパーマネントが攻撃できるかどうか
    /// <summary>(R1-d) AS-IS <c>Permanent.CanAttack</c> (Permanent.cs:2090-2119).</summary>
    public bool CanAttack(ICardEffect cardEffect, bool withoutTap = false, bool isVortex = false, bool isExecute = false)
    {
        // can not attack with empty cards
        if (TopCard == null)
        {
            return false;
        }

        // can not attack during opponent's turn
        if (TopCard.Owner != new GameContext(_context).TurnPlayer?.PlayerId)
        {
            return false;
        }

        //Can not attack during another attack
        if (HeadlessDCGO.Engine.Assets.Scripts.Script.AttackProcess.For(_context).IsAttacking)
            return false;

        // can not attack to player
        if (!CanAttackTargetDigimon(null, cardEffect, withoutTap, isVortex, isExecute))
        {
            // can not attack to opponent's Digimon
            if (new Player(_context, TopCard.Owner).Enemy!.GetFieldPermanents().Count((permanent) => CanAttackTargetDigimon(permanent, cardEffect, withoutTap, isVortex, isExecute)) == 0)
            {
                return false;
            }
        }

        return true;
    }
    #endregion

    #region このパーマネントが対象の攻撃パーマネントをブロックできるかどうか
    /// <summary>(R1-d) AS-IS <c>Permanent.CanBlock</c> (Permanent.cs:2123-2210). Frame check ADAPTATION per the
    /// section header. NOTE the AS-IS asymmetry preserved: the field-permanent Unblockable scan does NOT apply
    /// the CanNotBeAffected guard, only the player-effect scan does.</summary>
    public bool CanBlock(Permanent AttackingPermanent)
    {
        if (TopCard == null)
        {
            return false;
        }

        if (!IsDigimon)
        {
            return false;
        }

        if (IsSuspended)
        {
            return false;
        }

        if (!CanSuspend)
        {
            return false;
        }

        // ADAPTATION: AS-IS `if (PermanentFrame != null) { if (!IsBattleAreaFrame()) return false; }` (frame model
        // absent) → this permanent must be on the battle area.
        if (!CardEffectCommons.IsPermanentExistsOnBattleArea(this))
        {
            return false;
        }

        if (!AttackingPermanent.IsDigimon)
            return false;

        if (!AttackingPermanent.CanSwitchAttackTarget)
            return false;

        #region "Unblockable" effect

        #region Effects of permanents in play
        foreach (Player player in new GameContext(_context).Players)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICannotBlockEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            if (((ICannotBlockEffect)cardEffect).CannotBlock(AttackingPermanent, this))
                            {
                                return false;
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region player effect
        foreach (Player player in new GameContext(_context).Players)
        {
            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICannotBlockEffect)
                {
                    if (cardEffect.CanUse(null))
                    {
                        if (((ICannotBlockEffect)cardEffect).CannotBlock(AttackingPermanent, this))
                        {
                            if (!TopCard.CanNotBeAffected(cardEffect))
                            {
                                return false;
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #endregion



        return true;
    }
    #endregion

    #region 対象のパーマネントを攻撃できるか
    /// <summary>(R1-d) AS-IS <c>Permanent.CanAttackTargetDigimon</c> (Permanent.cs:2214-2372). Frame check and
    /// summoning-sickness (EnterFieldTurnCount → EnteredThisTurn) ADAPTATIONs per the section header.</summary>
    public bool CanAttackTargetDigimon(Permanent? Defender, ICardEffect cardEffect, bool withoutTap = false, bool isVortex = false, bool isExecute = false)
    {
        if (TopCard != null)
        {
            if (!IsDigimon)
            {
                return false;
            }

            if (!withoutTap)
            {
                if (IsSuspended)
                {
                    return false;
                }

                if (!CanSuspend)
                {
                    return false;
                }
            }

            // ADAPTATION: AS-IS `if (PermanentFrame != null) { if (!IsBattleAreaFrame()) return false; }`.
            if (!CardEffectCommons.IsPermanentExistsOnBattleArea(this))
            {
                return false;
            }

            // ADAPTATION: AS-IS `if (EnterFieldTurnCount == TurnCount)` → the enteredThisTurn substrate flag.
            if (EnteredThisTurn)
            {
                if (!HasRush && !isVortex)
                {
                    return false;
                }
            }

            #region 「対象の防御パーマネントを攻撃できない」効果

            #region 場のパーマネントの効果
            foreach (Player player in new GameContext(_context).Players)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    foreach (ICardEffect cardEffect1 in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect1 is ICanNotAttackTargetDefendingPermanentEffect)
                        {
                            if (cardEffect1.CanUse(null))
                            {
                                if (((ICanNotAttackTargetDefendingPermanentEffect)cardEffect1).CanNotAttackTargetDefendingPermanent(this, Defender))
                                {
                                    if (!TopCard.CanNotBeAffected(cardEffect1))
                                    {
                                        return false;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            #region プレイヤーの効果
            foreach (Player player in new GameContext(_context).Players)
            {
                foreach (ICardEffect cardEffect1 in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect1 is ICanNotAttackTargetDefendingPermanentEffect)
                    {
                        if (cardEffect1.CanUse(null))
                        {
                            if (((ICanNotAttackTargetDefendingPermanentEffect)cardEffect1).CanNotAttackTargetDefendingPermanent(this, Defender))
                            {
                                if (!TopCard.CanNotBeAffected(cardEffect1))
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            #endregion

            if (Defender != null)
            {
                if (Defender.TopCard != null)
                {
                    if (Defender.TopCard.Owner == new Player(_context, TopCard.Owner).Enemy?.PlayerId)
                    {
                        if (Defender.IsDigimon && new Player(_context, Defender.TopCard.Owner).GetBattleAreaPermanents().Contains(Defender))
                        {
                            if (Defender.IsSuspended || isExecute)
                            {
                                return true;
                            }

                            #region 「対象の防御パーマネントを攻撃できる」効果

                            #region 場のパーマネントの効果
                            foreach (Player player in new GameContext(_context).Players)
                            {
                                foreach (Permanent permanent in player.GetFieldPermanents())
                                {
                                    foreach (ICardEffect cardEffect1 in permanent.EffectList(EffectTiming.None))
                                    {
                                        if (cardEffect1 is ICanAttackTargetDefendingPermanentEffect)
                                        {
                                            if (cardEffect1.CanUse(null))
                                            {
                                                if (((ICanAttackTargetDefendingPermanentEffect)cardEffect1).CanAttackTargetDefendingPermanent(this, Defender, cardEffect))
                                                {
                                                    return true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion

                            #region プレイヤーの効果
                            foreach (Player player in new GameContext(_context).Players)
                            {
                                foreach (ICardEffect cardEffect1 in player.EffectList(EffectTiming.None))
                                {
                                    if (cardEffect1 is ICanAttackTargetDefendingPermanentEffect)
                                    {
                                        if (cardEffect1.CanUse(null))
                                        {
                                            if (((ICanAttackTargetDefendingPermanentEffect)cardEffect1).CanAttackTargetDefendingPermanent(this, Defender, cardEffect))
                                            {
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion

                            #endregion
                        }
                    }
                }
            }

            else
            {
                return true;
            }
        }

        return false;
    }
    #endregion

    #region Can Be Destroyed By Battle
    /// <summary>(R1-d) AS-IS <c>Permanent.CanBeDestroyedByBattle</c> (Permanent.cs:3233-3305): CanBeDestroyed()
    /// then a usable <c>ICanNotBeDestroyedByBattleEffect</c> scan over every turn-ordered player's field
    /// permanents, FACE-UP security, and the player.</summary>
    public bool CanBeDestroyedByBattle(Permanent AttackingPermanent, Permanent DefendingPermanent, CardSource DefendingCard)
    {
        if (!CanBeDestroyed())
        {
            return false;
        }

        #region Given Effects
        foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                #region Permanents
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanNotBeDestroyedByBattleEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            if (((ICanNotBeDestroyedByBattleEffect)cardEffect).CanNotBeDestroyedByBattle(this, AttackingPermanent, DefendingPermanent, DefendingCard))
                            {
                                return false;
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
                    if (cardEffect is ICanNotBeDestroyedByBattleEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            if (((ICanNotBeDestroyedByBattleEffect)cardEffect).CanNotBeDestroyedByBattle(this, AttackingPermanent, DefendingPermanent, DefendingCard))
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            #endregion

            #region Player Effects
            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICanNotBeDestroyedByBattleEffect)
                {
                    if (cardEffect.CanUse(null))
                    {
                        if (((ICanNotBeDestroyedByBattleEffect)cardEffect).CanNotBeDestroyedByBattle(this, AttackingPermanent, DefendingPermanent, DefendingCard))
                        {
                            return false;
                        }
                    }
                }
            }
            #endregion
        }
        #endregion

        return true;
    }
    #endregion

    #region このパーマネントが効果で消滅するか
    /// <summary>(R1-d) AS-IS <c>Permanent.CanBeDestroyedBySkill</c> (Permanent.cs:3309-3365): CanNotBeAffected
    /// first, then CanBeDestroyed(), then a usable <c>ICanNotBeDestroyedBySkillEffect</c> scan over every
    /// turn-ordered player's field permanents and the player.</summary>
    public bool CanBeDestroyedBySkill(ICardEffect cardEffect)
    {
        if (this.TopCard != null)
        {
            if (this.TopCard.CanNotBeAffected(cardEffect))
            {
                return false;
            }

            if (!CanBeDestroyed())
            {
                return false;
            }

            #region 効果で消滅しない効果
            foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    #region 場のパーマネントの効果
                    foreach (ICardEffect cardEffect1 in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect1 is ICanNotBeDestroyedBySkillEffect)
                        {
                            if (cardEffect1.CanUse(null))
                            {
                                if (((ICanNotBeDestroyedBySkillEffect)cardEffect1).CanNotBeDestroyedBySkill(this, cardEffect))
                                {
                                    return false;
                                }
                            }
                        }
                    }
                    #endregion
                }

                #region プレイヤーの効果
                foreach (ICardEffect cardEffect1 in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect1 is ICanNotBeDestroyedBySkillEffect)
                    {
                        if (cardEffect1.CanUse(null))
                        {
                            if (((ICanNotBeDestroyedBySkillEffect)cardEffect1).CanNotBeDestroyedBySkill(this, cardEffect))
                            {
                                return false;
                            }
                        }
                    }
                }
                #endregion
            }
            #endregion
        }

        return true;
    }
    #endregion

    #region Can Leave Field
    /// <summary>(R1-d) AS-IS <c>Permanent.CanBeRemoved()</c> (Permanent.cs:3369-3412): TRUE unless a usable
    /// <c>ICanNotBeRemovedEffect</c> (over every turn-ordered player's field permanents and the player) forbids
    /// it.</summary>
    public bool CanBeRemoved()
    {
        #region Effect that never disappears
        foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                #region Effects of permanents in play
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanNotBeRemovedEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            if (((ICanNotBeRemovedEffect)cardEffect).CanNotBeRemoved(this))
                            {
                                return false;
                            }
                        }
                    }
                }
                #endregion
            }

            #region player effect
            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICanNotBeRemovedEffect)
                {
                    if (cardEffect.CanUse(null))
                    {
                        if (((ICanNotBeRemovedEffect)cardEffect).CanNotBeRemoved(this))
                        {
                            return false;
                        }
                    }
                }
            }
            #endregion
        }
        #endregion

        return true;
    }
    #endregion

    #region このデジモンを場からデジクロス条件の代わりにできるか
    /// <summary>(RD-EXT3-01 / RD-R5-04) AS-IS <c>Permanent.CanSubstituteForDigiXrosCondition</c>
    /// (Permanent.cs:3796-3839): TRUE when some usable <c>ICanSelectDigiXrosEffect</c> — over every turn-ordered
    /// player's field permanents and the player themselves — declares this field permanent a legal substitute
    /// material for <paramref name="cardSource"/>'s DigiXros. Substrate: <c>GManager.instance.turnStateMachine
    /// .gameContext.Players_ForTurnPlayer</c> → <c>new GameContext(_context).Players_ForTurnPlayer</c> (the
    /// established Permanent idiom).</summary>
    public bool CanSubstituteForDigiXrosCondition(CardSource cardSource)
    {
        #region デジクロス条件の代わりにできる効果
        foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                #region 場のパーマネントの効果
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanSelectDigiXrosEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            if (((ICanSelectDigiXrosEffect)cardEffect).CanSelect(cardSource, this))
                            {
                                return true;
                            }
                        }
                    }
                }
                #endregion
            }

            #region プレイヤーの効果
            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICanSelectDigiXrosEffect)
                {
                    if (cardEffect.CanUse(null))
                    {
                        if (((ICanSelectDigiXrosEffect)cardEffect).CanSelect(cardSource, this))
                        {
                            return true;
                        }
                    }
                }
            }
            #endregion
        }
        #endregion

        return false;
    }
    #endregion

    #region Can Substitue for Assembly
    /// <summary>(RD-EXT3-02) AS-IS <c>Permanent.CanSubstituteForAssemblyCondition</c> (Permanent.cs:3843-3886):
    /// the Assembly sibling of <see cref="CanSubstituteForDigiXrosCondition"/> — TRUE when some usable
    /// <c>ICanSelectAssemblyEffect</c> declares this field permanent a legal substitute Assembly material for
    /// <paramref name="cardSource"/>. Same turn-ordered field-permanent + player scan.</summary>
    public bool CanSubstituteForAssemblyCondition(CardSource cardSource)
    {
        #region Effects that can be used in place of Assembly conditions
        foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                #region Effects of permanents in play
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanSelectAssemblyEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            if (((ICanSelectAssemblyEffect)cardEffect).CanSelect(cardSource, this))
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
                if (cardEffect is ICanSelectAssemblyEffect)
                {
                    if (cardEffect.CanUse(null))
                    {
                        if (((ICanSelectAssemblyEffect)cardEffect).CanSelect(cardSource, this))
                        {
                            return true;
                        }
                    }
                }
            }
            #endregion
        }
        #endregion

        return false;
    }
    #endregion

    #region 攻撃の対象を変更できるか
    /// <summary>(R1-d) AS-IS <c>Permanent.CanSwitchAttackTarget</c> (Permanent.cs:3746-3792): TRUE unless a usable
    /// <c>ICanNotSwitchAttackTargetEffect</c> (over every turn-ordered player's field permanents and the player)
    /// forbids it. Support member for <see cref="CanBlock"/> (AS-IS reads it there).</summary>
    public bool CanSwitchAttackTarget
    {
        get
        {
            #region アタックの対象が変更できない効果
            foreach (Player player in new GameContext(_context).Players_ForTurnPlayer)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    #region 場のパーマネントの効果
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is ICanNotSwitchAttackTargetEffect)
                        {
                            if (cardEffect.CanUse(null))
                            {
                                if (((ICanNotSwitchAttackTargetEffect)cardEffect).CanNotBeSwitchAttackTarget(this))
                                {
                                    return false;
                                }
                            }
                        }
                    }
                    #endregion
                }

                #region プレイヤーの効果
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanNotSwitchAttackTargetEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            if (((ICanNotSwitchAttackTargetEffect)cardEffect).CanNotBeSwitchAttackTarget(this))
                            {
                                return false;
                            }
                        }
                    }
                }
                #endregion
            }
            #endregion

            return true;
        }
    }
    #endregion

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
            await RemoveLinkCardAsync(
                _context.CardInstanceRepository, _context.ZoneMover, InstanceId, cardSource.InstanceId,
                trash: trashCard, cancellationToken).ConfigureAwait(false);
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

    /// <summary>(MIG4; DELETION-SOURCE re-migration) 1:1 mirror of AS-IS
    /// <c>Permanent.DiscardEvoRoots(ignoreOverflow, putToTrash)</c> (Permanent.cs:106-142), now carrying the AS-IS
    /// body itself: snapshot BOTH root lists (<c>DigivolutionCards.Clone()</c> / <c>LinkedCards.Clone()</c>),
    /// run the ACE-Overflow pass over each list (<c>new AceOverflowClass(roots).Overflow()</c>) unless
    /// <paramref name="ignoreOverflow"/>, then per evo root <c>AddTrashCard</c> (putToTrash) else
    /// <c>RemoveFromAllArea</c>, and per link root <c>RemoveLinkedCard</c> (putToTrash) else
    /// <c>RemoveFromAllArea</c>.
    /// The <c>putToTrash == true</c> arm previously delegated to the substrate
    /// <c>DeletionSourceTrash.TrashEvoSourcesAsync</c>, which folded the same AS-IS lines into a bulk
    /// <c>TrashSourcesAsync(fromBottom:true, honorProtection:false)</c> plus a hand-rolled overflow pass
    /// (<c>ApplyAceOverflow</c>) — both are re-expressed here in the AS-IS primitives, which are live mirrors:
    /// <see cref="CardObjectController.AddTrashCard"/> withdraws the source from the stack and inserts it face-up
    /// at the top of the trash (AS-IS CardObjectController.cs:717-734, no protection check, no
    /// OnDigivolutionCardDiscarded window — the AS-IS deletion trash is direct) and
    /// <see cref="AceOverflowClass"/> applies the AS-IS per-card on-field filter + turn-player-first ordering,
    /// replacing the retired helper's coarser host-on-field guard.</summary>
    public async Task DiscardEvoRoots(bool ignoreOverflow = false, bool putToTrash = true, CancellationToken cancellationToken = default)
    {
        List<CardSource> evoRoots = new List<CardSource>(DigivolutionCards);   // AS-IS :108 DigivolutionCards.Clone()
        List<CardSource> linkRoots = new List<CardSource>(LinkedCards);        // AS-IS :109 LinkedCards.Clone()

        if (!ignoreOverflow)
        {
            // AS-IS :113-114 — evo roots FIRST, then link roots, both while the host is still on the field.
            await new AceOverflowClass(evoRoots).Overflow(cancellationToken).ConfigureAwait(false);
            await new AceOverflowClass(linkRoots).Overflow(cancellationToken).ConfigureAwait(false);
        }

        foreach (CardSource cardSource1 in evoRoots)   // AS-IS :117-128
        {
            if (putToTrash)
            {
                await CardObjectController.AddTrashCard(cardSource1, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await CardObjectController.RemoveFromAllArea(cardSource1, cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (CardSource cardSource2 in linkRoots)   // AS-IS :130-141
        {
            if (putToTrash)
            {
                await RemoveLinkedCard(cardSource2, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await CardObjectController.RemoveFromAllArea(cardSource2, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>(MIG4) AS-IS <c>Permanent.AddDigivolutionCardsTop(added, cardEffect)</c> (Permanent.cs:1064-1123):
    /// move each card off its current zone and insert it just under the top card. AS-IS per-card
    /// <c>cardSources.Insert(1, ...)</c> REVERSES the batch's relative order under the top (last processed ends
    /// up highest) — replicated by reversing before <see cref="Permanent.AddSourcesTopAsync"/>'s
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

        // (MIG4-DETACH-LIVE-TOP) AS-IS RemoveFromAllArea strips THIS permanent's OWN live top out of its stack
        // when the top card itself is in the added batch (App-Fusion: SelectAppFusionEffect.AddToSources issues
        // AddDigivolutionCardsTop({link, TopCard}), Permanent.cs:234). The per-card cardSources.Insert(1, ...)
        // then RE-ROOTS the stack — a different card ends up at index 0 (the new top) and the old top folds under
        // it. The mirror's identity model (permanent id == top instance) expresses that as a top swap, so the
        // batch takes the dedicated re-root arm; a batch that does NOT contain the host's own top keeps the plain
        // place-under path below (identity unchanged).
        if (addedDigivolutionCards.Any(card => card.InstanceId == InstanceId))
        {
            await ReRootAddDigivolutionCardsTopAsync(addedDigivolutionCards, causeEffectSourceId, cancellationToken).ConfigureAwait(false);
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
            await Permanent.AddSourcesTopAsync(
                _context.CardInstanceRepository,
                _context.ZoneMover,
                InstanceId,
                group.ToArray(),
                group.Key,
                cancellationToken: cancellationToken,
                context: _context,
                gameEventQueue: _context.GameEventQueue,
                causeSourceId: causeEffectSourceId ?? default).ConfigureAwait(false);
        }
    }

    /// <summary>(MIG4-DETACH-LIVE-TOP resolved) The RE-ROOT arm of AS-IS <c>AddDigivolutionCardsTop</c>
    /// (Permanent.cs:1064-1122) for the batch that contains THIS permanent's own live top — the App-Fusion fold
    /// <c>SelectAppFusionEffect.AddToSources</c> issues as <c>AddDigivolutionCardsTop({link, TopCard})</c>. It
    /// REPRODUCES the AS-IS list algorithm on a VIRTUAL stack (per-card RemoveFromAllArea + <c>cardSources.Insert
    /// (1, ...)</c>) to derive the final ordered stack, then TRANSLATES that to the mirror identity model: the
    /// final first element becomes the new top; the rest become its ordered digivolution sources (the
    /// <see cref="Permanent.AttachTargetAsSource"/> stack shape — reconstructed on this
    /// dedicated path, WITHOUT touching AttachTargetAsSource itself). The old top physically leaves the field
    /// (→ None, a buried source) and the new top enters it (→ BattleArea, face-up); both moves carry
    /// <see cref="HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.PermanentContinuityKey"/> so the zone-mover lifetime chokepoint
    /// leaves the PERSISTING permanent's just-after bookkeeping alone, and this op owns the
    /// <see cref="HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.ReKey"/> (the digivolve / de-digivolve top-swap seat convention). The
    /// new top re-inits its ported effects (AS-IS <c>CardSource.Init()</c>, as
    /// <see cref="AddCardSource"/> does), and the persisting permanent fires ONE OnAddDigivolutionCards for the
    /// whole batch (AS-IS :1119). Folding ANOTHER permanent's live top (AS-IS
    /// <c>IPlacePermanentToDigivolutionCards</c>) is a DISTINCT re-parenting primitive that is NOW RESOLVED
    /// (MIG4-DETACH-LIVE-TOP, other-host arm, witnessed by LATENT-Close.Witness): that primitive calls
    /// <c>CardObjectController.RemoveField(source)</c> BEFORE the per-card <see cref="AddDigivolutionCardsBottom"/>,
    /// so by detach time the source top is already off the battle area and
    /// <see cref="DetachEmbeddedSourceOrLinkAsync"/> early-returns (no STOP) — the top attaches under the new host 1:1.</summary>
    private async Task ReRootAddDigivolutionCardsTopAsync(
        IReadOnlyList<CardSource> addedDigivolutionCards,
        HeadlessEntityId? causeEffectSourceId,
        CancellationToken cancellationToken)
    {
        HeadlessEntityId oldTopId = InstanceId;
        bool hostIsToken = this.IsToken;

        // The AS-IS virtual cardSources list: top at index 0, then the digivolution sources TOP-MOST first
        // (DigivolutionCards already reports the AS-IS top→bottom order — same mapping as the cardSources getter).
        var virtualStack = new List<HeadlessEntityId> { oldTopId };
        foreach (CardSource currentSource in DigivolutionCards)
        {
            virtualStack.Add(currentSource.InstanceId);
        }

        var addedCards = new List<HeadlessEntityId>();
        bool isFromSameDigimon = false;
        bool isFromDigimon = false;

        foreach (CardSource card in addedDigivolutionCards)
        {
            HeadlessEntityId cardId = card.InstanceId;

            // AS-IS :1073 — an added card already in THIS stack (top OR source).
            if (virtualStack.Contains(cardId))
            {
                isFromSameDigimon = true;
            }

            // AS-IS :1078-1084 — the added card is part of a battle-area permanent that carries >=1 sources; for
            // THIS permanent the live count is the (in-progress) virtual stack, matching the AS-IS in-place list
            // mutation the loop reads back.
            if (CardEffectCommons.IsExistOnBattleArea(card))
            {
                PermanentView cardPermanent = card.PermanentOfThisCard();
                int sourceCount = cardPermanent.TopInstanceId == oldTopId
                    ? virtualStack.Count - 1
                    : cardPermanent.DigivolutionCards.Count;
                if (sourceCount >= 1)
                {
                    isFromDigimon = true;
                }
            }

            // AS-IS :1086 RemoveFromAllArea. A card embedded in ANOTHER permanent / the hand physically detaches
            // here (the re-parenting primitive IPlacePermanentToDigivolutionCards is now RESOLVED — it RemoveFields
            // the source first, so its per-card detach reaches this pre-step off-field and early-returns; a DIRECT
            // Top-fold of another permanent's still-live top has no AS-IS caller and would STOP in
            // DetachEmbeddedSourceOrLinkAsync). One of THIS permanent's own cards
            // (its top or an existing source) is only removed from the virtual stack — the final source rewrite
            // below is authoritative for its new position.
            bool ownStackMember = virtualStack.Contains(cardId);
            virtualStack.Remove(cardId);
            if (cardId != oldTopId && !ownStackMember)
            {
                await DetachEmbeddedSourceOrLinkAsync(card, cancellationToken).ConfigureAwait(false);
            }

            // AS-IS :1088-1096 token guard + cardSources.Insert(1, ...).
            if (!hostIsToken && !card.IsToken)
            {
                virtualStack.Insert(Math.Min(1, virtualStack.Count), cardId);
                addedCards.Add(cardId);
            }
            else if (cardId != oldTopId)
            {
                // AS-IS still pulled the token card off its zone (RemoveFromAllArea) but never attaches it.
                await WithdrawToNoneAsync(card, cancellationToken).ConfigureAwait(false);
            }
        }

        HeadlessEntityId newTopId = virtualStack.Count > 0 ? virtualStack[0] : oldTopId;
        List<HeadlessEntityId> newSourceIds = virtualStack.Skip(1).ToList();

        if (newTopId != oldTopId)
        {
            // Old top → None (now a buried source). Continuity: the AS-IS Permanent object PERSISTS across the
            // swap, so the lifetime chokepoint must not Reset the bookkeeping — this op ReKeys below.
            await _context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(OwnerId, oldTopId, CurrentZoneOf(OwnerId, oldTopId), Headless.Choices.ChoiceZone.None,
                    Metadata: HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.ContinuityMoveMetadata),
                cancellationToken).ConfigureAwait(false);

            // New top → BattleArea (from wherever it sits: a de-linked card in None, or a hand card). Continuity
            // + face-up, mirroring the digivolve top swap.
            HeadlessPlayerId newTopOwner = OwnerOfCard(newTopId) ?? OwnerId;
            await _context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(newTopOwner, newTopId, CurrentZoneOf(newTopOwner, newTopId), Headless.Choices.ChoiceZone.BattleArea,
                    FaceUp: true, Metadata: HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.ContinuityMoveMetadata),
                cancellationToken).ConfigureAwait(false);

            // Any remaining source still sitting in a real zone (a loose hand card folded under) moves off-field.
            foreach (HeadlessEntityId sourceId in newSourceIds)
            {
                if (sourceId == oldTopId)
                {
                    continue;
                }

                HeadlessPlayerId sourceOwner = OwnerOfCard(sourceId) ?? OwnerId;
                Headless.Choices.ChoiceZone sourceZone = CurrentZoneOf(sourceOwner, sourceId);
                if (sourceZone != Headless.Choices.ChoiceZone.None)
                {
                    await _context.ZoneMover.MoveAsync(
                        new ZoneMoveRequest(sourceOwner, sourceId, sourceZone, Headless.Choices.ChoiceZone.None),
                        cancellationToken).ConfigureAwait(false);
                }
            }

            WriteReRootedStack(newTopId, oldTopId, newSourceIds);
            HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.ReKey(_context.CardInstanceRepository, oldTopId, newTopId);
            CardSource.Init(_context, newTopId, OwnerId);
        }
        else
        {
            // Degenerate: the top did not actually change (e.g. only the top itself was added and there was no
            // other card to promote to index 0). Just write the sources back onto the same top.
            WriteReRootedStack(newTopId, oldTopId, newSourceIds);
        }

        // AS-IS :1099-1119 — ONE OnAddDigivolutionCards for the whole batch (subject = the persisting permanent,
        // now topped by newTopId). Same window/metadata as the plain place-under path.
        await EmitAddDigivolutionCardsAsync(
            _context,
            _context.GameEventQueue,
            OwnerId,
            newTopId,
            addedCards.Select(id => id.Value).ToArray(),
            causeEffectSourceId ?? default,
            skip: false,
            isFromSameDigimon,
            isFromDigimon).ConfigureAwait(false);
    }

    /// <summary>(MIG4-DETACH-LIVE-TOP) Write the re-rooted stack onto <paramref name="newTopId"/>: its
    /// <c>sourceIds</c> become <paramref name="newSourceIds"/> (top→bottom, the AttachTargetAsSource shape) and it
    /// INHERITS the persisting permanent's tap / summoning-sickness state from <paramref name="oldTopId"/> — the
    /// AS-IS Permanent object survives the swap (EnterFieldTurnCount / suspend are permanent-level), the same
    /// carry the de-digivolve promote makes (<c>ArmorPurgeTopAsync</c>). A demoted old top keeps its now-stale
    /// <c>sourceIds</c> untouched, exactly as <c>AttachTargetAsSource</c> leaves a digivolved-under target.</summary>
    private void WriteReRootedStack(HeadlessEntityId newTopId, HeadlessEntityId oldTopId, IReadOnlyList<HeadlessEntityId> newSourceIds)
    {
        if (!_context.CardInstanceRepository.TryGetInstance(newTopId, out CardInstanceRecord? newTop) || newTop is null)
        {
            return;
        }

        bool inheritedEnteredThisTurn = false;
        bool inheritedSuspended = false;
        if (_context.CardInstanceRepository.TryGetInstance(oldTopId, out CardInstanceRecord? oldTop) && oldTop is not null)
        {
            inheritedEnteredThisTurn = oldTop.Metadata.TryGetValue("enteredThisTurn", out object? entered) && entered is bool e && e;
            inheritedSuspended = oldTop.Metadata.TryGetValue("isSuspended", out object? susp) && susp is bool s && s;
        }

        var metadata = new Dictionary<string, object?>(newTop.Metadata, StringComparer.Ordinal);
        if (newSourceIds.Count > 0)
        {
            metadata["sourceIds"] = newSourceIds.Select(id => id.Value).ToArray();
        }
        else
        {
            metadata.Remove("sourceIds");
        }

        metadata["enteredThisTurn"] = inheritedEnteredThisTurn;
        metadata["isSuspended"] = inheritedSuspended;
        _context.CardInstanceRepository.Upsert(newTop with { Metadata = metadata });
    }

    /// <summary>The owner of <paramref name="cardId"/> per the instance repository (null when unknown) — the
    /// controller a re-root zone move for a folded card needs.</summary>
    private HeadlessPlayerId? OwnerOfCard(HeadlessEntityId cardId) =>
        _context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) && record is not null
            ? record.OwnerId
            : null;

    /// <summary>(MIG4) AS-IS <c>Permanent.AddDigivolutionCardsBottom(added, cardEffect, skipEffectAndActivateSkill,
    /// isFacedown)</c> (Permanent.cs:1133-1227): move each card off its current zone and append it to the bottom
    /// of the stack (loop-order append, no reversal — unlike Top). Same token guard and multi-zone-emit design
    /// item as Top. AS-IS writes the buried source's face state in the per-card loop (:1194-1197): <c>isFacedown
    /// =&gt; cardSource.SetReverse()</c> (IsFlipped = true), else <c>cardSource.SetFace()</c> (IsFlipped = false)
    /// — mirrored via <see cref="SetSourceFaceState"/> on the shared <c>isFlipped</c> instance flag, BEFORE the
    /// batch OnAddDigivolutionCards emit (AS-IS SetReverse precedes StackSkillInfos), so the read side
    /// (<see cref="EffectList_ForCard"/>'s <c>if (!cardSource.IsFlipped)</c>) already excludes a face-down source's
    /// inherited effects. (MIG4-ADDDIGI-FACEDOWN resolved.)</summary>
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

        bool hostIsToken = this.IsToken;
        var toAttach = new List<HeadlessEntityId>();
        foreach (CardSource card in addedDigivolutionCards)
        {
            await DetachEmbeddedSourceOrLinkAsync(card, cancellationToken).ConfigureAwait(false);

            if (!hostIsToken && !card.IsToken)
            {
                toAttach.Add(card.InstanceId);
                // AS-IS Permanent.cs:1194-1197: SetReverse() when face-down, else SetFace() — in the per-card
                // loop, i.e. before the batch OnAddDigivolutionCards emit below.
                SetSourceFaceState(card.InstanceId, isFacedown);
            }
            else
            {
                await WithdrawToNoneAsync(card, cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (IGrouping<Headless.Choices.ChoiceZone, HeadlessEntityId> group in toAttach.GroupBy(id => CurrentZoneOf(OwnerId, id)))
        {
            await Permanent.AddSourcesBottomAsync(
                _context.CardInstanceRepository,
                _context.ZoneMover,
                InstanceId,
                group.ToArray(),
                group.Key,
                cancellationToken: cancellationToken,
                context: _context,
                gameEventQueue: _context.GameEventQueue,
                causeSourceId: causeEffectSourceId ?? default,
                skipEffectAndActivateSkill: skipEffectAndActivateSkill).ConfigureAwait(false);
        }
    }

    /// <summary>(MIG4-ADDDIGI-FACEDOWN) AS-IS per-source face write in
    /// <c>AddDigivolutionCardsBottom</c> (Permanent.cs:1194-1197): <c>cardSource.SetReverse()</c> sets
    /// <c>IsFlipped = true</c> and <c>cardSource.SetFace()</c> sets it false. The mirror <see cref="CardSource.IsFlipped"/>
    /// is the shared <c>isFlipped</c> instance-metadata flag (CardSource.cs:531-533; same round-trip as
    /// <c>SelectCardEffect.SetFaceMirror</c> — no new store): stamp boxed <c>true</c> for face-down, remove the key
    /// for face-up (absent = face-up, exactly as <c>SetFaceMirror</c> clears it). The gameplay-hook side effects of
    /// AS-IS <c>SetReverse/SetFace</c> (<c>GManager.OnCardFlippedChanged</c> / <c>OnSecurityStackChanged</c> — pure
    /// UI redraw events, CardSource.cs:83-96) carry no rule effect and have no headless counterpart.</summary>
    private void SetSourceFaceState(HeadlessEntityId cardId, bool faceDown)
    {
        if (!_context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
        if (faceDown)
        {
            metadata["isFlipped"] = true;   // AS-IS SetReverse(): IsFlipped = true
        }
        else
        {
            metadata.Remove("isFlipped");   // AS-IS SetFace(): IsFlipped = false
        }

        _context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    /// <summary>(MIG4; LINK re-migration) AS-IS <c>Permanent.AddLinkCard(addedLinkCard, cardEffect)</c>
    /// (Permanent.cs:1237-1292), re-homed here from the retired substrate <c>LinkHelpers.AddLinkCardAsync</c> and
    /// realigned to the AS-IS statement order: isFromDigimon (PRE-move, :1241-1249) → pre-attach overflow via
    /// <see cref="RemoveLinkedCard"/> (:1251-1256) → RemoveFromAllArea (:1259) → the
    /// <c>!this.IsToken &amp;&amp; !addedLinkCard.IsToken</c> attach (:1261-1268) → the single WhenLinked emit
    /// (:1270-1291). The invented post-attach max enforcement is retired (see the link region header): AS-IS
    /// sweeps a standing over-max through <c>AutoProcessing.DigimonLackLinkMaxCountProcess</c>.</summary>
    public async Task AddLinkCard(
        CardSource addedLinkCard,
        HeadlessEntityId? causeEffectSourceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addedLinkCard);

        // (C1d RDW-02) AS-IS :1241-1249 — isFromDigimon == the added card is currently a battle-area permanent
        // whose stack has >=1 digivolution source. Evaluated PRE-move (post-move it is off-field ⇒ false).
        bool isFromDigimon =
            CardEffectCommons.IsExistOnBattleArea(addedLinkCard)
            && addedLinkCard.PermanentOfThisCard().DigivolutionCards.Count >= 1;

        // AS-IS :1251-1256 — the overflow is resolved BEFORE the attach, through RemoveLinkedCard:
        //   LinkedMax > 1 ⇒ RemoveLinkedCard(null, (LinkedCards.Count + 1) - LinkedMax)  (owner SELECTS)
        //   LinkedMax <= 1 ⇒ RemoveLinkedCard(LinkedCards[0])                            (silent oldest)
        List<CardSource> preLinked = LinkedCards;
        int preMax = LinkedMax;
        if (preLinked.Count >= preMax && preLinked.Count >= 1)
        {
            if (preMax > 1)
            {
                await RemoveLinkedCard(null, (preLinked.Count + 1) - preMax, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await RemoveLinkedCard(preLinked[0], cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        // AS-IS :1259 CardObjectController.RemoveFromAllArea(addedLinkCard) — detach from whatever permanent it
        // is embedded in, then pull it out of its concrete zone (link cards are stored off-field).
        await DetachEmbeddedSourceOrLinkAsync(addedLinkCard, cancellationToken).ConfigureAwait(false);
        await WithdrawToNoneAsync(addedLinkCard, cancellationToken).ConfigureAwait(false);

        if (this.IsToken || addedLinkCard.IsToken)
        {
            // AS-IS :1261 — the token guard skips the attach; the RemoveFromAllArea withdrawal already ran.
            return;
        }

        // (B-3 tuck reset) AS-IS AddLinkCard resets the attached card's per-turn use counts
        // (cardSource.cEntity_EffectController.InitUseCountThisTurn(), CardController.cs:3393).
        // (R6-Da'-6 D3) per-card cap reset on the CEntity_EffectController store.
        CEntity_EffectControllerStore.ResetUseCountForCard(_context, addedLinkCard.InstanceId);

        // AS-IS :1263-1267 — LinkedCards.Insert(0, addedLinkCard); LinkedDP += addedLinkCard.LinkDP;
        // cardSources.Insert(1, addedLinkCard); addedLinkCard.SetFace();
        AttachLinkCard(_context.CardInstanceRepository, InstanceId, addedLinkCard.InstanceId);
        SetSourceFaceState(addedLinkCard.InstanceId, faceDown: false);

        // AS-IS :1270-1291 — the single WhenLinked window ({Permanent, CardEffect, Card, isFromDigimon}).
        // (G-Link P2 risk-1) the causing effect's SOURCE id is threaded as the AS-IS {"CardEffect", cardEffect};
        // empty when the caller passes no cause ⇒ a NULL CardEffect, exactly as AS-IS.
        await EmitWhenLinkedAsync(
            _context,
            OwnerId,
            InstanceId,
            addedLinkCard,
            causeEffectSourceId ?? default,
            isFromDigimon).ConfigureAwait(false);
    }

    /// <summary>(R4 S3b-2) AS-IS <c>Permanent.StackCards</c> (Permanent.cs:884) — verbatim: every stacked card
    /// EXCEPT the link cards (top + digivolution sources).</summary>
    public List<CardSource> StackCards => cardSources.Filter(cardSource => !LinkedCards.Contains(cardSource));

    // ==== (R4 S3b-2②) AS-IS "just-after" bookkeeping fields (Permanent.cs:3686-3941) — carried by the
    // match-scoped HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore (the mirror Permanent is a per-access view; see the store header
    // for the CREATE/PERSIST/DIE lifetime mapping). Names, defaults and mutability verbatim.

    /// <summary>AS-IS <c>Permanent.PlayingEffect</c> (:3686) — the effect that PLAYED this permanent (null for
    /// a normal main-phase play).</summary>
    public ICardEffect? PlayingEffect
    {
        get => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).PlayingEffect;
        set => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).PlayingEffect = value;
    }

    /// <summary>AS-IS <c>Permanent.DigivolvingEffect</c> (:3690) — the effect that DIGIVOLVED this permanent
    /// (null for a normal digivolve; read by <c>CardEffectCommons.IsDigivolvedByTheEffect</c>).</summary>
    public ICardEffect? DigivolvingEffect
    {
        get => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).DigivolvingEffect;
        set => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).DigivolvingEffect = value;
    }

    /// <summary>(RD-EXT3-05) AS-IS <c>Permanent.PlaceOtherPermanentEffect</c> (:3674) — the effect that
    /// re-parented this permanent under another (recorded by <c>IPlacePermanentToDigivolutionCards</c> before the
    /// source leaves the field). NOTE the view-model DIE semantics: the source permanent is Reset at the zone
    /// chokepoint the instant it leaves the field, so a read after re-parent returns the AS-IS field default —
    /// consistent with there being no ported consumer that reads a dead permanent's PlaceOtherPermanentEffect.</summary>
    public ICardEffect? PlaceOtherPermanentEffect
    {
        get => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).PlaceOtherPermanentEffect;
        set => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).PlaceOtherPermanentEffect = value;
    }

    /// <summary>AS-IS <c>Permanent.LevelJustAfterPlayed</c> (:3890, −1 = never played).</summary>
    public int LevelJustAfterPlayed
    {
        get => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).LevelJustAfterPlayed;
        set => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).LevelJustAfterPlayed = value;
    }

    /// <summary>AS-IS <c>Permanent.PlayCostJustAfterPlayed</c> (:3894, −1 = never played / no play cost).</summary>
    public int PlayCostJustAfterPlayed
    {
        get => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).PlayCostJustAfterPlayed;
        set => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).PlayCostJustAfterPlayed = value;
    }

    /// <summary>AS-IS <c>Permanent.CardNamesJustAfterPlayed</c> (:3898).</summary>
    public List<string> CardNamesJustAfterPlayed
    {
        get => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).CardNamesJustAfterPlayed;
        set => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).CardNamesJustAfterPlayed = value;
    }

    /// <summary>AS-IS <c>Permanent.CardNamesJustAfterDigivolved</c> (:3902).</summary>
    public List<string> CardNamesJustAfterDigivolved
    {
        get => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).CardNamesJustAfterDigivolved;
        set => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).CardNamesJustAfterDigivolved = value;
    }

    /// <summary>AS-IS <c>Permanent.TraitsJustAfterPlayed</c> (:3906).</summary>
    public List<string> TraitsJustAfterPlayed
    {
        get => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).TraitsJustAfterPlayed;
        set => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).TraitsJustAfterPlayed = value;
    }

    /// <summary>AS-IS <c>Permanent.IsBurstDigivolved</c> (:3938).</summary>
    public bool IsBurstDigivolved
    {
        get => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).IsBurstDigivolved;
        set => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).IsBurstDigivolved = value;
    }

    /// <summary>AS-IS <c>Permanent.IsAppFusion</c> (Permanent.cs, sibling of IsBurstDigivolved).</summary>
    public bool IsAppFusion
    {
        get => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).IsAppFusion;
        set => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).IsAppFusion = value;
    }

    /// <summary>(RD-R5-02) AS-IS <c>Permanent.IsReturnedToHandByBurstDigivolution</c> (Permanent.cs:3930) — the
    /// burst-bounce marker HandBounceClaass.Bounce (:2789) stamps and <see cref="SelectBurstDigivolutionEffect"/>
    /// .BounceTamer reads back to gate TamerBounced. Backed by the bookkeeping store (sibling of IsBurstDigivolved).</summary>
    public bool IsReturnedToHandByBurstDigivolution
    {
        get => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).IsReturnedToHandByBurstDigivolution;
        set => HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).IsReturnedToHandByBurstDigivolution = value;
    }

    /// <summary>(R4 S3b-2①) AS-IS <c>Permanent.AddCardSource(cardSource)</c> (Permanent.cs:1045-1053): the new
    /// card becomes this permanent's stack TOP (AS-IS <c>cardSources.Insert(0, cardSource)</c>; face handling =
    /// the zone move's face stamp). SUBSTRATE MAPPING: the mirror permanent's identity IS its top card's zone
    /// residency, so the op is —
    /// <list type="bullet">
    /// <item>the old top leaves the field zone (a plain Move → None, the verified digivolve-op shape
    /// (DigivolveAction targetRemoval): NOT a leave-field kind, no windows fire);</item>
    /// <item>the new card enters the SAME zone under the same controller (no OnEnterField supply metadata —
    /// the AS-IS executor opens the window inline, design doc S3b-2① option B);</item>
    /// <item>the stack threads via the verified <see cref="Permanent.AttachTargetAsSource"/>
    /// (sourceIds + the N-1 entered-this-turn inheritance);</item>
    /// <item>G6-001: the new top auto-registers its ported effects (the old top's registration stays — its
    /// inherited half folds into the new top, per the digivolve action's model).</item>
    /// </list>
    /// RETURNS the refreshed view: the mirror view keys on the top instance, so the AS-IS in-place mutation is
    /// expressed as view replacement — callers rebind (<c>permanent = await permanent.AddCardSource(card)</c>).
    /// ADAPTATION (view semantics), documented in the executor port.</summary>
    public async Task<Permanent> AddCardSource(CardSource cardSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cardSource);

        HeadlessEntityId oldTopId = InstanceId;
        HeadlessPlayerId controller = OwnerId;
        Headless.Choices.ChoiceZone fieldZone = CurrentZoneOf(controller, oldTopId);
        if (fieldZone == Headless.Choices.ChoiceZone.None)
        {
            fieldZone = Headless.Choices.ChoiceZone.BattleArea;
        }

        // (RD-R3-02) both halves of the top swap carry the continuity marker: the AS-IS Permanent object
        // PERSISTS across AddCardSource, so the zone-mover lifetime chokepoint must not Reset either card's
        // bookkeeping — AttachTargetAsSource ReKeys it below.
        await _context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(controller, oldTopId, fieldZone, Headless.Choices.ChoiceZone.None,
                Metadata: HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.ContinuityMoveMetadata),
            cancellationToken).ConfigureAwait(false);

        Headless.Choices.ChoiceZone cardFrom = CurrentZoneOf(cardSource.Owner, cardSource.InstanceId);
        await _context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(controller, cardSource.InstanceId, cardFrom, fieldZone,
                Metadata: HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.ContinuityMoveMetadata),
            cancellationToken).ConfigureAwait(false);

        AttachTargetAsSource(
            _context.CardInstanceRepository,
            cardSource.InstanceId,
            oldTopId);

        CardSource.Init(_context, cardSource.InstanceId, controller);
        return new Permanent(_context, cardSource.InstanceId, controller);
    }

    /// <summary>(MIG4) AS-IS <c>Permanent.RemoveCardSource(cardSource)</c> (Permanent.cs:1297-1302): a bare
    /// removal from this permanent's stack list (AS-IS `cardSources.Remove(cardSource)`) — NO zone move, NO
    /// trash/trigger. Delegates to <see cref="Permanent.PlaySpecificSourceAsync"/> with
    /// <c>destination: ChoiceZone.None</c> (its documented detach-only mode: remove from sourceIds, skip the
    /// physical move). No-ops silently if the card is not one of this permanent's sources (List.Remove parity).</summary>
    public async Task RemoveCardSource(CardSource cardSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cardSource);
        await Permanent.PlaySpecificSourceAsync(
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
    /// demote) when the added card is currently some permanent's battling top — an identity-corrupting edge for the
    /// headless model (permanent id == top identity). The one shape that folds the HOST's OWN live top into its own
    /// stack (App-Fusion: <c>AddDigivolutionCardsTop({link, TopCard})</c>) is RESOLVED — handled as a top swap by
    /// <see cref="ReRootAddDigivolutionCardsTopAsync"/>, never reaching this pre-step. Folding ANOTHER permanent's
    /// live top under this one (AS-IS <c>IPlacePermanentToDigivolutionCards</c> — its own re-parenting primitive) is
    /// ALSO RESOLVED (MIG4-DETACH-LIVE-TOP, witnessed by LATENT-Close.Witness): that primitive performs
    /// <c>CardObjectController.RemoveField(source)</c> BEFORE its per-card add, so the source top is off the battle
    /// area by the time it reaches this pre-step and <see cref="CardSource.PermanentOfThisCard"/> is empty → the
    /// guard below early-returns and the top attaches under the new host 1:1. The throw remains a guard for a shape
    /// with NO AS-IS caller: a DIRECT AddDigivolutionCardsBottom / AddLinkCard of a card that is STILL some field
    /// permanent's live top (no prior RemoveField) — AS-IS <c>RemoveDigivolveRootEffect</c>, out of scope.</summary>
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
                "re-parents/demotes a card that is STILL a live field top DIRECTLY (no AS-IS caller does this: " +
                "IPlacePermanentToDigivolutionCards RemoveFields the source first; the host's OWN live top fold is the " +
                "AddDigivolutionCardsTop re-root arm) — design item MIG4-DETACH-LIVE-TOP (residual direct-path guard).");
        }

        if (_context.CardInstanceRepository.TryGetInstance(host.TopInstanceId, out CardInstanceRecord? hostRecord) && hostRecord is not null
            && ReadLinkedCardIds(hostRecord.Metadata).Contains(card.InstanceId))
        {
            await RemoveLinkCardAsync(
                _context.CardInstanceRepository,
                _context.ZoneMover,
                host.TopInstanceId,
                card.InstanceId,
                trash: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        await Permanent.PlaySpecificSourceAsync(
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

    // ========================================================================================================
    // #region Digivolution-stack (AS-IS `cardSources`) mutations
    //
    // (DIGIVOLVE cluster re-migration) Re-homed VERBATIM from the retired substrate `DigivolutionStackHelpers`
    // (+ `DeDigivolveHelpers.ArmorPurgeTopAsync`) into their AS-IS原가: `Permanent.AddCardSource` /
    // `AddDigivolutionCardsTop` / `AddDigivolutionCardsBottom` / `RemoveCardSource` (Permanent.cs:1045 / 1064 /
    // 1133 / 1297) — i.e. the `List<CardSource> cardSources` mutations that AS-IS performs on the live Permanent
    // object itself. In the mirror that list is the top card's `sourceIds` instance metadata (ordered top→bottom),
    // so the AS-IS list ops become metadata rewrites; they stay STATIC (host-id-keyed) because a mirror Permanent
    // view is transient and the same op is issued for hosts other than `this` (keyword / effect paths).
    //
    // ONLY change from the retired file: the dead `TriggerEventEmitter.Emit(...)` calls are DROPPED — that
    // substrate event-queue trigger model is retired (GameEventQueue.cs header: "all trigger windows now fire via
    // the AS-IS AutoProcessing.StackSkillInfos inline"), and the OnAddDigivolutionCards window is now opened by
    // the AS-IS `StackSkillInfos(hashtable, EffectTiming.OnAddDigivolutionCards)` call ported 1:1 from
    // Permanent.cs:1105-1119 / 1213-1223 (see EmitAddDigivolutionCardsAsync). The `gameEventQueue` parameters are
    // KEPT: callers use them as the AS-IS "does this path open the window" discriminator.
    // Retired with zero live callers: `ReturnSourcesAsync`, `DetachSourceFromHostAsync`,
    // `DeDigivolveHelpers.DeDigivolveAsync` (IDegeneration owns the AS-IS de-digivolve walk), and
    // `DeDigivolveHelpers.IsDeDigivolveImmune` (a one-line wrapper — inlined at its two CardController sites).
    // ========================================================================================================

    /// <summary>Instance-metadata key holding the ordered (top→bottom) digivolution-source id list — the mirror
    /// of AS-IS <c>Permanent.cardSources</c>. Re-homed from the retired substrate helpers; string value unchanged.</summary>
    internal const string SourceIdsKey = "sourceIds";

    /// <summary>Instance-metadata flag behind AS-IS <c>Permanent.IsSuspended</c>. Re-homed from the retired
    /// substrate helpers; string value unchanged (the literal readers in this file keep matching).</summary>
    internal const string IsSuspendedKey = "isSuspended";

    /// <summary>Instance-metadata flag behind AS-IS <c>Permanent.CanSuspend</c>.</summary>
    internal const string CanSuspendKey = "canSuspend";

    /// <summary>Instance-metadata stamp behind the STATIC half of AS-IS <c>Permanent.ImmuneFromDeDigivolve()</c>
    /// (<c>IImmuneFromDeDigivolveEffect</c>); the CONTINUOUS half is the live
    /// <see cref="ImmuneFromDeDigivolve"/> scan.</summary>
    internal const string CannotBeDeDigivolvedKey = "cannotBeDeDigivolved";

    // Deletion markers stripped from a top card that is trashed by a top-swap (the deletion was REPLACED, so no
    // OnDeletion / POST window may open for it). Byte-identical to the substrate constants they mirror.
    private const string DeletedByBattleMetadataKey = "deletedByBattle";
    private const string DeletedByEffectMetadataKey = "deletedByEffect";
    private const string DeletedByOwnEffectMetadataKey = "deletedByOwnEffect";
    internal const string PendingDeletionMetadataKey = "pendingDeletion";

    /// <summary>Instance-metadata opt-out behind AS-IS <c>Permanent.IsPlaceToTrashDueToNotHavingDP</c>
    /// (Permanent.cs:3694, default true — ABSENT/true is the default, explicit <c>false</c> is the opt-out).
    /// Re-homed from the retired substrate <c>GameFlowProcessor</c>; string value unchanged.</summary>
    internal const string PlaceToTrashDueToNoDpKey = "isPlaceToTrashDueToNotHavingDP";

    /// <summary>Instance-metadata flag behind AS-IS <c>Permanent.IsPlayedOptionPermanent</c> (Permanent.cs:3946,
    /// default false). Re-homed from the retired substrate <c>GameFlowProcessor</c>; string value unchanged.</summary>
    internal const string IsPlayedOptionPermanentKey = "isPlayedOptionPermanent";

    // ========================================================================================================
    // #region Link cards (AS-IS `LinkedCards` / `LinkedDP` / `LinkedMax` / `AddLinkCard` / `RemoveLinkedCard`)
    //
    // (LINK cluster re-migration) Re-homed VERBATIM from the retired substrate `LinkHelpers` into its AS-IS 원가:
    // DCGO/Assets/Scripts/Script/Permanent.cs — `AddLinkCard` (:1237-1292), `RemoveLinkedCard` (:1306-1348),
    // `LinkedCards` (:1041), `LinkedMax` (:896), `LinkedDP` (:670). AS-IS holds the link cards in the live
    // `List<CardSource> LinkedCards` ON the Permanent object; the mirror holds the same list as the HOST top
    // card's `linkedCardIds` instance metadata (newest-first — the AS-IS `Insert(0, …)` order), so the AS-IS
    // list ops become metadata rewrites. The helpers stay STATIC (host-id-keyed) for the same reason as the
    // digivolution-stack block above: a mirror Permanent view is transient and the same op is issued for hosts
    // other than `this`.
    //
    // Changes from the retired substrate file (both forced, both structural — no logic simplification):
    //  * the dead `TriggerEventEmitter.Emit(WhenLinked / OnLinkCardDiscarded)` calls are DROPPED — that substrate
    //    event-queue trigger model is retired (GameEventQueue.cs header). The AS-IS WhenLinked window is opened
    //    by the 1:1 `StackSkillInfos(hashtable, EffectTiming.WhenLinked)` port (AS-IS Permanent.cs:1278-1290,
    //    see EmitWhenLinkedAsync), exactly like the sibling OnAddDigivolutionCards emit. AS-IS RemoveLinkedCard
    //    opens NO window at all (AS-IS :1345 "//TODO: Add event call if something was removed"), so the retired
    //    OnLinkCardDiscarded emit has no AS-IS replacement — nothing is lost.
    //  * the substrate-invented post-attach `EnforceLinkedMaxAsync` + its `TrimLinkedOverflowByOwnerSelectionAsync`
    //    / `ResolveHostZone` duplicate selection are RETIRED: AS-IS resolves the overflow BEFORE the attach
    //    (`if (LinkedCards.Count >= LinkedMax) { … RemoveLinkedCard(…) }`, AS-IS :1251-1257) and the standing
    //    over-max state is swept by the AS-IS rule process `DigimonLackLinkMaxCountProcess`
    //    (AS-IS AutoProcessing.cs:524-537), which the mirror AutoProcessing already runs 1:1. `AddLinkCard` below
    //    now issues the AS-IS pre-attach `RemoveLinkedCard` calls verbatim.
    // ========================================================================================================

    /// <summary>Host instance-metadata key holding the ordered (newest-first) link-card id list — the mirror of
    /// AS-IS <c>Permanent.LinkedCards</c>. Re-homed from the retired substrate helper; string value unchanged.</summary>
    internal const string LinkedCardIdsKey = "linkedCardIds";

    /// <summary>Host instance-metadata key behind AS-IS <c>Permanent.LinkedDP</c> (:670).</summary>
    internal const string LinkedDpKey = "linkedDp";

    /// <summary>Host instance-metadata key carrying an effect-written base override of AS-IS
    /// <c>Permanent.LinkedMax</c> (:896).</summary>
    internal const string LinkedMaxKey = "linkedMax";

    /// <summary>Per-card definition-metadata key behind AS-IS <c>CardSource.LinkDP</c>.</summary>
    internal const string LinkDpKey = "linkDp";

    /// <summary>AS-IS <c>Permanent.LinkedMax</c> default (1).</summary>
    internal const int DefaultLinkedMax = 1;

    /// <summary>The link cards currently attached to <paramref name="metadata"/>'s host (newest first) — the
    /// mirror read of AS-IS <c>Permanent.LinkedCards</c>.</summary>
    internal static IReadOnlyList<HeadlessEntityId> ReadLinkedCardIds(IReadOnlyDictionary<string, object?> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (!metadata.TryGetValue(LinkedCardIdsKey, out object? raw) || raw is null)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        return raw switch
        {
            IEnumerable<HeadlessEntityId> ids => ids.ToArray(),
            IEnumerable<string> strings => strings
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => new HeadlessEntityId(value))
                .ToArray(),
            string text => text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => new HeadlessEntityId(value))
                .ToArray(),
            _ => Array.Empty<HeadlessEntityId>(),
        };
    }

    /// <summary>AS-IS <c>Permanent.LinkedDP</c> read (sum of the attached cards' LinkDP).</summary>
    internal static int ReadLinkedDp(IReadOnlyDictionary<string, object?> metadata) =>
        ReadLinkMetadataInt(metadata, LinkedDpKey) ?? 0;

    /// <summary>The host's BASE link maximum (its <see cref="LinkedMaxKey"/> override, else
    /// <see cref="DefaultLinkedMax"/>) — the value AS-IS <c>Permanent.LinkedMax</c> folds effects onto.</summary>
    internal static int ReadLinkedMax(IReadOnlyDictionary<string, object?> metadata) =>
        ReadLinkMetadataInt(metadata, LinkedMaxKey) ?? DefaultLinkedMax;

    /// <summary>
    /// (AS-IS <c>Permanent.RemoveLinkedCard</c>'s storage half, :1308-1319) Detach <paramref name="linkCardId"/>
    /// from <paramref name="hostId"/>: remove it from the linked list, subtract its LinkDP, and optionally trash
    /// it (AS-IS <c>CardObjectController.AddTrashCard</c>). Returns true when removed. AS-IS opens NO window here.
    /// </summary>
    internal static async Task<bool> RemoveLinkCardAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        HeadlessEntityId linkCardId,
        bool trash = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
        {
            return false;
        }

        List<string> linked = ReadLinkedCardIds(host.Metadata).Select(id => id.Value).ToList();
        if (!linked.Remove(linkCardId.Value))
        {
            return false;
        }

        int linkDp = repository.TryGetInstance(linkCardId, out CardInstanceRecord? linkCard) && linkCard is not null
            ? ReadLinkMetadataInt(linkCard.Metadata, LinkDpKey) ?? 0
            : 0;
        int linkedDp = Math.Max(0, ReadLinkedDp(host.Metadata) - linkDp);
        repository.Upsert(host with { Metadata = WithLinked(host.Metadata, linked, linkedDp) });

        if (trash && linkCard is not null)
        {
            await zoneMover.MoveAsync(
                new ZoneMoveRequest(linkCard.OwnerId, linkCardId, Headless.Choices.ChoiceZone.None, Headless.Choices.ChoiceZone.Trash),
                cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>(AS-IS <c>Permanent.AddLinkCard</c>'s storage half, :1263-1267) Prepend
    /// <paramref name="linkCardId"/> to <paramref name="hostId"/>'s linked list (AS-IS <c>Insert(0, …)</c>) and
    /// add its LinkDP (AS-IS <c>LinkedDP += addedLinkCard.LinkDP</c>).</summary>
    internal static void AttachLinkCard(
        ICardInstanceRepository repository,
        HeadlessEntityId hostId,
        HeadlessEntityId linkCardId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (!repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
        {
            return;
        }

        List<string> linked = ReadLinkedCardIds(host.Metadata).Select(id => id.Value).ToList();
        linked.Insert(0, linkCardId.Value);
        int linkDp = repository.TryGetInstance(linkCardId, out CardInstanceRecord? linkCard) && linkCard is not null
            ? ReadLinkMetadataInt(linkCard.Metadata, LinkDpKey) ?? 0
            : 0;
        repository.Upsert(host with { Metadata = WithLinked(host.Metadata, linked, ReadLinkedDp(host.Metadata) + linkDp) });
    }

    private static Dictionary<string, object?> WithLinked(IReadOnlyDictionary<string, object?> metadata, IReadOnlyList<string> linked, int linkedDp)
    {
        var copy = new Dictionary<string, object?>(metadata, StringComparer.Ordinal);
        if (linked.Count > 0)
        {
            copy[LinkedCardIdsKey] = linked.ToArray();
            copy[LinkedDpKey] = linkedDp;
        }
        else
        {
            copy.Remove(LinkedCardIdsKey);
            copy.Remove(LinkedDpKey);
        }

        return copy;
    }

    private static int? ReadLinkMetadataInt(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            int i => i,
            long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
            string s when int.TryParse(s, out int parsed) => parsed,
            _ => null,
        };
    }

    /// <summary>AS-IS <c>AddLinkCard</c>'s <c>StackSkillInfos(hashtable, EffectTiming.WhenLinked)</c>
    /// (Permanent.cs:1278-1290), ported 1:1: payload <c>{Permanent, CardEffect, Card, isFromDigimon}</c>, ONE
    /// emit, gated on <c>addedCard</c>. The AS-IS <c>cardEffect</c> is the mirror's threaded causing-effect
    /// SOURCE id collapsed to a <see cref="BareCauseEffect"/> (empty id ⇒ NULL CardEffect, exactly as AS-IS).
    /// Sibling of <see cref="EmitAddDigivolutionCardsAsync"/>.</summary>
    private static async Task EmitWhenLinkedAsync(
        EngineContext context,
        HeadlessPlayerId hostOwner,
        HeadlessEntityId hostId,
        CardSource addedLinkCard,
        HeadlessEntityId causeSourceId,
        bool isFromDigimon)
    {
        using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(context);
        var hashtable = new System.Collections.Hashtable
        {
            { "Permanent", new Permanent(context, hostId, hostOwner) },
            { "CardEffect", BareCauseEffect.ForOrNull(context, causeSourceId) },
            { "Card", addedLinkCard },
            { "isFromDigimon", isFromDigimon },
        };

        await HeadlessDCGO.Engine.Assets.Scripts.Script.AutoProcessing.For(context)
            .StackSkillInfos(hashtable, EffectTiming.WhenLinked).ConfigureAwait(false);
    }
    // #endregion Link cards

    /// <summary>(AS-IS <c>AddDigivolutionCardsBottom</c>, Permanent.cs:1133-1223) Moves <paramref name="cards"/>
    /// from <paramref name="fromZone"/> off-field and appends them (in order) to the BOTTOM of
    /// <paramref name="targetId"/>'s digivolution stack.
    /// (B-3 tuck reset; R6-Da'-6 D3) When <paramref name="context"/> is supplied, each tucked card's per-turn use counts are
    /// cleared — AS-IS clears them on EVERY path that puts a card under another permanent:
    /// PlacePermanentToDigivolutionCards runs InitUseCountThisTurn on the tucked card (CardController.cs:3093)
    /// after RemoveField already Init()-reset the whole leaving stack (CardObjectController.cs:546-553); DigiXros
    /// materials reset per card (SelectDigiXrosClass.cs:923); hand/deck/trash-origin cards were reset when they
    /// entered that zone, so the uniform reset is a no-op for them.</summary>
    internal static async Task AddSourcesBottomAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId targetId,
        IReadOnlyList<HeadlessEntityId> cards,
        Headless.Choices.ChoiceZone fromZone,
        CancellationToken cancellationToken = default,
        EngineContext? context = null,
        GameEventQueue? gameEventQueue = null,
        HeadlessEntityId causeSourceId = default,
        bool skipEffectAndActivateSkill = false)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        ArgumentNullException.ThrowIfNull(cards);

        if (cards.Count == 0 || !repository.TryGetInstance(targetId, out CardInstanceRecord? target) || target is null)
        {
            return;
        }

        // (C1d RDW-02) compute the AS-IS from-flags BEFORE the cards move (post-move they read wrong).
        (bool isFromSame, bool isFromDigimon) = ComputeAddDigivolutionFromFlags(repository, zoneMover, target, cards);
        var appended = new List<string>();
        foreach (HeadlessEntityId cardId in cards)
        {
            if (!repository.TryGetInstance(cardId, out CardInstanceRecord? card) || card is null)
            {
                continue;
            }

            // (C-Del 3c-2b / Material Save) an ALREADY-OFF-ZONE card (a digivolution source detached from a dying
            // permanent, moving permanent→permanent) needs no zone move — AS-IS RemoveFromAllArea is a no-op for
            // it; AppendSources below performs the attach. A None→None ZoneMoveRequest is rejected by its ctor.
            if (fromZone != Headless.Choices.ChoiceZone.None)
            {
                await zoneMover.MoveAsync(
                    new ZoneMoveRequest(card.OwnerId, cardId, fromZone, Headless.Choices.ChoiceZone.None),
                    cancellationToken).ConfigureAwait(false);
            }

            appended.Add(cardId.Value);
            // (R6-Da'-6 D3) AS-IS CardSource.Init() per-card cap reset on the CEntity_EffectController store
            // (the OnceFlags string-key twin died with the uniform corpus).
            if (context is not null)
            {
                CEntity_EffectControllerStore.ResetUseCountForCard(context, cardId);
            }
        }

        AppendSources(repository, target, appended);
        // AS-IS AddDigivolutionCardsBottom (Permanent.cs:1203-1223) fires OnAddDigivolutionCards when an EFFECT
        // places >=1 card under a permanent, EXCEPT when skipEffectAndActivateSkill.
        await EmitAddDigivolutionCardsAsync(
            context, gameEventQueue, target.OwnerId, targetId, appended, causeSourceId,
            skipEffectAndActivateSkill, isFromSame, isFromDigimon).ConfigureAwait(false);
    }

    /// <summary>(AS-IS <c>AddDigivolutionCardsTop</c>, Permanent.cs:1064-1123) Moves <paramref name="cards"/> from
    /// <paramref name="fromZone"/> off-field and inserts them (in order) at the TOP of
    /// <paramref name="targetId"/>'s digivolution stack (just below the top card). The sourceIds list is
    /// top→bottom, so "add to top" prepends.</summary>
    internal static async Task AddSourcesTopAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId targetId,
        IReadOnlyList<HeadlessEntityId> cards,
        Headless.Choices.ChoiceZone fromZone,
        CancellationToken cancellationToken = default,
        EngineContext? context = null,
        GameEventQueue? gameEventQueue = null,
        HeadlessEntityId causeSourceId = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        ArgumentNullException.ThrowIfNull(cards);

        if (cards.Count == 0 || !repository.TryGetInstance(targetId, out CardInstanceRecord? target) || target is null)
        {
            return;
        }

        // (C1d RDW-02) compute the AS-IS from-flags BEFORE the cards move.
        (bool isFromSame, bool isFromDigimon) = ComputeAddDigivolutionFromFlags(repository, zoneMover, target, cards);
        var moved = new List<string>();
        foreach (HeadlessEntityId cardId in cards)
        {
            if (!repository.TryGetInstance(cardId, out CardInstanceRecord? card) || card is null)
            {
                continue;
            }

            // (C-Del 3c-2b / MIG4-DISCARDEVOROOTS-PUTTOTRASH) an ALREADY-OFF-ZONE card (a digivolution source
            // detached to None — e.g. the jogress collapse's DiscardEvoRoots(putToTrash:false) roots re-parented
            // onto the fused permanent) needs no zone move; PrependSources below performs the attach. A None→None
            // ZoneMoveRequest is rejected by its ctor — identical guard to AddSourcesBottomAsync.
            if (fromZone != Headless.Choices.ChoiceZone.None)
            {
                await zoneMover.MoveAsync(
                    new ZoneMoveRequest(card.OwnerId, cardId, fromZone, Headless.Choices.ChoiceZone.None),
                    cancellationToken).ConfigureAwait(false);
            }

            moved.Add(cardId.Value);
            // (B-3 tuck reset) same AS-IS InitUseCountThisTurn mirror as AddSourcesBottomAsync.
            if (context is not null)
            {
                CEntity_EffectControllerStore.ResetUseCountForCard(context, cardId);
            }
        }

        PrependSources(repository, target, moved);
        // AS-IS AddDigivolutionCardsTop (Permanent.cs:1064-1119) has NO skip flag — an effect placing >=1 card on
        // top always fires OnAddDigivolutionCards.
        await EmitAddDigivolutionCardsAsync(
            context, gameEventQueue, target.OwnerId, targetId, moved, causeSourceId,
            skip: false, isFromSame, isFromDigimon).ConfigureAwait(false);
    }

    /// <summary>(C-23 Material Save / MindLink) Moves the first <paramref name="count"/> digivolution sources of
    /// <paramref name="fromId"/> to the bottom of <paramref name="toId"/>'s stack (pure re-parent — the source
    /// cards already live off-field). Returns true when at least one source moved. This is the AS-IS
    /// <c>cardSources</c> re-parent tail only: it is NOT an AS-IS <c>AddDigivolutionCardsBottom</c> call, so it
    /// opens NO OnAddDigivolutionCards window (the AS-IS caller's own AddDigivolutionCardsBottom already did).</summary>
    internal static bool MoveSourcesBottom(
        ICardInstanceRepository repository,
        HeadlessEntityId fromId,
        HeadlessEntityId toId,
        int count,
        EngineContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (count < 1 ||
            fromId == toId ||
            !repository.TryGetInstance(fromId, out CardInstanceRecord? source) || source is null ||
            !repository.TryGetInstance(toId, out CardInstanceRecord? destination) || destination is null)
        {
            return false;
        }

        List<string> fromSources = ReadSourceIdsFrom(source.Metadata).Select(id => id.Value).ToList();
        if (fromSources.Count == 0)
        {
            return false;
        }

        List<string> moved = fromSources.Take(Math.Min(count, fromSources.Count)).ToList();
        List<string> remaining = fromSources.Skip(moved.Count).ToList();

        repository.Upsert(source with { Metadata = WithSources(source.Metadata, remaining) });
        AppendSources(repository, destination, moved);
        if (context is not null)
        {
            foreach (string movedValue in moved)
            {
                CEntity_EffectControllerStore.ResetUseCountForCard(context, new HeadlessEntityId(movedValue));
            }
        }

        return true;
    }

    /// <summary>(B-10) Trash <paramref name="count"/> of <paramref name="hostId"/>'s digivolution sources
    /// (from the bottom/DigiEgg end by default) — move them off-field to the trash and drop them from the
    /// host's stack. Returns the number trashed.</summary>
    /// <param name="honorProtection">(C-3) AS-IS effect-trash (<c>ITrashDigivolutionCards</c>) filters
    /// <c>CanNotTrashFromDigivolutionCards</c>-protected sources OUT of the window; the DELETION path
    /// (<c>DiscardEvoRoots</c>) does NOT (no keyword check) and passes false.</param>
    internal static Task<int> TrashSourcesAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        int count,
        bool fromBottom = true,
        CancellationToken cancellationToken = default,
        GameEventQueue? gameEventQueue = null,
        bool honorProtection = true,
        EngineContext? context = null,
        HeadlessEntityId causingEffectSourceId = default) =>
        RemoveSourcesAsync(repository, zoneMover, hostId, count, fromBottom, Headless.Choices.ChoiceZone.Trash, cancellationToken, gameEventQueue, honorProtection, context, causingEffectSourceId);

    /// <summary>(W6 process) Trash SPECIFIC digivolution sources of <paramref name="hostId"/> (AS-IS
    /// <c>ITrashDigivolutionCards(permanent, selectedCards, …)</c>). Returns the count trashed.</summary>
    internal static async Task<int> TrashSpecificSourcesAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        IReadOnlyList<HeadlessEntityId> cardIds,
        CancellationToken cancellationToken = default,
        GameEventQueue? gameEventQueue = null,
        EngineContext? context = null,
        HeadlessEntityId causingEffectSourceId = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        ArgumentNullException.ThrowIfNull(cardIds);
        if (!repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null ||
            !host.Metadata.TryGetValue(SourceIdsKey, out object? raw) || raw is not IEnumerable<string> existing)
        {
            return 0;
        }

        List<string> sources = existing.ToList();
        // First determine which requested cards are real sources (and their owners) WITHOUT moving them yet, so
        // the OnDigivolutionCardDiscarded trigger can fire BEFORE the cards physically leave (AS-IS ordering).
        var discarded = new List<(HeadlessEntityId Id, HeadlessPlayerId Owner)>();
        foreach (HeadlessEntityId cardId in cardIds)
        {
            // (C-3) AS-IS ITrashDigivolutionCards.TrashDigivolutionCards() re-filters CanNotTrashFromDigivolutionCards-
            // protected sources (CardController.cs:5158-5160) — a defensive second filter over the already-narrowed
            // DigiBurst candidate pool. This is an EFFECT-trash path (no deletion caller), so protection is honoured.
            if (IsSourceTrashProtected(repository, cardId.Value, context, causingEffectSourceId))
            {
                continue;
            }

            if (!sources.Remove(cardId.Value))
            {
                continue;
            }

            HeadlessPlayerId owner = repository.TryGetInstance(cardId, out CardInstanceRecord? card) && card is not null
                ? card.OwnerId
                : host.OwnerId;
            discarded.Add((cardId, owner));
        }

        if (discarded.Count == 0)
        {
            return 0;
        }

        repository.TryGetInstance(hostId, out CardInstanceRecord? refreshed);
        repository.Upsert(refreshed! with
        {
            Metadata = new Dictionary<string, object?>(refreshed!.Metadata, StringComparer.Ordinal) { [SourceIdsKey] = sources.ToArray() }
        });

        // (C-3 재상환 P2-1) AS-IS ITrashDigivolutionCards runs `new AceOverflowClass(_trashTargetCards).Overflow()`
        // IMMEDIATELY BEFORE the removal loop (CardController.cs:5219) — an un-flipped ACE source leaving by an
        // effect-trash costs its owner the printed Overflow memory.
        await ApplyEffectTrashAceOverflow(
            repository, zoneMover, context, hostId, host.OwnerId, discarded.Select(d => d.Id).ToArray(), cancellationToken).ConfigureAwait(false);

        foreach ((HeadlessEntityId id, HeadlessPlayerId owner) in discarded)
        {
            // (MIG3 review P2-2) AS-IS ITrashDigivolutionCards skips AddTrashCard for a TOKEN source
            // (CardController.cs:5227-5230 `if (!cardSource.IsToken)`) — a trashed token vanishes instead of
            // becoming a real trash card. Same isToken handling as ArmorPurgeTopAsync.
            bool isToken = repository.TryGetInstance(id, out CardInstanceRecord? tokenRecord) && tokenRecord is not null
                && tokenRecord.Metadata.TryGetValue("isToken", out object? tokenRaw) && tokenRaw is true;
            if (isToken)
            {
                continue; // already off-stack (ChoiceZone.None) — vanishes, no trash move.
            }

            await zoneMover.MoveAsync(
                new ZoneMoveRequest(owner, id, Headless.Choices.ChoiceZone.None, Headless.Choices.ChoiceZone.Trash), cancellationToken).ConfigureAwait(false);
        }

        return discarded.Count;
    }

    /// <summary>(G10-007) Remove a SPECIFIC digivolution source from <paramref name="hostId"/> and move it to
    /// <paramref name="destination"/> (e.g. the battle area, to play it as another Digimon) — the AS-IS
    /// <c>Permanent.RemoveCardSource</c> (Permanent.cs:1297) list drop plus the physical move. Returns true if
    /// the source belonged to the host and was moved.</summary>
    internal static async Task<bool> PlaySpecificSourceAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        HeadlessEntityId sourceId,
        Headless.Choices.ChoiceZone destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        if (sourceId.IsEmpty || !repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
        {
            return false;
        }

        List<string> sources = ReadSourceIdsFrom(host.Metadata).Select(id => id.Value).ToList();
        if (!sources.Remove(sourceId.Value))
        {
            return false;
        }

        repository.Upsert(host with { Metadata = WithSources(host.Metadata, sources) });
        HeadlessPlayerId owner = repository.TryGetInstance(sourceId, out CardInstanceRecord? src) && src is not null
            ? src.OwnerId
            : host.OwnerId;
        // (BT22_035) destination == None ⇒ DETACH-only: the source is already off-field (ChoiceZone.None as a
        // digivolution source); removing it from sourceIds leaves it off-field for an immediate re-attach (e.g.
        // as a LINK card). A None → None ZoneMoveRequest is rejected (both zones abstract), so skip the move.
        if (destination != Headless.Choices.ChoiceZone.None)
        {
            await zoneMover.MoveAsync(new ZoneMoveRequest(owner, sourceId, Headless.Choices.ChoiceZone.None, destination), cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// (B1) The Armor Purge / de-digivolve top-trash: trash ONLY the top card and promote the immediate
    /// under-source — the permanent survives (AS-IS <c>ArmorPurgeClass.ArmorPurge</c>, KeyWordEffects/ArmorPurge.cs:
    /// 40-99, whose physical half is <c>CardObjectController.RemoveFromAllArea</c> + <c>AddTrashCard</c> and whose
    /// "promote" is implicit in the AS-IS live object — <c>cardSources[0]</c> becomes the top). Unlike a full
    /// de-digivolve there is NO rookie floor and NO de-digivolve immunity here (those guards belong to
    /// <c>IDegeneration</c> alone), and a TOKEN top is removed without being trashed (AS-IS ArmorPurge.cs:54-57).
    /// The AS-IS <c>[When Top Card is Trashed]</c> window is opened by the CALLER at its own AS-IS position
    /// (IDegeneration / IMassDegeneration / ITrashStack batch it; ArmorPurgeProcess fires it per purge), so this
    /// primitive stays emit-free. Returns false when there is no under-source to promote.
    /// </summary>
    internal static async Task<bool> ArmorPurgeTopAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId cardId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        if (!repository.TryGetInstance(cardId, out CardInstanceRecord? top) || top is null)
        {
            return false;
        }

        IReadOnlyList<HeadlessEntityId> sources = ReadSourceIdsFrom(top.Metadata);
        if (sources.Count == 0 ||
            !repository.TryGetInstance(sources[0], out CardInstanceRecord? promoted) || promoted is null)
        {
            return false;
        }

        // The trashed top is NOT a deleted permanent (the deletion was replaced) — strip the deletion
        // markers so no OnDeletion/POST window opens for it, then move it out.
        var topMetadata = new Dictionary<string, object?>(top.Metadata, StringComparer.Ordinal);
        topMetadata.Remove(PendingDeletionMetadataKey);
        topMetadata.Remove(DeletedByBattleMetadataKey);
        topMetadata.Remove(DeletedByEffectMetadataKey);
        topMetadata.Remove(DeletedByOwnEffectMetadataKey);
        bool isToken = ReadFlagFrom(top.Metadata, "isToken");
        repository.Upsert(top with { Metadata = topMetadata });

        // (RD-R3-02) both halves of the top swap carry the continuity marker: the AS-IS Permanent object
        // PERSISTS across the promote, so the zone-mover lifetime chokepoint must not Reset either card's
        // bookkeeping — the ReKey below carries it to the new top.
        await zoneMover.MoveAsync(
            new ZoneMoveRequest(top.OwnerId, cardId, Headless.Choices.ChoiceZone.BattleArea, isToken ? Headless.Choices.ChoiceZone.None : Headless.Choices.ChoiceZone.Trash,
                Metadata: HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.ContinuityMoveMetadata),
            cancellationToken).ConfigureAwait(false);
        await zoneMover.MoveAsync(
            new ZoneMoveRequest(promoted.OwnerId, sources[0], Headless.Choices.ChoiceZone.None, Headless.Choices.ChoiceZone.BattleArea, FaceUp: true,
                Metadata: HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.ContinuityMoveMetadata),
            cancellationToken).ConfigureAwait(false);

        var metadata = new Dictionary<string, object?>(promoted.Metadata, StringComparer.Ordinal);
        string[] remaining = sources.Skip(1).Select(id => id.Value).ToArray();
        if (remaining.Length > 0)
        {
            metadata[SourceIdsKey] = remaining;
        }
        else
        {
            metadata.Remove(SourceIdsKey);
        }

        // The permanent persists: carry tap state to the new top (AS-IS SetChangedLocationTime — continuous
        // effects re-derive from the new top), no deletion markers.
        metadata[IsSuspendedKey] = ReadFlagFrom(top.Metadata, IsSuspendedKey);
        metadata.Remove(DeletedByBattleMetadataKey);
        metadata.Remove(DeletedByEffectMetadataKey);
        repository.Upsert(promoted with { Metadata = metadata });
        // (R4 S3b-2②) same persistence for the just-after bookkeeping store (the AS-IS object survives).
        HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.ReKey(repository, cardId, sources[0]);

        return true;
    }

    private static async Task<int> RemoveSourcesAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        int count,
        bool fromBottom,
        Headless.Choices.ChoiceZone destination,
        CancellationToken cancellationToken,
        GameEventQueue? gameEventQueue = null,
        bool honorProtection = true,
        EngineContext? context = null,
        HeadlessEntityId causingEffectSourceId = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        if (count < 1 || !repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
        {
            return 0;
        }

        List<string> sources = ReadSourceIdsFrom(host.Metadata).Select(id => id.Value).ToList();
        if (sources.Count == 0)
        {
            return 0;
        }

        int take = Math.Min(count, sources.Count);
        // Bottom (DigiEgg) is the end of the top→bottom list; top is the start.
        List<string> window = fromBottom
            ? sources.Skip(sources.Count - take).ToList()
            : sources.Take(take).ToList();
        // (fidelity) AS-IS ITrashDigivolutionCards.TrashDigivolutionCards() filters CanNotTrashFromDigivolutionCards-
        // protected sources OUT of the selected window before trashing — the protection applies to TRASH only, NOT
        // return-to-hand/deck. A protected source in the window is skipped and stays in the stack; the trash does
        // NOT reach deeper to make up the count (AS-IS collects the window by position, then filters).
        List<string> removed = destination == Headless.Choices.ChoiceZone.Trash && honorProtection
            ? window.Where(value => !IsSourceTrashProtected(repository, value, context, causingEffectSourceId)).ToList()
            : window;
        List<string> remaining = sources.Where(value => !removed.Contains(value)).ToList();

        // (RD-C5W-ESSTRASHSCAN) AS-IS ITrashDigivolutionCards.TrashDigivolutionCards opens the
        // OnDigivolutionCardDiscarded trigger window INLINE (CardController.cs:5202-5215) with the payload
        // {"CardEffect", _cardEffect}, {"Permanent", permanentTarget_Fixed}, {"DiscardedCards",
        // trashDigivolutionCards_Fixed} — COLLECTED BEFORE the sources leave the host's stack (:5219-5234 physical
        // removal follows). Opened BEFORE the Upsert so Permanent.DigivolutionCards STILL contains the removed
        // sources: the trash-resident ESS (EX8_051) is collected via the host permanent's inherited-effect
        // EffectList scan and its CanTriggerOnTrashSelfDigivolutionCard membership gate passes. Gated on a live
        // context: the DELETION path (Permanent.DiscardEvoRoots) passes context:null and must NOT fire it.
        if (context is not null && gameEventQueue is not null && destination == Headless.Choices.ChoiceZone.Trash && removed.Count > 0)
        {
            using AmbientMatchContext.Scope _discardScope = AmbientMatchContext.Enter(context);
            List<CardSource> discardedCards = removed
                .Select(value => new CardSource(context, new HeadlessEntityId(value), host.OwnerId, host.OwnerId))
                .ToList();
            var discardHashtable = new System.Collections.Hashtable
            {
                { "CardEffect", BareCauseEffect.For(context, causingEffectSourceId) },
                { "Permanent", new Permanent(context, hostId, host.OwnerId) },
                { "DiscardedCards", discardedCards },
            };
            await HeadlessDCGO.Engine.Assets.Scripts.Script.AutoProcessing.For(context)
                .StackSkillInfos(discardHashtable, EffectTiming.OnDigivolutionCardDiscarded).ConfigureAwait(false);
        }

        repository.Upsert(host with { Metadata = WithSources(host.Metadata, remaining) });

        // (C-3 재상환 P2-1) AS-IS ITrashDigivolutionCards applies the ACE-Overflow pass to the trash targets just
        // before moving them (CardController.cs:5219) — TRASH destination only (return-to-hand/deck is not this
        // AS-IS path). The deletion path (DiscardEvoRoots) applies its own AceOverflowClass pass and
        // calls in with context:null, so it never double-charges here.
        if (destination == Headless.Choices.ChoiceZone.Trash && removed.Count > 0)
        {
            await ApplyEffectTrashAceOverflow(
                repository, zoneMover, context, hostId, host.OwnerId,
                removed.Select(value => new HeadlessEntityId(value)).ToArray(), cancellationToken).ConfigureAwait(false);
        }

        foreach (string sourceValue in removed)
        {
            var sourceId = new HeadlessEntityId(sourceValue);
            HeadlessPlayerId owner = repository.TryGetInstance(sourceId, out CardInstanceRecord? src) && src is not null
                ? src.OwnerId
                : host.OwnerId;
            await zoneMover.MoveAsync(new ZoneMoveRequest(owner, sourceId, Headless.Choices.ChoiceZone.None, destination), cancellationToken).ConfigureAwait(false);
        }

        return removed.Count;
    }

    // (C-3 재상환 P2-1) AS-IS ITrashDigivolutionCards' `new AceOverflowClass(trashTargets).Overflow()`
    // (CardController.cs:5219): the effect-trash counterpart of the DiscardEvoRoots overflow.
    // (DELETION-SOURCE re-migration) The substrate `DeletionSourceTrash.ApplyAceOverflow` reimplementation is
    // replaced by the AS-IS call itself — the mirror `AceOverflowClass` (CardController.cs:2100) applies the
    // per-card on-field existence filter, the turn-player-first ordering and the MemoryController.Add. The
    // host-on-field guard is kept (for a source-trash the per-source existence test IS "is the host fielded",
    // and it short-circuits the CardSource view construction).
    private static async Task ApplyEffectTrashAceOverflow(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        EngineContext? context,
        HeadlessEntityId hostId,
        HeadlessPlayerId hostOwner,
        IReadOnlyList<HeadlessEntityId> trashTargets,
        CancellationToken cancellationToken = default)
    {
        if (context is null || trashTargets.Count == 0 || zoneMover is not IZoneStateReader zones
            || !(zones.GetCards(hostOwner, Headless.Choices.ChoiceZone.BattleArea).Contains(hostId)
                || zones.GetCards(hostOwner, Headless.Choices.ChoiceZone.BreedingArea).Contains(hostId)))
        {
            return;
        }

        var trashTargetCards = trashTargets
            .Select(id => new CardSource(
                context,
                id,
                repository.TryGetInstance(id, out CardInstanceRecord? source) && source is not null ? source.OwnerId : hostOwner))
            .ToList();

        await new AceOverflowClass(trashTargetCards).Overflow(cancellationToken).ConfigureAwait(false);
    }

    // (C-3, fidelity) AS-IS CardSource.CanNotTrashFromDigivolutionCards = the legacy per-source stamp
    // (willBeRemoveSources-style flag) OR a field-effect SCAN (ICanNotTrashFromDigivolutionCardsEffect). The
    // EFFECT-trash path passes the context/causing-source so BT9_109's conditional continuous protection
    // (X-Antibody sources under a live host) is honoured; the DELETION path passes honorProtection: false and
    // never reaches here, exactly AS-IS DiscardEvoRoots (no keyword check).
    private static bool IsSourceTrashProtected(
        ICardInstanceRepository repository,
        string sourceValue,
        EngineContext? context,
        HeadlessEntityId causingEffectSourceId)
    {
        if (string.IsNullOrEmpty(sourceValue))
        {
            return false;
        }

        var sourceId = new HeadlessEntityId(sourceValue);
        // Stamp (AS-IS in-flight willBeRemoveSources mirror) — honoured even without a live context.
        if (repository.TryGetInstance(sourceId, out CardInstanceRecord? source) && source is not null
            && source.Metadata.TryGetValue(CardEffectCommons.TrashProtectedKey, out object? raw) && raw is true)
        {
            return true;
        }

        if (context is null || causingEffectSourceId.IsEmpty)
        {
            return false;
        }

        // (R3-W3c-4) AS-IS-literal live scan via the R1-e getter CardSource.CanNotTrashFromDigivolutionCards
        // (delegated through CardEffectCommons.IsTrashProtectedSource) over the LIVE EffectList(None).
        return CardEffectCommons.IsTrashProtectedSource(context, causingEffectSourceId, sourceId);
    }

    /// <summary>AS-IS <c>AddDigivolutionCardsTop</c> / <c>AddDigivolutionCardsBottom</c>'s
    /// <c>StackSkillInfos(hashtable, EffectTiming.OnAddDigivolutionCards)</c> (Permanent.cs:1105-1119 /
    /// 1213-1223), ported 1:1: payload <c>{Permanent, CardEffect, CardSources, isFromSameDigimon, isFromDigimon}</c>,
    /// ONE emit for the whole batch, gated on <c>addedCards.Count >= 1</c> (and, on the Bottom arm, on
    /// <c>!skipEffectAndActivateSkill</c>). The AS-IS <c>cardEffect</c> is the mirror's threaded causing-effect
    /// SOURCE id collapsed to a <see cref="BareCauseEffect"/>; a rule-sourced add (AS-IS
    /// <c>AddDigivolutionCardsBottom(card, null)</c>) carries an empty id and therefore a NULL CardEffect, exactly
    /// as AS-IS — the reactors' <c>CanTriggerOnAddDigivolutionCard</c> gate requires a non-null one.
    /// A context-less (bare / unit) caller cannot reach AutoProcessing, so it opens no window.</summary>
    private static async Task EmitAddDigivolutionCardsAsync(
        EngineContext? context,
        GameEventQueue? gameEventQueue,
        HeadlessPlayerId hostOwner,
        HeadlessEntityId hostId,
        IReadOnlyList<string> addedCardIds,
        HeadlessEntityId causeSourceId,
        bool skip,
        bool isFromSameDigimon,
        bool isFromDigimon)
    {
        if (context is null || gameEventQueue is null || addedCardIds.Count == 0 || skip)
        {
            return;
        }

        using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(context);
        List<CardSource> addedCards = addedCardIds
            .Select(value => new CardSource(context, new HeadlessEntityId(value), hostOwner, hostOwner))
            .ToList();
        var hashtable = new System.Collections.Hashtable
        {
            { "Permanent", new Permanent(context, hostId, hostOwner) },
            { "CardEffect", BareCauseEffect.ForOrNull(context, causeSourceId) },
            { "CardSources", addedCards },
            { "isFromSameDigimon", isFromSameDigimon },
            { "isFromDigimon", isFromDigimon },
        };

        await HeadlessDCGO.Engine.Assets.Scripts.Script.AutoProcessing.For(context)
            .StackSkillInfos(hashtable, EffectTiming.OnAddDigivolutionCards).ConfigureAwait(false);
    }

    // (C1d RDW-02) AS-IS AddDigivolutionCardsTop/Bottom (Permanent.cs:1071-1085): isFromSameDigimon == an added
    // card was already in THIS permanent's cardSources; isFromDigimon == an added card exists on the battle area
    // and its permanent has >=1 digivolution sources. Evaluated PRE-move (target + cards still in their prior
    // state). No zone reader (bare re-parent) => isFromDigimon can only be a battle-area top, so it is false.
    private static (bool isFromSame, bool isFromDigimon) ComputeAddDigivolutionFromFlags(
        ICardInstanceRepository repository, IZoneMover? zoneMover, CardInstanceRecord target, IReadOnlyList<HeadlessEntityId> cards)
    {
        bool isFromSame = false;
        bool isFromDigimon = false;
        var hostSources = ReadSourceIdsFrom(target.Metadata).Select(id => id.Value).ToHashSet(StringComparer.Ordinal);
        IZoneStateReader? reader = zoneMover as IZoneStateReader;
        foreach (HeadlessEntityId cardId in cards)
        {
            if (hostSources.Contains(cardId.Value))
            {
                isFromSame = true;
            }

            if (reader is not null
                && repository.TryGetInstance(cardId, out CardInstanceRecord? card) && card is not null
                && reader.GetCards(card.OwnerId, Headless.Choices.ChoiceZone.BattleArea).Contains(cardId)
                && ReadSourceIdsFrom(card.Metadata).Count >= 1)
            {
                isFromDigimon = true;
            }
        }

        return (isFromSame, isFromDigimon);
    }

    private static void AppendSources(ICardInstanceRepository repository, CardInstanceRecord target, IReadOnlyList<string> add)
    {
        if (add.Count == 0)
        {
            return;
        }

        // Re-read the target so an earlier append in the same operation is preserved.
        CardInstanceRecord current = repository.TryGetInstance(target.InstanceId, out CardInstanceRecord? latest) && latest is not null
            ? latest
            : target;
        List<string> sources = ReadSourceIdsFrom(current.Metadata).Select(id => id.Value).ToList();
        sources.AddRange(add);
        repository.Upsert(current with { Metadata = WithSources(current.Metadata, sources) });
    }

    private static void PrependSources(ICardInstanceRepository repository, CardInstanceRecord target, IReadOnlyList<string> add)
    {
        if (add.Count == 0)
        {
            return;
        }

        CardInstanceRecord current = repository.TryGetInstance(target.InstanceId, out CardInstanceRecord? latest) && latest is not null
            ? latest
            : target;
        List<string> sources = ReadSourceIdsFrom(current.Metadata).Select(id => id.Value).ToList();
        sources.InsertRange(0, add);
        repository.Upsert(current with { Metadata = WithSources(current.Metadata, sources) });
    }

    private static Dictionary<string, object?> WithSources(IReadOnlyDictionary<string, object?> metadata, IReadOnlyList<string> sources)
    {
        var copy = new Dictionary<string, object?>(metadata, StringComparer.Ordinal);
        if (sources.Count > 0)
        {
            copy[SourceIdsKey] = sources.ToArray();
        }
        else
        {
            copy.Remove(SourceIdsKey);
        }

        return copy;
    }

    private static IReadOnlyList<HeadlessEntityId> ReadSourceIdsFrom(IReadOnlyDictionary<string, object?> metadata)
    {
        if (!metadata.TryGetValue(SourceIdsKey, out object? raw) || raw is null)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        return raw switch
        {
            IEnumerable<HeadlessEntityId> ids => ids.ToArray(),
            IEnumerable<string> strings => strings
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => new HeadlessEntityId(value))
                .ToArray(),
            string text => text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => new HeadlessEntityId(value))
                .ToArray(),
            _ => Array.Empty<HeadlessEntityId>()
        };
    }

    private static bool ReadFlagFrom(IReadOnlyDictionary<string, object?> metadata, string key, bool defaultValue = false)
    {
        if (!metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return defaultValue;
        }

        return raw is bool value ? value : defaultValue;
    }

    /// <summary>(DIGIVOLVE cluster re-migration) AS-IS <c>Permanent.AddCardSource(cardSource)</c>
    /// (Permanent.cs:1045-1052) as the digivolve orchestrator issues it (CardController.cs:1365-1376
    /// <c>permanent = _targetPermanent; permanent.AddCardSource(card)</c>): the OLD top is inserted at index 0 of
    /// the stack and the digivolving card becomes the new top. The AS-IS Permanent OBJECT persists across the
    /// swap, which the mirror's identity model (permanent id == top instance) expresses as: the new top's
    /// <c>sourceIds</c> = [old top] + old top's sources + the card's own sources, and the just-after bookkeeping
    /// is ReKeyed onto the new top. Re-homed VERBATIM from the retired substrate
    /// <c>Permanent.AttachTargetAsSource</c> (its sole rule content).</summary>
    internal static IReadOnlyList<HeadlessEntityId> AttachTargetAsSource(
        ICardInstanceRepository repository,
        HeadlessEntityId cardId,
        HeadlessEntityId targetCardId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        CardInstanceRecord card = repository.TryGetInstance(cardId, out CardInstanceRecord? currentCard) && currentCard is not null
            ? currentCard
            : throw new InvalidOperationException($"Card instance '{cardId}' was not found.");
        CardInstanceRecord target = repository.TryGetInstance(targetCardId, out CardInstanceRecord? currentTarget) && currentTarget is not null
            ? currentTarget
            : throw new InvalidOperationException($"Target card '{targetCardId}' was not found.");

        HeadlessEntityId[] sourceIds = new[] { targetCardId }
            .Concat(ReadSourceIdsFrom(target.Metadata))
            .Concat(ReadSourceIdsFrom(card.Metadata))
            .Distinct()
            .ToArray();

        // N-1 (summoning sickness): digivolving keeps the SAME permanent in the original (the top card
        // is swapped, EnterFieldTurnCount is not reset), so the evolved Digimon INHERITS the under-card's
        // entered-this-turn status. A Digimon that has been on the field since a prior turn stays able to
        // attack after digivolving; one played this turn remains sick. Jogress/breeding paths are exempt
        // (they never set the flag), matching the original's EnterFieldTurnCount = -1.
        bool inheritedEnteredThisTurn = ReadBoolFrom(target.Metadata, "enteredThisTurn");
        Dictionary<string, object?> metadata = new(card.Metadata, StringComparer.Ordinal)
        {
            [SourceIdsKey] = sourceIds.Select(id => id.Value).ToArray(),
            ["digivolvedFromCardId"] = targetCardId.Value,
            ["digivolvedFromDefinitionId"] = target.DefinitionId.Value,
            ["enteredThisTurn"] = inheritedEnteredThisTurn
        };
        repository.Upsert(card with { Metadata = metadata });
        // (R4 S3b-2②) the AS-IS Permanent OBJECT persists across the top swap — carry its just-after
        // bookkeeping (PlayingEffect / LevelJustAfterPlayed / …) to the new top key.
        HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.ReKey(repository, targetCardId, cardId);
        return sourceIds;
    }

    private static bool ReadBoolFrom(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return false;
        }

        return rawValue switch
        {
            bool value => value,
            string value => bool.TryParse(value, out bool parsed) && parsed,
            _ => false
        };
    }

    // #endregion Digivolution-stack mutations
}

/// <summary>(W3 / P6A-PERMANENT-EFFECTLIST-ADDED) Match-scoped backing for the AS-IS <see cref="Permanent"/> duration
/// buckets (Permanent.cs:1575-1608). AS-IS they are plain settable list fields on the persistent Permanent; the mirror
/// Permanent is a transient view, so the live lists live here keyed by (EngineContext, top-card InstanceId) — the same
/// substrate pattern as <c>CEntity_EffectControllerStore</c> and <c>PlayerEffectListStore</c>. Each permanent's holder
/// is created lazily with empty lists (AS-IS field initialisers).</summary>
internal sealed class PermanentEffectLists
{
    public List<Func<EffectTiming, ICardEffect>> UntilOwnerTurnEndEffects = new();
    public List<Func<EffectTiming, ICardEffect>> UntilOwnerDrawPhaseEffects = new();
    public List<Func<EffectTiming, ICardEffect>> UntilEachTurnEndEffects = new();
    public List<Func<EffectTiming, ICardEffect>> UntilOpponentTurnEndEffects = new();
    public List<Func<EffectTiming, ICardEffect>> UntilEndBattleEffects = new();
    public List<Func<EffectTiming, ICardEffect>> UntilEndAttackEffects = new();
    public List<Func<EffectTiming, ICardEffect>> UntilOwnerTurnStartEffects = new();
    public List<Func<EffectTiming, ICardEffect>> UntilNextUntapEffects = new();
    public List<Func<EffectTiming, ICardEffect>> PermanentEffects = new();
}

internal static class PermanentEffectListStore
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        EngineContext, System.Collections.Concurrent.ConcurrentDictionary<HeadlessEntityId, PermanentEffectLists>> ByContext = new();

    public static PermanentEffectLists Get(EngineContext context, HeadlessEntityId instanceId)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ByContext.GetOrCreateValue(context).GetOrAdd(instanceId, static _ => new PermanentEffectLists());
    }
}

