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

    // ===== (R3-A) DestroyPermanentsClass deletion-snapshot members (AS-IS Permanent.cs public fields the
    // Destroy() sequence marks/records). The five "JustBeforeRemoveField" carriers are backed by the SAME
    // instance-metadata keys the substrate CardLeavePlayCleanup snapshot writes/reads (Runtime.CardLeavePlayCleanup),
    // so the mirror DestroyPermanentsClass writer and every OnDeletion reader agree on one carrier — no invention.
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

    /// <summary>(R3-A) AS-IS <c>Permanent.DPJustBeforeRemoveField</c> (default -1). Backed by the substrate
    /// snapshot key so OnDeletion DP-comparison predicates read the same value the writer stamped.</summary>
    public int DPJustBeforeRemoveField
    {
        get => _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null
            ? CardLeavePlayCleanup.DpJustBeforeRemoveField(record.Metadata) : -1;
        set => WriteJustBeforeKey(CardLeavePlayCleanup.DpJustBeforeRemoveFieldKey, value);
    }

    /// <summary>(R3-A) AS-IS <c>Permanent.LevelJustBeforeRemoveField</c> (default -1; consumers gate on &gt; 0).</summary>
    public int LevelJustBeforeRemoveField
    {
        get => _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null
            ? CardLeavePlayCleanup.LevelJustBeforeRemoveField(record.Metadata) : -1;
        set => WriteJustBeforeKey(CardLeavePlayCleanup.LevelJustBeforeRemoveFieldKey, value);
    }

    /// <summary>(R3-A) AS-IS <c>Permanent.CostJustBeforeRemoveField</c> (default -1).</summary>
    public int CostJustBeforeRemoveField
    {
        get => _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null
            ? CardLeavePlayCleanup.CostJustBeforeRemoveField(record.Metadata) : -1;
        set => WriteJustBeforeKey(CardLeavePlayCleanup.CostJustBeforeRemoveFieldKey, value);
    }

    /// <summary>(R3-A) AS-IS <c>Permanent.CardNamesJustBeforeRemoveField</c> (default empty).</summary>
    public List<string> CardNamesJustBeforeRemoveField
    {
        get => _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null
            ? CardLeavePlayCleanup.CardNamesJustBeforeRemoveField(record.Metadata).ToList() : new List<string>();
        set => WriteJustBeforeKey(CardLeavePlayCleanup.CardNamesJustBeforeRemoveFieldKey, value?.ToArray() ?? Array.Empty<string>());
    }

    /// <summary>(R3-A) AS-IS <c>Permanent.CardTraitsJustBeforeRemoveField</c> (default empty).</summary>
    public List<string> CardTraitsJustBeforeRemoveField
    {
        get => _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null
            ? CardLeavePlayCleanup.CardTraitsJustBeforeRemoveField(record.Metadata).ToList() : new List<string>();
        set => WriteJustBeforeKey(CardLeavePlayCleanup.CardTraitsJustBeforeRemoveFieldKey, value?.ToArray() ?? Array.Empty<string>());
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
    //     mirror (design item RD-P6C1-2) — the established no-capacity convention (BT1_089) omits it.
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
    /// paths (<see cref="Headless.Effects.MatchStateMutationSink.EnteredThisTurnKey"/>) and read verbatim by the
    /// existing attack validator. TRUE == entered the field this turn (AS-IS EnterFieldTurnCount == TurnCount).</summary>
    private bool EnteredThisTurn =>
        _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? e) && e is not null
        && e.Metadata.TryGetValue(Headless.Effects.MatchStateMutationSink.EnteredThisTurnKey, out object? raw) && raw is true;

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
                    [Headless.Effects.MatchStateMutationSink.EnteredThisTurnKey] = value == _context.TurnController.Current.TurnNumber,
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

    /// <summary>(MIG4-DETACH-LIVE-TOP resolved) The RE-ROOT arm of AS-IS <c>AddDigivolutionCardsTop</c>
    /// (Permanent.cs:1064-1122) for the batch that contains THIS permanent's own live top — the App-Fusion fold
    /// <c>SelectAppFusionEffect.AddToSources</c> issues as <c>AddDigivolutionCardsTop({link, TopCard})</c>. It
    /// REPRODUCES the AS-IS list algorithm on a VIRTUAL stack (per-card RemoveFromAllArea + <c>cardSources.Insert
    /// (1, ...)</c>) to derive the final ordered stack, then TRANSLATES that to the mirror identity model: the
    /// final first element becomes the new top; the rest become its ordered digivolution sources (the
    /// <see cref="Headless.Runtime.DigivolveAction.AttachTargetAsSource"/> stack shape — reconstructed on this
    /// dedicated path, WITHOUT touching AttachTargetAsSource itself). The old top physically leaves the field
    /// (→ None, a buried source) and the new top enters it (→ BattleArea, face-up); both moves carry
    /// <see cref="PermanentBookkeepingStore.PermanentContinuityKey"/> so the zone-mover lifetime chokepoint
    /// leaves the PERSISTING permanent's just-after bookkeeping alone, and this op owns the
    /// <see cref="PermanentBookkeepingStore.ReKey"/> (the digivolve / de-digivolve top-swap seat convention). The
    /// new top re-registers its ported effects (<see cref="CardEffectRegistrar.RegisterCard"/>, as
    /// <see cref="AddCardSource"/> does), and the persisting permanent fires ONE OnAddDigivolutionCards for the
    /// whole batch (AS-IS :1119). A batch folding ANOTHER permanent's live top (AS-IS
    /// <c>IPlacePermanentToDigivolutionCards</c>) is a DISTINCT re-parenting primitive and still STOPs in
    /// <see cref="DetachEmbeddedSourceOrLinkAsync"/> (design item MIG4-DETACH-LIVE-TOP, other-host arm).</summary>
    private async Task ReRootAddDigivolutionCardsTopAsync(
        IReadOnlyList<CardSource> addedDigivolutionCards,
        HeadlessEntityId? causeEffectSourceId,
        CancellationToken cancellationToken)
    {
        HeadlessEntityId oldTopId = InstanceId;
        bool hostIsToken = this.IsToken;

        // The AS-IS virtual cardSources list: top at index 0, then the digivolution sources TOP-MOST first
        // (DigivolutionCards is bottom→top, so reverse — same mapping as the cardSources getter, :1866-1879).
        var virtualStack = new List<HeadlessEntityId> { oldTopId };
        IReadOnlyList<CardSource> currentSources = DigivolutionCards;
        for (int i = currentSources.Count - 1; i >= 0; i--)
        {
            virtualStack.Add(currentSources[i].InstanceId);
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
            // here (if it is that other permanent's OWN live top, DetachEmbeddedSourceOrLinkAsync STILL STOPs —
            // decision-2: IPlacePermanentToDigivolutionCards is out of scope). One of THIS permanent's own cards
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
                    Metadata: PermanentBookkeepingStore.ContinuityMoveMetadata),
                cancellationToken).ConfigureAwait(false);

            // New top → BattleArea (from wherever it sits: a de-linked card in None, or a hand card). Continuity
            // + face-up, mirroring the digivolve top swap.
            HeadlessPlayerId newTopOwner = OwnerOfCard(newTopId) ?? OwnerId;
            await _context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(newTopOwner, newTopId, CurrentZoneOf(newTopOwner, newTopId), Headless.Choices.ChoiceZone.BattleArea,
                    FaceUp: true, Metadata: PermanentBookkeepingStore.ContinuityMoveMetadata),
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
            PermanentBookkeepingStore.ReKey(_context.CardInstanceRepository, oldTopId, newTopId);
            CardEffectRegistrar.RegisterCard(_context, newTopId, OwnerId);
        }
        else
        {
            // Degenerate: the top did not actually change (e.g. only the top itself was added and there was no
            // other card to promote to index 0). Just write the sources back onto the same top.
            WriteReRootedStack(newTopId, oldTopId, newSourceIds);
        }

        // AS-IS :1099-1119 — ONE OnAddDigivolutionCards for the whole batch (subject = the persisting permanent,
        // now topped by newTopId). Same window/metadata as the plain place-under path.
        DigivolutionStackHelpers.EmitAddDigivolutionCards(
            _context.GameEventQueue,
            OwnerId,
            newTopId,
            addedCards.Select(id => id.Value).ToArray(),
            causeEffectSourceId ?? default,
            skip: false,
            isFromSameDigimon,
            isFromDigimon);
    }

    /// <summary>(MIG4-DETACH-LIVE-TOP) Write the re-rooted stack onto <paramref name="newTopId"/>: its
    /// <c>sourceIds</c> become <paramref name="newSourceIds"/> (top→bottom, the AttachTargetAsSource shape) and it
    /// INHERITS the persisting permanent's tap / summoning-sickness state from <paramref name="oldTopId"/> — the
    /// AS-IS Permanent object survives the swap (EnterFieldTurnCount / suspend are permanent-level), the same
    /// carry the de-digivolve promote makes (<c>DeDigivolveHelpers</c>). A demoted old top keeps its now-stale
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
            context: _context,
            // (G-Link P2 risk-1) thread the causing effect's source to the WhenLinked window (AS-IS
            // Permanent.AddLinkCard {"CardEffect", cardEffect}); empty when the caller passes no cause.
            causeSourceId: causeEffectSourceId ?? default).ConfigureAwait(false);
    }

    /// <summary>(R4 S3b-2) AS-IS <c>Permanent.StackCards</c> (Permanent.cs:884) — verbatim: every stacked card
    /// EXCEPT the link cards (top + digivolution sources).</summary>
    public List<CardSource> StackCards => cardSources.Filter(cardSource => !LinkedCards.Contains(cardSource));

    // ==== (R4 S3b-2②) AS-IS "just-after" bookkeeping fields (Permanent.cs:3686-3941) — carried by the
    // match-scoped PermanentBookkeepingStore (the mirror Permanent is a per-access view; see the store header
    // for the CREATE/PERSIST/DIE lifetime mapping). Names, defaults and mutability verbatim.

    /// <summary>AS-IS <c>Permanent.PlayingEffect</c> (:3686) — the effect that PLAYED this permanent (null for
    /// a normal main-phase play).</summary>
    public ICardEffect? PlayingEffect
    {
        get => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).PlayingEffect;
        set => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).PlayingEffect = value;
    }

    /// <summary>AS-IS <c>Permanent.DigivolvingEffect</c> (:3690) — the effect that DIGIVOLVED this permanent
    /// (null for a normal digivolve; read by <c>CardEffectCommons.IsDigivolvedByTheEffect</c>).</summary>
    public ICardEffect? DigivolvingEffect
    {
        get => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).DigivolvingEffect;
        set => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).DigivolvingEffect = value;
    }

    /// <summary>(RD-EXT3-05) AS-IS <c>Permanent.PlaceOtherPermanentEffect</c> (:3674) — the effect that
    /// re-parented this permanent under another (recorded by <c>IPlacePermanentToDigivolutionCards</c> before the
    /// source leaves the field). NOTE the view-model DIE semantics: the source permanent is Reset at the zone
    /// chokepoint the instant it leaves the field, so a read after re-parent returns the AS-IS field default —
    /// consistent with there being no ported consumer that reads a dead permanent's PlaceOtherPermanentEffect.</summary>
    public ICardEffect? PlaceOtherPermanentEffect
    {
        get => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).PlaceOtherPermanentEffect;
        set => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).PlaceOtherPermanentEffect = value;
    }

    /// <summary>AS-IS <c>Permanent.LevelJustAfterPlayed</c> (:3890, −1 = never played).</summary>
    public int LevelJustAfterPlayed
    {
        get => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).LevelJustAfterPlayed;
        set => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).LevelJustAfterPlayed = value;
    }

    /// <summary>AS-IS <c>Permanent.PlayCostJustAfterPlayed</c> (:3894, −1 = never played / no play cost).</summary>
    public int PlayCostJustAfterPlayed
    {
        get => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).PlayCostJustAfterPlayed;
        set => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).PlayCostJustAfterPlayed = value;
    }

    /// <summary>AS-IS <c>Permanent.CardNamesJustAfterPlayed</c> (:3898).</summary>
    public List<string> CardNamesJustAfterPlayed
    {
        get => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).CardNamesJustAfterPlayed;
        set => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).CardNamesJustAfterPlayed = value;
    }

    /// <summary>AS-IS <c>Permanent.CardNamesJustAfterDigivolved</c> (:3902).</summary>
    public List<string> CardNamesJustAfterDigivolved
    {
        get => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).CardNamesJustAfterDigivolved;
        set => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).CardNamesJustAfterDigivolved = value;
    }

    /// <summary>AS-IS <c>Permanent.TraitsJustAfterPlayed</c> (:3906).</summary>
    public List<string> TraitsJustAfterPlayed
    {
        get => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).TraitsJustAfterPlayed;
        set => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).TraitsJustAfterPlayed = value;
    }

    /// <summary>AS-IS <c>Permanent.IsBurstDigivolved</c> (:3938).</summary>
    public bool IsBurstDigivolved
    {
        get => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).IsBurstDigivolved;
        set => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).IsBurstDigivolved = value;
    }

    /// <summary>AS-IS <c>Permanent.IsAppFusion</c> (Permanent.cs, sibling of IsBurstDigivolved).</summary>
    public bool IsAppFusion
    {
        get => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).IsAppFusion;
        set => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).IsAppFusion = value;
    }

    /// <summary>(RD-R5-02) AS-IS <c>Permanent.IsReturnedToHandByBurstDigivolution</c> (Permanent.cs:3930) — the
    /// burst-bounce marker HandBounceClaass.Bounce (:2789) stamps and <see cref="SelectBurstDigivolutionEffect"/>
    /// .BounceTamer reads back to gate TamerBounced. Backed by the bookkeeping store (sibling of IsBurstDigivolved).</summary>
    public bool IsReturnedToHandByBurstDigivolution
    {
        get => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).IsReturnedToHandByBurstDigivolution;
        set => PermanentBookkeepingStore.Get(_context.CardInstanceRepository, InstanceId).IsReturnedToHandByBurstDigivolution = value;
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
    /// <item>the stack threads via the verified <see cref="Headless.Runtime.DigivolveAction.AttachTargetAsSource"/>
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
                Metadata: PermanentBookkeepingStore.ContinuityMoveMetadata),
            cancellationToken).ConfigureAwait(false);

        Headless.Choices.ChoiceZone cardFrom = CurrentZoneOf(cardSource.Owner, cardSource.InstanceId);
        await _context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(controller, cardSource.InstanceId, cardFrom, fieldZone,
                Metadata: PermanentBookkeepingStore.ContinuityMoveMetadata),
            cancellationToken).ConfigureAwait(false);

        Headless.Runtime.DigivolveAction.AttachTargetAsSource(
            _context.CardInstanceRepository,
            cardSource.InstanceId,
            oldTopId);

        CardEffectRegistrar.RegisterCard(_context, cardSource.InstanceId, controller);
        return new Permanent(_context, cardSource.InstanceId, controller);
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
    /// demote) when the added card is currently some permanent's battling top — an identity-corrupting edge for the
    /// headless model (permanent id == top identity). The one shape that folds the HOST's OWN live top into its own
    /// stack (App-Fusion: <c>AddDigivolutionCardsTop({link, TopCard})</c>) is now RESOLVED — it is handled as a top
    /// swap by <see cref="ReRootAddDigivolutionCardsTopAsync"/> and never reaches this pre-step. What STILL STOPs
    /// here (design item MIG4-DETACH-LIVE-TOP, remaining arms) is folding ANOTHER permanent's live top under this
    /// one (AS-IS <c>IPlacePermanentToDigivolutionCards</c> — its own re-parenting primitive) and the
    /// AddDigivolutionCardsBottom / AddLinkCard self-top paths (AS-IS RemoveDigivolveRootEffect, out of scope).</summary>
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
                "re-parents/demotes another permanent's top under this one (AS-IS IPlacePermanentToDigivolutionCards " +
                "/ RemoveDigivolveRootEffect); the host's OWN live top fold is handled by the AddDigivolutionCardsTop " +
                "re-root arm — design item MIG4-DETACH-LIVE-TOP (remaining arms).");
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

