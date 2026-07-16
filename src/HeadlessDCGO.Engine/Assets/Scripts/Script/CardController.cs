// Source: Assets/Scripts/Script/CardController.cs
// Decision: PORT
// Category: BattleLogic
// Priority: HIGH
// Migration: Port core engine source — INCREMENTAL (goal 3 owns the CardController class itself).
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
//
// (MIG2/MIG3) AS-IS CardController.cs is NOT a class — it is a FILE of 36 independent top-level command
// classes (ctor -> optional Set* fluent setters -> one IEnumerator verb), the shared primitive library
// ~600 card-effect files call directly. This mirror grows in AS-IS file order, slice by slice (goal 3):
//   slice 3a (this commit): the leaf window-emitter tier — IDiscardHands/IDiscardHand (:9-112),
//     DrawClass (:1903), IAddTrashCardsFromLibraryTop (:1971), IAddSecurityFromLibrary (:2041),
//     IRecovery (:2085), IDestroySecurity (:4235), IReduceSecurity (:5412), IAddSecurity (:5462),
//     IFlipSecurity (:5516), ITrashDeckCards (:5767), AceOverflowClass (:5827).
//   ITrashLinkCards (:5242) landed with goal 2.
//   slice 3b: the cut-in tier — IDegeneration (:4779), IMassDegeneration (:4945),
//     ITrashDigivolutionCards (:5127), ReturnToLibraryBottomDigivolutionCardsClass (:5352),
//     SuspendPermanentsClass (:5558), IUnsuspendPermanents (:5661), ITrashStack (:5858).
//   Remaining slices: DestroyPermanentsClass -> ISecurityCheck -> IBattle -> play/bounce/place clusters.
//
// EMISSION-OWNERSHIP SEAM (applies to every class here): the headless substrate DERIVES the zone-crossing
// windows (OnDiscardHand / OnDiscardSecurity / OnDiscardLibrary / OnLoseSecurity / OnAddSecurity) from the
// CardMoved events themselves (TriggerTimingMap), batch-collapsed per the batch id stamped on the moves —
// so an AS-IS `StackSkillInfos(hashtable, <zone timing>)` call translates to STAMPING THE BATCH ID on the
// moves, NOT to a second manual emit (which would double-fire; precedent: MatchStateMutationSink's
// OnDiscardSecurity EmitTiming was removed for exactly this). Non-zone-derived windows (OnDraw,
// OnFaceUpSecurityIncreased) keep their manual TriggerEventEmitter.Emit as the sole source.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script
{

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

#region Trash cards from hand

/// <summary>
/// (MIG3-3a) 1:1 mirror of AS-IS <c>IDiscardHands</c>/<c>IDiscardHand</c> (CardController.cs:9-112): discards a
/// batch of hand cards, tracking which actually left the hand and landed in the trash (a card already gone from
/// hand, or that failed to reach the trash, is not counted), then the "[When cards are trashed from hand]"
/// (OnDiscardHand) window opens ONCE for the whole successfully-discarded list (:42-60) — carried by the
/// zone-derived timing + the ONE shared discard batch id stamped on every move (see the file-header seam note).
///
/// Substrate notes: AS-IS <c>ICardEffect cardEffect</c> -> <c>HeadlessEntityId? causeEffectSourceId</c>. AS-IS
/// <c>IDiscardHand</c>'s ctor <c>Hashtable hashtable</c> param is stored but NEVER read in <c>Discard()</c>
/// (:97-111, verified dead) — dropped; the batch id + cause the substrate move needs flow through
/// <c>Discard(...)</c> params instead. DeleteHandCardEffectCoroutine / PlayLog are UI (stripped).
/// </summary>
public class IDiscardHands
{
    public IDiscardHands(List<IDiscardHand> discardHands, HeadlessEntityId? causeEffectSourceId, ICardEffect? cardEffect = null)
    {
        foreach (IDiscardHand discardHand in discardHands)
        {
            this.discardHands.Add(discardHand);
        }

        _causeEffectSourceId = causeEffectSourceId;
        _cardEffect = cardEffect;
    }

    List<IDiscardHand> discardHands { get; set; } = new List<IDiscardHand>();
    readonly HeadlessEntityId? _causeEffectSourceId;
    // (C1b RD-C1-CARDEFFECT-IDTHREAD) AS-IS `ICardEffect cardEffect` re-threaded ALONGSIDE the cause id (the id
    // still stamps the substrate move; the live effect feeds the AS-IS window hashtable). null on rule-process
    // paths exactly like the AS-IS null cardEffect.
    readonly ICardEffect? _cardEffect;
    public bool HasDiscarded { get; set; }

    public async Task DiscardHands(CancellationToken cancellationToken = default)
    {
        if (discardHands.Count == 0)
        {
            return;
        }

        // ONE shared batch id for the whole list = the AS-IS single StackSkillInfos(OnDiscardHand) call (:56);
        // the derived-timing batch collapse fires each reactor once per batch, not per card.
        EngineContext context = discardHands[0].CardSource.Context;
        long discardBatchId = context.NextDiscardBatchId();

        foreach (IDiscardHand discardHand in discardHands)
        {
            await discardHand.Discard(discardBatchId, _causeEffectSourceId, cancellationToken).ConfigureAwait(false);
        }

        List<CardSource> discardedCards = new List<CardSource>();

        foreach (IDiscardHand discardHand in discardHands)
        {
            if (discardHand.discarded)
            {
                discardedCards.Add(discardHand.CardSource);
            }
        }

        if (discardedCards.Count >= 1)
        {
            // AS-IS :42-60 "[When cards are trashed from hand]" — the Hand->Trash moves above already derive
            // OnDiscardHand (batch-collapsed on the shared id); a manual emit here would double-fire.

            // (C1b) AS-IS CardController.cs:42-56 — drained from C2 flip. StackSkillInfos({"DiscardedCards",
            // discardedCards}, {"CardEffect", cardEffect}, OnDiscardHand). Live _cardEffect re-threaded from the
            // in-scope Select* callers (RD-C1-CARDEFFECT-IDTHREAD); carrier zone-derivation stays (main inert).
            await GManager.instance.autoProcessing.StackSkillInfos(
                new System.Collections.Hashtable { { "DiscardedCards", discardedCards }, { "CardEffect", _cardEffect } },
                EffectTiming.OnDiscardHand).ConfigureAwait(false);

            HasDiscarded = true;
        }

        // AS-IS :62-80 add log = UI (stripped).
    }
}

/// <summary>(MIG3-3a) AS-IS <c>IDiscardHand</c> (CardController.cs:84-112) — see <see cref="IDiscardHands"/>.</summary>
public class IDiscardHand
{
    public IDiscardHand(CardSource cardSource)
    {
        CardSource = cardSource;
    }

    public CardSource CardSource { get; }
    public bool discarded { get; private set; }

    public async Task Discard(long? discardBatchId, HeadlessEntityId? causeEffectSourceId, CancellationToken cancellationToken = default)
    {
        EngineContext context = CardSource.Context;
        var zones = (IZoneStateReader)context.ZoneMover;

        // AS-IS :99 `bool oldisHand = cardSource.Owner.HandCards.Contains(cardSource)`.
        bool oldIsHand = zones.GetCards(CardSource.Owner, ChoiceZone.Hand).Contains(CardSource.InstanceId);

        // AS-IS :101 DeleteHandCardEffectCoroutine = UI (stripped).

        // AS-IS :103 CardObjectController.AddTrashCard(cardSource).
        await context.ZoneMover.TrashCardAsync(CardSource.Owner, CardSource.InstanceId, discardBatchId, causeEffectSourceId, cancellationToken: cancellationToken).ConfigureAwait(false);

        // AS-IS :105 `bool isTrash = CardEffectCommons.IsExistOnTrash(cardSource)` — result-verification: only
        // a card that WAS in hand and IS now in the trash counts as discarded.
        bool isTrash = zones.GetCards(CardSource.Owner, ChoiceZone.Trash).Contains(CardSource.InstanceId);

        if (oldIsHand && isTrash)
        {
            discarded = true;
        }
    }
}

#endregion

#region Draw

/// <summary>
/// (MIG3-3a) 1:1 mirror of AS-IS <c>DrawClass</c> (CardController.cs:1903-1965): draws up to <c>drawCount</c>
/// cards from the library top into hand, then opens the "[When draw cards]" (OnDraw) window ONCE for the whole
/// batch — only if at least one card was actually drawn (:1948). AS-IS performs NO deck-out handling here: an
/// empty library is a silent no-op (:1919) and a short library draws what's available (:1923-1933) — deck-out
/// loss lives elsewhere (the phase-draw check), both AS-IS and headless.
///
/// Substrate notes: AS-IS <c>Player</c> -> <c>(EngineContext, HeadlessPlayerId)</c>; <c>ICardEffect</c> ->
/// <c>HeadlessEntityId? causeEffectSourceId</c>. <c>CardObjectController.AddHandCards</c> (:1935) =
/// <c>IZoneMover.DrawAsync</c>. OnDraw is NOT zone-derived (the moves derive OnAddHand, a different window) —
/// the manual emit here is the sole OnDraw source, matching the existing headless OnDraw emitters. PlayLog = UI.
/// </summary>
public class DrawClass
{
    // (C1c RD-C1b-CARDARG) AS-IS shape `new DrawClass(player, drawCount, cardEffect)` (CardController.cs:1903) —
    // the card/keyword/Tfx callers pass the live effect; the substrate cause id is derived from it
    // (EffectSourceCard.InstanceId), exactly as MIG3 computed at the call site. null cardEffect (e.g. the AS-IS
    // `new DrawClass(owner, 1, null)` digivolve-draw) -> null cause, byte-identical.
    public DrawClass(EngineContext context, HeadlessPlayerId playerId, int drawCount, ICardEffect? cardEffect)
        : this(context, playerId, drawCount, cardEffect?.EffectSourceCard?.InstanceId, cardEffect)
    {
    }

    // (C1c) substrate overload: the effect-driven-draw sink (MatchStateMutationSink) holds only a raw cause id and
    // has no ICardEffect — its OnAddHand/OnDraw reactor gate keys off the stamped id, not the live effect. cardEffect
    // stays null there. Retained ALONGSIDE the AS-IS effect ctor because that off-limits consumer needs the id-only
    // shape (so the id param is NOT redundant — cf. the drop for the other C1c routines).
    public DrawClass(EngineContext context, HeadlessPlayerId playerId, int drawCount, HeadlessEntityId? causeEffectSourceId, ICardEffect? cardEffect = null)
    {
        _context = context;
        _playerId = playerId;
        _drawCount = drawCount;
        _cardEffect = cardEffect;
        _causeEffectSourceId = causeEffectSourceId;
    }

    readonly EngineContext _context;
    readonly HeadlessPlayerId _playerId;
    readonly int _drawCount;
    readonly ICardEffect? _cardEffect;
    readonly HeadlessEntityId? _causeEffectSourceId;

    public async Task Draw(CancellationToken cancellationToken = default)
    {
        if (_drawCount <= 0)
        {
            return;
        }

        var zones = (IZoneStateReader)_context.ZoneMover;
        if (zones.GetCards(_playerId, ChoiceZone.Library).Count <= 0)
        {
            return;
        }

        long addHandBatchId = _context.NextAddHandBatchId();

        IReadOnlyList<HeadlessEntityId> drawnCards = await _context.ZoneMover.DrawAsync(
            _playerId, _drawCount, addHandBatchId, _causeEffectSourceId, cancellationToken).ConfigureAwait(false);

        // AS-IS :1937-1944 PlayLog = UI (stripped).

        if (drawnCards.Count >= 1)
        {
            #region "When draw cards" effect

            var hashtable = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["playerId"] = _playerId.Value,
            };
            if (_causeEffectSourceId is { IsEmpty: false } cause)
            {
                hashtable["causeEffectSourceId"] = cause.Value;
            }

            // AS-IS :1960 StackSkillInfos(hashtable, OnDraw) — {Player, CardEffect}.
            TriggerEventEmitter.Emit(_context.GameEventQueue, TriggerTimings.OnDraw, actor: _playerId, extraMetadata: hashtable);

            // (C1c) AS-IS CardController.cs:1948-1960 — drained from C2 flip. StackSkillInfos({"Player", _player},
            // {"CardEffect", _cardEffect}, OnDraw). Live _cardEffect re-threaded from the in-scope card/Tfx callers
            // (RD-C1b-CARDARG); the carrier Emit above stays (main instance undrained -> inert today).
            await GManager.instance.autoProcessing.StackSkillInfos(
                new System.Collections.Hashtable
                {
                    { "Player", new Player(_context, _playerId) },
                    { "CardEffect", _cardEffect },
                },
                EffectTiming.OnDraw).ConfigureAwait(false);

            #endregion
        }
    }
}

#endregion

#region Trash cards from deck top

/// <summary>
/// (MIG3-3a) 1:1 mirror of AS-IS <c>IAddTrashCardsFromLibraryTop</c> (CardController.cs:1971-2035): peeks the
/// top <c>addTrashCount</c> library cards and delegates the move + window to <see cref="ITrashDeckCards"/>
/// (:2020) — literal composition. <c>SetNotShowCards</c> only gated the UI reveal (:2014-2018, stripped) — now
/// inert, kept for method-shape parity. PlaySE/WaitForSeconds/PlayLog = UI (stripped).
/// </summary>
public class IAddTrashCardsFromLibraryTop
{
    public IAddTrashCardsFromLibraryTop(EngineContext context, HeadlessPlayerId playerId, int addTrashCount, ICardEffect? cardEffect)
    {
        _context = context;
        _playerId = playerId;
        _addTrashCount = addTrashCount;
        _cardEffect = cardEffect;
        // (C1c RD-C1b-CARDARG) AS-IS `new IAddTrashCardsFromLibraryTop(count, player, cardEffect)` — carries the
        // live cardEffect down into ITrashDeckCards' window; substrate cause id derived from it (was the MIG3 arg).
        // NOTE: this class has NO live callers today (verified caller-dead, like ReturnToLibraryBottom*) — threaded
        // for AS-IS routine fidelity so any future/Tfx caller lands the live effect.
        _causeEffectSourceId = cardEffect?.EffectSourceCard?.InstanceId;
    }

    public void SetNotShowCards()
    {
        _notShowCards = true;
    }

    readonly EngineContext _context;
    readonly HeadlessPlayerId _playerId;
    readonly int _addTrashCount;
    readonly ICardEffect? _cardEffect;
    readonly HeadlessEntityId? _causeEffectSourceId;
    public List<CardSource> discardedCards = new List<CardSource>();
    bool _notShowCards = false;

    public async Task AddTrashCardsFromLibraryTop(CancellationToken cancellationToken = default)
    {
        if (_addTrashCount <= 0)
        {
            return;
        }

        var zones = (IZoneStateReader)_context.ZoneMover;
        if (zones.GetCards(_playerId, ChoiceZone.Library).Count == 0)
        {
            return;
        }

        IReadOnlyList<HeadlessEntityId> library = zones.GetCards(_playerId, ChoiceZone.Library);

        for (int i = 0; i < _addTrashCount; i++)
        {
            if (library.Count > i)
            {
                discardedCards.Add(new CardSource(_context, library[i], _playerId, _playerId));
            }
        }

        // AS-IS :2014-2018 ShowCardEffect (gated by !_notShowCards) = UI (stripped).

        await new ITrashDeckCards(discardedCards, _cardEffect).TrashDeckCards(cancellationToken).ConfigureAwait(false);

        // AS-IS :2022-2033 PlaySE/WaitForSeconds/PlayLog = UI (stripped).
    }
}

#endregion

#region Security rule gate seam

/// <summary>
/// design item MIG3-CANREDUCESECURITY / MIG3-CANADDSECURITY: stand-ins for AS-IS <c>Player.CanReduceSecurity()</c>
/// (Player.cs:1521-1529 — body is just <c>!IsSecurityLooking</c>) and <c>Player.CanAddSecurity(ICardEffect)</c>
/// (Player.cs:1469-1517 — <c>!IsSecurityLooking</c> PLUS a continuous <c>ICannotAddSecurityEffect</c> restriction
/// scan). Headless has no live IsSecurityLooking reader on EngineContext and no ICannotAddSecurityEffect
/// producer yet (PlayerRuleAdapter's variants are snapshot-based and scan-less). Stubbed true until the
/// producers land; every mirror call site routes through here so the wiring is one edit.
/// </summary>
internal static class SecurityRuleGateSeam
{
    public static bool CanReduceSecurity(EngineContext context, HeadlessPlayerId playerId) => true;

    public static bool CanAddSecurity(EngineContext context, HeadlessPlayerId playerId, HeadlessEntityId? causeEffectSourceId) => true;
}

#endregion

#region Add security from deck top

/// <summary>
/// (MIG3-3a) 1:1 mirror of AS-IS <c>IAddSecurityFromLibrary</c> (CardController.cs:2041-2079): deals up to
/// <c>addSecurityCount</c> library-top cards into security. AS-IS delegates each card to
/// <c>CardObjectController.AddSecurityCard(StockCard, useEffect: i==0)</c> (:2062; CardObjectController.cs:
/// 976-1007): <c>useEffect</c> gates ONLY the CreateRecoveryEffect VFX; <c>new IAddSecurity(card).AddSecurity()</c>
/// runs UNCONDITIONALLY per non-token, non-DigiEgg card, AFTER SetReverse()/SetFace() set the face state.
///
/// Substrate notes: the ->Security moves derive the OnAddSecurity window (per-card batch ids via
/// batchIdFactory — OnAddSecurity is per-card, not collapsed); the mirror <see cref="IAddSecurity"/> call per
/// card carries the face-state stamp + the OnFaceUpSecurityIncreased half (not zone-derived).
/// </summary>
public class IAddSecurityFromLibrary
{
    public IAddSecurityFromLibrary(EngineContext context, HeadlessPlayerId playerId, int addSecurityCount, bool faceUp = false)
    {
        _context = context;
        _playerId = playerId;
        _addSecurityCount = addSecurityCount;
        _faceUp = faceUp;
    }

    readonly EngineContext _context;
    readonly HeadlessPlayerId _playerId;
    readonly int _addSecurityCount;
    // (substrate extension) AS-IS deals face-down (:2062 -> SetReverse); a face-UP recovery effect (the sink's
    // ApplyRecover FaceUpKey) is the AS-IS SetFace branch of AddSecurityCard — threaded, default false.
    readonly bool _faceUp;

    public async Task AddSecurity(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<HeadlessEntityId> added = await _context.ZoneMover.AddSecurityFromLibraryAsync(
            _playerId, _addSecurityCount, faceUp: _faceUp,
            batchIdFactory: () => _context.NextSecurityAddBatchId(),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        foreach (HeadlessEntityId cardId in added)
        {
            // AS-IS AddSecurityCard:988-991 SetReverse()/SetFace() BEFORE constructing IAddSecurity — stamp so
            // IAddSecurity's own face-up read is correct.
            SecurityFaceState.Stamp(_context.CardInstanceRepository, cardId, faceUp: _faceUp);

            var cardSource = new CardSource(_context, cardId, _playerId, _playerId);

            // AS-IS CardObjectController.cs:1004 — IAddSecurity always constructed+called per card;
            // useEffect (i==0) gates only the recovery VFX (UI, stripped).
            await new IAddSecurity(cardSource).AddSecurity(cancellationToken).ConfigureAwait(false);
        }

        // AS-IS :2068-2077 PlayLog = UI (stripped).
    }
}

#endregion

#region Recovery

/// <summary>
/// (MIG3-3a) 1:1 mirror of AS-IS <c>IRecovery</c> (CardController.cs:2085-2108): recovers up to
/// <c>addLifeCount</c> library-top cards into security via <see cref="IAddSecurityFromLibrary"/> (:2104), gated
/// on non-empty library, positive count, and <c>Player.CanAddSecurity</c> (<see cref="SecurityRuleGateSeam"/>).
/// The cause id is used ONLY for the CanAddSecurity gate, exactly as AS-IS. CreateRecoveryEffect = UI.
/// </summary>
public class IRecovery
{
    public IRecovery(EngineContext context, HeadlessPlayerId playerId, int addLifeCount, HeadlessEntityId? causeEffectSourceId)
    {
        _context = context;
        _playerId = playerId;
        _addLifeCount = addLifeCount;
        _causeEffectSourceId = causeEffectSourceId;
    }

    readonly EngineContext _context;
    readonly HeadlessPlayerId _playerId;
    readonly int _addLifeCount;
    readonly HeadlessEntityId? _causeEffectSourceId;

    public async Task Recovery(CancellationToken cancellationToken = default)
    {
        var zones = (IZoneStateReader)_context.ZoneMover;
        if (zones.GetCards(_playerId, ChoiceZone.Library).Count == 0)
        {
            return;
        }

        if (_addLifeCount <= 0)
        {
            return;
        }

        if (!SecurityRuleGateSeam.CanAddSecurity(_context, _playerId, _causeEffectSourceId))
        {
            return;
        }

        await new IAddSecurityFromLibrary(_context, _playerId, _addLifeCount).AddSecurity(cancellationToken).ConfigureAwait(false);

        // AS-IS :2106 CreateRecoveryEffect = UI (stripped).
    }
}

#endregion

#region Digi-Burst

/// <summary>
/// (bridge W5) 1:1 mirror of AS-IS <c>IDigiBurst</c> (CardController.cs:2114-2264): the Digi-Burst carrier a
/// card's [Main] inlines (<c>new IDigiBurst(permanent, N, activateClass)</c>, e.g. ST4_13). AS-IS shape kept:
/// ctor <c>(Permanent, int, ICardEffect)</c>, <c>SetUpToMaxCount</c> ("Digi-Burst up to N"),
/// <c>CanDigiBurst</c> gate (:2135-2160 — host non-null, stack-trash immunity, then Some/Count of trashable
/// sources vs the burst count), <c>DigiBurst</c> (:2162-2263 — the controller SELECTS which sources to discard
/// via the W4-bridged <see cref="SelectCardEffect"/> over the stack (exact AS-IS 16-param SetUp), then the
/// OnUseDigiburst window opens BEFORE the trash (:2228), then <see cref="ITrashDigivolutionCards"/> trashes
/// exactly the selected cards (:2233) and <see cref="discardedCards"/> collects them (:2235; AS-IS adds ALL
/// selected cards whether or not each was actually trashed — quirk kept)).
///
/// Substrate notes: AS-IS <c>ICardEffect</c> KEPT on the ctor (W4 convention); the cause id fed to the
/// gates/carriers is <c>_cardEffect.EffectSourceCard.InstanceId</c>. <c>ImmuneFromStackTrashing(_cardEffect)</c>
/// (:2141) = the same <see cref="RestrictionScan"/> ImmuneStackTrashingKey scan
/// <see cref="ITrashDigivolutionCards"/> applies (self-contained-privates style). AS-IS
/// <c>StackSkillInfos(hashtable {"Permanent", "CardEffect"}, EffectTiming.OnUseDigiburst)</c> (:2218-2228) =
/// the OnUseDigiburst queue emit (actor = the host's controller, subject = the host permanent's top card) —
/// the exact verified emit shape of the resolver's DigiBurstActivatedEffect path
/// (ActivatedEffectResolver.cs:508), JOURNALED like there so a suspended resolution's replay (a later choice in
/// the same body suspending) does not double-emit. Add-log/PlayLog (:2237-2259) = UI (stripped).
/// </summary>
public class IDigiBurst
{
    public IDigiBurst(Permanent permanent, int DigiBurstCount, ICardEffect cardEffect)
    {
        _permanent = permanent;
        _digiBurstCount = DigiBurstCount;
        _cardEffect = cardEffect;
    }

    Permanent _permanent = null!;
    int _digiBurstCount = 0;
    ICardEffect _cardEffect = null!;
    bool _upToMaxCount = false;

    public List<CardSource> discardedCards = new List<CardSource>();

    // AS-IS :2130-2133.
    public void SetUpToMaxCount()
    {
        _upToMaxCount = true;
    }

    // AS-IS :2135-2160.
    public bool CanDigiBurst()
    {
        if (_permanent != null)
        {
            if (_permanent.TopCard != null) // mirror TopCard is never null — kept for 1:1 shape (:2139).
            {
                if (ImmuneFromStackTrashing()) return false; // AS-IS :2141 `_permanent.ImmuneFromStackTrashing(_cardEffect)`.

                if (_upToMaxCount)
                {
                    if (_permanent.DigivolutionCards.Some((cardSource) => !cardSource.CanNotTrashFromDigivolutionCards(_cardEffect)))
                    {
                        return true;
                    }
                }
                else
                {
                    if (_permanent.DigivolutionCards.Count((cardSource) => !cardSource.CanNotTrashFromDigivolutionCards(_cardEffect)) >= _digiBurstCount)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    // AS-IS :2162-2263, IEnumerator -> Task.
    public async Task DigiBurst()
    {
        if (CanDigiBurst())
        {
            discardedCards = new List<CardSource>();

            List<CardSource> selectedCards = new List<CardSource>();

            SelectCardEffect selectCardEffect = GManager.instance!.GetComponent<SelectCardEffect>();

            selectCardEffect.SetUp(
                        canTargetCondition: (cardSource) => !cardSource.CanNotTrashFromDigivolutionCards(_cardEffect),
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: CanEndSelectCondition,
                        canNoSelect: () => false,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        message: "Select digivolution cards to discard.",
                        maxCount: _digiBurstCount,
                        canEndNotMax: _upToMaxCount,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.Custom,
                        customRootCardList: _permanent.DigivolutionCards.ToList(),
                        canLookReverseCard: true,
                        selectPlayer: _permanent.TopCard.Owner,
                        cardEffect: null!);

            selectCardEffect.SetUseFaceDown();

            selectCardEffect.SetUpCustomMessage("Select digivolution cards to discard.", "The opponent is selecting digivolution cards to discard.");

            await selectCardEffect.Activate().ConfigureAwait(false);

            bool CanEndSelectCondition(List<CardSource> cardSources)
            {
                if (CardEffectCommons.CardEffectCommons.HasNoElement(cardSources))
                {
                    return false;
                }

                return true;
            }

            async Task SelectCardCoroutine(CardSource cardSource)
            {
                selectedCards.Add(cardSource);

                await Task.CompletedTask; // AS-IS `yield return null`.
            }

            if (selectedCards.Count >= 1)
            {
                #region "When use Digi-Burst" effect

                // AS-IS :2218-2228 — hashtable {"Permanent": _permanent, "CardEffect": _cardEffect} →
                // StackSkillInfos(hashtable, EffectTiming.OnUseDigiburst): the "[When you use Digi-Burst]"
                // window opens AFTER the select, BEFORE the trash. Mirror = the journaled OnUseDigiburst queue
                // emit (see class doc; verified shape = ActivatedEffectResolver's DigiBurstActivatedEffect).
                EngineContext context = _permanent.TopCard.Context;
                EmitJournaled(context, TriggerTimings.OnUseDigiburst, _permanent.TopCard.Controller, _permanent.InstanceId);

                // (C1) AS-IS CardController.cs:2218-2228 — drained from C2 flip. StackSkillInfos({"Permanent",
                // _permanent}, {"CardEffect", _cardEffect}, OnUseDigiburst). Live _cardEffect is in scope here.
                await GManager.instance.autoProcessing.StackSkillInfos(
                    new System.Collections.Hashtable { { "Permanent", _permanent }, { "CardEffect", _cardEffect } },
                    EffectTiming.OnUseDigiburst).ConfigureAwait(false);

                #endregion

                // trash digivolution cards (AS-IS :2233 `new ITrashDigivolutionCards(_permanent, selectedCards,
                // _cardEffect)`; cause id stamps the substrate move, the live _cardEffect feeds the window hashtable).
                await new ITrashDigivolutionCards(_permanent, selectedCards, CauseEffectSourceId, _cardEffect).TrashDigivolutionCards().ConfigureAwait(false);

                foreach (CardSource cardSource in selectedCards)
                {
                    discardedCards.Add(cardSource);
                }

                // AS-IS :2237-2259 add log (PlayLog) = UI (stripped).
            }
        }
    }

    // The AS-IS `_cardEffect` threaded to the protection/immunity gates — the causing effect's source card id.
    private HeadlessEntityId? CauseEffectSourceId => _cardEffect?.EffectSourceCard?.InstanceId;

    // AS-IS Permanent.ImmuneFromStackTrashing(_cardEffect) (:2141). (R3-W3c B6) rehomed from the
    // ImmuneStackTrashingKey registry scan to the AS-IS-literal live getter — scans every field permanent's
    // EffectList(None) for a usable IImmuneFromStackTrashingEffect honouring the causing effect. A missing
    // causing effect matches no cause-keyed immunity (as in the AS-IS null-cause scan); the guard preserves that.
    private bool ImmuneFromStackTrashing()
    {
        if (CauseEffectSourceId is not { IsEmpty: false })
        {
            return false;
        }

        return _permanent.ImmuneFromStackTrashing(_cardEffect!);
    }

    // (bridge W5) duplicate of ActivatedEffectResolver's private EmitJournaled/RunJournaledImmediate (B-1
    // rework, ActivatedEffectResolver.cs:997): route the immediately-applied queue emit through the
    // uniform-cycle mutation journal so a resumed replay of this already-performed emit is SKIPPED instead of
    // doubled. Outside a cycle this is a plain emit.
    private static void EmitJournaled(EngineContext context, string timing, HeadlessPlayerId actor, HeadlessEntityId subject)
    {
        OnceFlagController.MutationReplay replay = context.OnceFlags.BeginMutationApply();
        if (replay == OnceFlagController.MutationReplay.Skip)
        {
            return;
        }

        TriggerEventEmitter.Emit(context.GameEventQueue, timing, actor: actor, subject: subject);
        if (replay == OnceFlagController.MutationReplay.Fresh)
        {
            context.OnceFlags.RecordFreshMutation(purelyImmediate: true);
        }
    }
}

#endregion

#region Battle (Destroy Security)

/// <summary>
/// (MIG3-3a) 1:1 mirror of AS-IS <c>IDestroySecurity</c> (CardController.cs:4235-4421): trashes up to
/// <c>destroySecurityCount</c> security cards from the top, bottom, or one selected card, stopping early if
/// security empties or <c>Player.CanReduceSecurity()</c> goes false (<see cref="SecurityRuleGateSeam"/>). After
/// the loop: ONE <see cref="IReduceSecurity"/> call (null-collector sentinel = emit-now, :4360-4363) then ONE
/// "[When security cards are trashed]" (OnDiscardSecurity) window (:4369-4377) for the WHOLE batch — both
/// carried by the zone-derived timings on the moves, batch-collapsed on the shared security-loss id stamped
/// below (file-header seam note; the sink's own removed OnDiscardSecurity EmitTiming is the precedent).
///
/// Substrate notes: raw MoveAsync per card (TrashSecurityAsync only supports top/bottom-N, not SelectedCard).
/// ShowBlueMatarial/Break/Enter/DestroySecurityEffect/ShowCardEffect/PlayLog = UI (stripped).
/// </summary>
public class IDestroySecurity
{
    private enum TrashMode
    {
        TopSecurity,
        BottomSecurity,
        SelectedCard,
    }

    public IDestroySecurity(EngineContext context, HeadlessPlayerId playerId, int destroySecurityCount, ICardEffect? cardEffect, bool fromTop)
    {
        _context = context;
        _playerId = playerId;
        _destroySecurityCount = destroySecurityCount;
        _cardEffect = cardEffect;
        // (C1c RD-C1b-CARDARG) AS-IS `new IDestroySecurity(player, count, cardEffect, fromTop)` — live cardEffect
        // carried for the OnDiscardSecurity / delegated OnLoseSecurity windows; substrate cause id derived from it.
        _causeEffectSourceId = cardEffect?.EffectSourceCard?.InstanceId;
        _trashMode = fromTop ? TrashMode.TopSecurity : TrashMode.BottomSecurity;
    }

    public IDestroySecurity(EngineContext context, HeadlessPlayerId playerId, CardSource card, ICardEffect? cardEffect)
    {
        _context = context;
        _playerId = playerId;
        _destroySecurityCount = 1;
        _cardEffect = cardEffect;
        // (C1c RD-C1b-CARDARG) AS-IS `new IDestroySecurity(player, card, cardEffect)`.
        _causeEffectSourceId = cardEffect?.EffectSourceCard?.InstanceId;
        _trashMode = TrashMode.SelectedCard;
        _selectedCard = card;
    }

    readonly EngineContext _context;
    readonly HeadlessPlayerId _playerId;
    readonly int _destroySecurityCount;
    readonly ICardEffect? _cardEffect;
    readonly HeadlessEntityId? _causeEffectSourceId;
    readonly TrashMode _trashMode;
    readonly CardSource? _selectedCard;
    public List<CardSource> DestroyedSecurity { get; } = new();

    public bool IsDestroyed(CardSource cardSource) =>
        DestroyedSecurity.Any(destroyed => destroyed.InstanceId == cardSource.InstanceId);

    public async Task DestroySecurity(CancellationToken cancellationToken = default)
    {
        var zones = (IZoneStateReader)_context.ZoneMover;

        bool StopDestroySecurity()
        {
            if (zones.GetCards(_playerId, ChoiceZone.Security).Count == 0)
            {
                return true;
            }

            if (!SecurityRuleGateSeam.CanReduceSecurity(_context, _playerId))
            {
                return true;
            }

            return false;
        }

        if (zones.GetCards(_playerId, ChoiceZone.Security).Count >= 1 && _destroySecurityCount >= 1)
        {
            int count = 0;
            List<CardSource> discardedCards = new List<CardSource>();
            long securityLossBatchId = _context.NextSecurityLossBatchId();

            while (true)
            {
                if (StopDestroySecurity())
                {
                    break;
                }

                if (count >= _destroySecurityCount)
                {
                    break;
                }

                IReadOnlyList<HeadlessEntityId> security = zones.GetCards(_playerId, ChoiceZone.Security);
                if (security.Count >= 1)
                {
                    count++;

                    CardSource? destroyedSecurityCard = _trashMode switch
                    {
                        TrashMode.TopSecurity => new CardSource(_context, security[0], _playerId, _playerId),
                        TrashMode.BottomSecurity => new CardSource(_context, security[security.Count - 1], _playerId, _playerId),
                        TrashMode.SelectedCard => _selectedCard is { } selected && security.Contains(selected.InstanceId) ? selected : null,
                        _ => null,
                    };

                    if (destroyedSecurityCard is not { } card)
                    {
                        break;
                    }

                    discardedCards.Add(card);

                    // AS-IS :4336-4346 ShowBlueMatarial/Break/Enter/DestroySecurityEffect + waits = UI (stripped).

                    var moveMetadata = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        [MatchStateMutationSink.SecurityLossBatchIdKey] = securityLossBatchId,
                    };
                    if (_causeEffectSourceId is { IsEmpty: false } cause)
                    {
                        moveMetadata[MatchStateMutationSink.DiscardCauseEffectIdKey] = cause.Value;
                    }

                    // AS-IS :4350 CardObjectController.AddTrashCard(destroyedSecurityCard).
                    await _context.ZoneMover.MoveAsync(
                        new ZoneMoveRequest(_playerId, card.InstanceId, ChoiceZone.Security, ChoiceZone.Trash, Metadata: moveMetadata),
                        cancellationToken).ConfigureAwait(false);

                    DestroyedSecurity.Add(card);
                }
            }

            // AS-IS :4356 ShowCardEffect = UI (stripped).

            if (discardedCards.Count >= 1)
            {
                // AS-IS :4360-4363 `new IReduceSecurity(player, ref nullSkillInfos, cardEffect)` — the
                // null-collector sentinel = emit-now mode; the OnLoseSecurity window is carried by the
                // Security->Trash moves above (SecurityLossBatchId collapse).
                await new IReduceSecurity(_context, _playerId, refCollector: null, _cardEffect)
                    .ReduceSecurity(cancellationToken).ConfigureAwait(false);

                // (C1c) AS-IS CardController.cs:4368-4377 — drained from C2 flip. StackSkillInfos({"DiscardedCards",
                // discardedCards}, {"CardEffect", cardEffect}, OnDiscardSecurity). Live _cardEffect re-threaded
                // (RD-C1b-CARDARG); the zone-derived OnDiscardSecurity above stays (main instance undrained -> inert).
                await GManager.instance.autoProcessing.StackSkillInfos(
                    new System.Collections.Hashtable
                    {
                        { "DiscardedCards", discardedCards },
                        { "CardEffect", _cardEffect },
                    },
                    EffectTiming.OnDiscardSecurity).ConfigureAwait(false);
            }

            // AS-IS :4382-4416 add log = UI (stripped).
        }
    }
}

#endregion

#region De-digivolve

/// <summary>
/// (MIG3-3b) 1:1 mirror of AS-IS <c>IDegeneration</c> (CardController.cs:4779-4943): De-Digivolve a Digimon —
/// each step trashes the current top card and promotes the immediate under-source to the new top, stopping at
/// the digivolution-stack floor, the AS-IS rookie/level-3 floor (:4852-4861, <c>Level == 3 &amp;&amp;
/// TopCard.HasLevel</c>), or the requested count. Guards (:4803-4809): mandatory causing effect, host alive,
/// de-digivolve immunity, stack-trash immunity, CanNotBeAffected. Opens ONE "[When Top Card is Trashed]"
/// (WhenTopCardTrashed) window for the whole batch (:4902-4917) — NOT zone-derived, manual emit, fired
/// UNCONDITIONALLY once the count gate was reached (AS-IS :4914-4915 sit OUTSIDE any selectedCards.Count
/// guard — a zero-card batch still opens the window with an empty list; only the stripped add-log is gated).
///
/// Substrate notes: physical per-card mutation (RemoveFromAllArea + AddTrashCard-if-!IsToken, :4887-4895) =
/// <see cref="DeDigivolveHelpers.ArmorPurgeTopAsync"/> — deliberately NOT DeDigivolveAsync, whose embedded
/// immunity/floor re-checks and per-step emit would diverge from / double-fire against this class's own
/// AS-IS-faithful outer guards and single batch emit. CreateDebuffEffect / ShowPermanentData / add-log = UI
/// (stripped). AS-IS SetChangedLocationTime() (:4897) = design item MIG3-LOCATIONTIME (no headless analog).
/// </summary>
public class IDegeneration
{
    public IDegeneration(Permanent permanent, int degenerationCount, HeadlessEntityId? causeEffectSourceId, bool? degenerationCountRuling = null, ICardEffect? cardEffect = null)
    {
        _permanent = permanent;
        _degenerationCount = degenerationCount;
        _causeEffectSourceId = causeEffectSourceId;
        _degenerationCountRuling = degenerationCountRuling;
        _cardEffect = cardEffect;
    }

    Permanent _permanent = null!;
    int _degenerationCount;
    readonly HeadlessEntityId? _causeEffectSourceId;
    readonly bool? _degenerationCountRuling;
    // (C1b RD-C1-CARDEFFECT-IDTHREAD / R3-W3c-2) AS-IS `ICardEffect _cardEffect` re-threaded alongside the cause id
    // so the S2 immunity check reads the AS-IS-literal live scan (TopCard.CanNotBeAffected). null on rule paths.
    readonly ICardEffect? _cardEffect;

    public async Task Degeneration(CancellationToken cancellationToken = default)
    {
        // AS-IS :4803-4804 `_cardEffect == null` / `EffectSourceCard == null` — mandatory causing effect.
        if (_causeEffectSourceId is not { IsEmpty: false }) return;
        if (_permanent == null) return; // AS-IS :4805
        if (_permanent.TopCard == null) return; // AS-IS :4806 — structurally dead, kept for 1:1 shape.

        EngineContext context = _permanent.TopCard.Context;

        if (ImmuneFromDeDigivolve(context, _permanent.InstanceId)) return; // AS-IS :4807

        // AS-IS :4808 ImmuneFromStackTrashing(_cardEffect). (R3-W3c B6) rehomed to the AS-IS-literal live getter
        // (threaded _cardEffect) from the ImmuneStackTrashingKey registry scan.
        if (_permanent.ImmuneFromStackTrashing(_cardEffect!)) return;

        // AS-IS :4809 TopCard.CanNotBeAffected(_cardEffect). (R3-W3c-2) rehomed to the AS-IS-literal live scan
        // (threaded _cardEffect).
        if (_permanent.TopCard.CanNotBeAffected(_cardEffect)) return;

        int maxCount = Math.Min(_permanent.DigivolutionCards.Count, _degenerationCount);

        // AS-IS :4813-4835 SelectCountEffect (owner picks 0..maxCount) when no explicit ruling was passed.
        if (_degenerationCountRuling is null)
        {
            // design item MIG3-DEGEN-COUNTSELECT (LOUD STUB): headless has no SelectCountEffect mirror / choice
            // park point wired here. AS-IS opens an interactive count choice; the AS-IS fallback when that
            // component is ABSENT (:4813 null-check) keeps the CONSTRUCTOR-requested count — mirrored here
            // (review P2-4: not maxCount; the loop's stack/floor stops bound it identically anyway). A real
            // park point (goal-2 link-trim shape) lands with the design item.
            _ = maxCount;
        }

        if (_degenerationCount < 1) return; // AS-IS :4837 `if (_degenerationCount >= 1)` complement.

        int count = 0;
        List<CardSource> selectedCards = new();
        // (MIG3 review P1-1) the AS-IS `_permanent` is a LIVE object whose TopCard is the promoted under-source
        // after each step; the headless Permanent view is pinned to its construction id — walk the CURRENT top
        // id across steps (ArmorPurgeTopAsync promotes sourceIds[0]) so multi-step de-digivolves read the live
        // stack, exactly like the AS-IS live object (and DeDigivolveHelpers.DeDigivolveAsync's own walk).
        HeadlessEntityId currentTopId = _permanent.InstanceId;

        while (true)
        {
            var current = new Permanent(context, currentTopId, _permanent.OwnerId);

            // AS-IS :4845-4869 StopCondition.
            if (current.HasNoDigivolutionCards) break;
            if (current.Level == 3 && current.TopCard.HasLevel) break;
            if (count >= _degenerationCount) break;

            // AS-IS :4878 CreateDebuffEffect (first iteration only) = UI (stripped).

            CardSource cardSource = current.TopCard;
            selectedCards.Add(cardSource);

            HeadlessEntityId? promotedId = NextPromotedSourceId(context, currentTopId);

            // AS-IS :4885 per-card AceOverflow, BEFORE the physical removal.
            await new AceOverflowClass(new List<CardSource> { cardSource }).Overflow(cancellationToken).ConfigureAwait(false);

            // AS-IS :4887-4895 RemoveFromAllArea + AddTrashCard(if !IsToken) — top-trash + promote-under-source,
            // one substrate call (no embedded guards/emits).
            bool promoted = await DeDigivolveHelpers.ArmorPurgeTopAsync(
                context.CardInstanceRepository, context.ZoneMover, cardSource.InstanceId,
                gameEventQueue: null, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!promoted || promotedId is not { } nextTopId) break;
            currentTopId = nextTopId;

            // AS-IS :4897 SetChangedLocationTime() — design item MIG3-LOCATIONTIME.

            count++;
        }

        #region "When Top Card is Trashed" effect

        // AS-IS :4906-4915 {Permanent, CardSources} -> StackSkillInfos(WhenTopCardTrashed) — manual (not
        // zone-derived), UNCONDITIONAL (matches AS-IS scope, see class doc). (MIG3 review P2-3) subject = the
        // SURVIVING top (the AS-IS live Permanent identity at emit time = the promoted card), matching
        // ArmorPurgeTopAsync's own subject convention.
        var extraMetadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["cardSourceIds"] = selectedCards.Select(cs => cs.InstanceId.Value).ToArray(),
        };
        TriggerEventEmitter.Emit(
            context.GameEventQueue, TriggerTimings.WhenTopCardTrashed,
            actor: _permanent.OwnerId, subject: currentTopId, extraMetadata: extraMetadata);

        // (C1) AS-IS CardController.cs:4906-4915 — drained from C2 flip. StackSkillInfos({"Permanent",
        // _permanent}, {"CardSources", selectedCards}, WhenTopCardTrashed). No CardEffect member.
        await GManager.instance.autoProcessing.StackSkillInfos(
            new System.Collections.Hashtable { { "Permanent", _permanent }, { "CardSources", selectedCards } },
            EffectTiming.WhenTopCardTrashed).ConfigureAwait(false);

        #endregion

        // AS-IS :4919-4940 add log (gated on selectedCards.Count >= 1) = UI (stripped).
    }

    // AS-IS Permanent.ImmuneFromDeDigivolve(): the STATIC per-card stamp OR the CONTINUOUS registry scan.
    // Checked ONCE, before the loop (AS-IS position). Duplicated in IMassDegeneration per the
    // self-contained-privates style precedent.
    private static bool ImmuneFromDeDigivolve(EngineContext context, HeadlessEntityId permanentId)
    {
        bool staticImmune = context.CardInstanceRepository.TryGetInstance(permanentId, out CardInstanceRecord? record) && record is not null
            && record.Metadata.TryGetValue(DeDigivolveHelpers.CannotBeDeDigivolvedKey, out object? raw) && raw is true;
        return staticImmune || DeDigivolveHelpers.IsDeDigivolveImmune(context, permanentId);
    }

    /// <summary>(MIG3 review P1-1) The id ArmorPurgeTopAsync will promote — the CURRENT top's sourceIds[0]
    /// (immediate under-source), read BEFORE the purge. Shared by the three top-trash walkers.</summary>
    internal static HeadlessEntityId? NextPromotedSourceId(EngineContext context, HeadlessEntityId topId)
    {
        if (context.CardInstanceRepository.TryGetInstance(topId, out CardInstanceRecord? record) && record is not null)
        {
            IReadOnlyList<HeadlessEntityId> sources = DeletionReplacementGate.ReadSourceIds(record.Metadata);
            if (sources.Count > 0)
            {
                return sources[0];
            }
        }

        return null;
    }
}

/// <summary>
/// (MIG3-3b) 1:1 mirror of AS-IS <c>IMassDegeneration</c> (CardController.cs:4945-5121): De-Digivolve MULTIPLE
/// Digimon simultaneously so blanket immunity is evaluated over the whole target SET atomically (:4948-4949).
/// The joint <c>ValidTarget</c> filter runs ONCE over the WHOLE list BEFORE any mutation (:4973-4982); each
/// survivor then runs its own top-trash loop and opens its OWN per-permanent WhenTopCardTrashed window
/// (:5091-5092 — INSIDE the foreach; unconditional per permanent, same AS-IS quirk as IDegeneration).
/// AS-IS :4984-5008 the per-target count-select choice is COMMENTED OUT in the original — preserved as dead
/// code below, not re-invented.
/// </summary>
public class IMassDegeneration
{
    public IMassDegeneration(List<Permanent> permanents, int degenerationCount, HeadlessEntityId? causeEffectSourceId, bool? degenerationCountRuling = null, ICardEffect? cardEffect = null)
    {
        _permanents = permanents;
        _degenerationCount = degenerationCount;
        _causeEffectSourceId = causeEffectSourceId;
        _degenerationCountRuling = degenerationCountRuling; // ctor parity; unread (dead count-select region).
        _cardEffect = cardEffect;
    }

    readonly List<Permanent> _permanents;
    readonly int _degenerationCount;
    readonly HeadlessEntityId? _causeEffectSourceId;
    // (C1b RD-C1-CARDEFFECT-IDTHREAD / R3-W3c-2) AS-IS live `_cardEffect` threaded for the S2 immunity live scan
    // (TopCard.CanNotBeAffected). No mirror caller yet — AS-IS-shaped for when a de-digivolve card lands.
    readonly ICardEffect? _cardEffect;
#pragma warning disable CS0414 // AS-IS dead field (count-select region commented out) — kept for ctor/field parity.
    readonly bool? _degenerationCountRuling;
#pragma warning restore CS0414

    public async Task Degeneration(CancellationToken cancellationToken = default)
    {
        if (_causeEffectSourceId is not { IsEmpty: false }) return; // AS-IS :4970-4971

        // (substrate-necessitated) headless needs an EngineContext from a list member; behaviorally equivalent
        // to AS-IS's natural no-op on an empty/all-null list.
        Permanent? contextSource = _permanents.FirstOrDefault(p => p != null);
        if (contextSource is null) return;
        EngineContext context = contextSource.TopCard.Context;

        // AS-IS :4973-4982 ValidTarget — the JOINT filter, whole set BEFORE any mutation.
        bool ValidTarget(Permanent permanent) =>
            permanent != null
            && permanent.TopCard != null // structurally dead, kept for 1:1 shape.
            && !ImmuneFromDeDigivolve(context, permanent.InstanceId)
            // (R3-W3c B6) rehomed to the AS-IS-literal live getter Permanent.ImmuneFromStackTrashing(_cardEffect).
            && !permanent.ImmuneFromStackTrashing(_cardEffect!)
            // (R3-W3c-2) rehomed to the AS-IS-literal live scan (threaded _cardEffect).
            && !permanent.TopCard.CanNotBeAffected(_cardEffect);

        List<Permanent> permanentsFixed = _permanents.Where(ValidTarget).ToList();

        // AS-IS :4984-5008 (dead code, preserved verbatim in shape — count-select UI disabled in the original):
        //     SelectCountEffect selectCountEffect = GManager.instance.GetComponent<SelectCountEffect>();
        //     if (selectCountEffect != null && _degenerationCountRuling == null && _degenerationCount > 1)
        //     {
        //         ... SetUp(SelectPlayer, targetPermanent, MaxCount, CanNoSelect:false,
        //             "How many cards do you trash?", SelectCountCoroutine) ... Activate() ...
        //     }

        if (_degenerationCount < 1) return; // AS-IS :5010 complement.

        foreach (Permanent permanent in permanentsFixed)
        {
            int count = 0;
            int maxCount = Math.Min(_degenerationCount, permanent.DigivolutionCards.Count);
            List<CardSource> selectedCards = new();
            // (MIG3 review P1-1) walk the live top id across steps — see IDegeneration.
            HeadlessEntityId currentTopId = permanent.InstanceId;

            while (true)
            {
                var current = new Permanent(context, currentTopId, permanent.OwnerId);

                if (current.HasNoDigivolutionCards) break;
                if (current.Level == 3 && current.TopCard.HasLevel) break;
                if (count >= maxCount) break;

                CardSource cardSource = current.TopCard;
                selectedCards.Add(cardSource);

                HeadlessEntityId? promotedId = IDegeneration.NextPromotedSourceId(context, currentTopId);

                await new AceOverflowClass(new List<CardSource> { cardSource }).Overflow(cancellationToken).ConfigureAwait(false);

                bool promoted = await DeDigivolveHelpers.ArmorPurgeTopAsync(
                    context.CardInstanceRepository, context.ZoneMover, cardSource.InstanceId,
                    gameEventQueue: null, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!promoted || promotedId is not { } nextTopId) break;
                currentTopId = nextTopId;

                // AS-IS :5074 SetChangedLocationTime() — design item MIG3-LOCATIONTIME.

                count++;
            }

            #region "When Top Card is Trashed" effect (per permanent)

            // (MIG3 review P2-3) subject = the surviving top after the walk.
            var extraMetadata = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["cardSourceIds"] = selectedCards.Select(cs => cs.InstanceId.Value).ToArray(),
            };
            TriggerEventEmitter.Emit(
                context.GameEventQueue, TriggerTimings.WhenTopCardTrashed,
                actor: permanent.OwnerId, subject: currentTopId, extraMetadata: extraMetadata);

            // (C1) AS-IS CardController.cs:5083-5092 — drained from C2 flip. StackSkillInfos({"Permanent",
            // permanent}, {"CardSources", selectedCards}, WhenTopCardTrashed). No CardEffect member.
            await GManager.instance.autoProcessing.StackSkillInfos(
                new System.Collections.Hashtable { { "Permanent", permanent }, { "CardSources", selectedCards } },
                EffectTiming.WhenTopCardTrashed).ConfigureAwait(false);

            #endregion

            // AS-IS :5096-5117 add log = UI (stripped).
        }
    }

    private static bool ImmuneFromDeDigivolve(EngineContext context, HeadlessEntityId permanentId)
    {
        bool staticImmune = context.CardInstanceRepository.TryGetInstance(permanentId, out CardInstanceRecord? record) && record is not null
            && record.Metadata.TryGetValue(DeDigivolveHelpers.CannotBeDeDigivolvedKey, out object? raw) && raw is true;
        return staticImmune || DeDigivolveHelpers.IsDeDigivolveImmune(context, permanentId);
    }
}

#endregion

#region Trash digivolution cards

/// <summary>
/// (MIG3-3b) 1:1 mirror of AS-IS <c>ITrashDigivolutionCards</c> (CardController.cs:5127-5236): trash SPECIFIC
/// digivolution sources of a permanent (arbitrary stack positions; no top-promotion needed). Guards
/// (:5150-5156): non-null target list, mandatory causing effect, host alive, stack-trash immunity,
/// CanNotBeAffected, non-empty stack. Candidates filter to (actual stack members) AND (not
/// CanNotTrashFromDigivolutionCards-protected) (:5158-5160).
///
/// AS-IS :5171-5192 the "[When digivolution cards would be trashed]" (WhenWouldDigivolutionCardDiscarded)
/// cut-in is LIVE in the original (unlike ITrashLinkCards' dead precedent) — headless has no such timing /
/// producer / drive point yet: design item MIG3-CUTIN-WOULDDISCARD. The willBeRemoveSources mark/refilter
/// round-trip (:5164, :5194-5200) is wired for when it lands — today a no-op re-filter.
///
/// Substrate notes: window + AceOverflow + physical removal (:5202-5234) are owned by
/// <see cref="DigivolutionStackHelpers.TrashSpecificSourcesAsync"/> (built for this exact AS-IS shape: fires
/// OnDigivolutionCardDiscarded — NOT zone-derived — applies overflow, then trashes each source).
/// </summary>
public class ITrashDigivolutionCards
{
    public ITrashDigivolutionCards(Permanent permanent, List<CardSource> trashTargetCards, HeadlessEntityId? causeEffectSourceId, ICardEffect? cardEffect = null)
    {
        _permanent = permanent;
        _trashTargetCards = trashTargetCards is null ? null : new List<CardSource>(trashTargetCards);
        _causeEffectSourceId = causeEffectSourceId;
        _cardEffect = cardEffect;
    }

    public bool IsTrashed(CardSource cardSource) => TrashedCards.Any(trashed => trashed.InstanceId == cardSource.InstanceId);

    Permanent _permanent = null!;
    List<CardSource>? _trashTargetCards;
    public List<CardSource> TrashedCards { get; } = new();
    readonly HeadlessEntityId? _causeEffectSourceId;
    // (C1b RD-C1-CARDEFFECT-IDTHREAD) AS-IS `ICardEffect cardEffect` re-threaded alongside the cause id. AS-IS
    // REQUIRES a live cause here (:5151 early-exit on null) — the id gate below already enforces that.
    readonly ICardEffect? _cardEffect;

    public async Task TrashDigivolutionCards(CancellationToken cancellationToken = default)
    {
        if (_trashTargetCards == null) return; // AS-IS :5150
        if (_causeEffectSourceId is not { IsEmpty: false } causeId) return; // AS-IS :5151
        if (_permanent == null) return; // AS-IS :5152
        if (_permanent.TopCard == null) return; // AS-IS :5153 — structurally dead, kept for 1:1 shape.

        EngineContext context = _permanent.TopCard.Context;

        // AS-IS :5154 ImmuneFromStackTrashing(_cardEffect). (R3-W3c B6) rehomed to the AS-IS-literal live getter.
        if (_permanent.ImmuneFromStackTrashing(_cardEffect!)) return;

        // AS-IS :5155 TopCard.CanNotBeAffected(_cardEffect). (R3-W3c-2) rehomed from the registry gate to the
        // AS-IS-literal live ICanNotAffectedEffect scan — the live cause effect is threaded (_cardEffect).
        if (_permanent.TopCard.CanNotBeAffected(_cardEffect)) return;

        if (_permanent.HasNoDigivolutionCards) return; // AS-IS :5156

        // AS-IS :5158-5160 membership + CanNotTrashFromDigivolutionCards protection filter.
        List<CardSource> hostSources = _permanent.DigivolutionCards.ToList();
        _trashTargetCards = _trashTargetCards
            .Where(cs => hostSources.Any(s => s.InstanceId == cs.InstanceId) && !CanNotTrashFromDigivolutionCards(context, _cardEffect, causeId, cs.InstanceId))
            .ToList();

        if (_trashTargetCards.Count == 0) return; // AS-IS :5162

        // AS-IS :5164 mark willBeRemoveSources.
        foreach (CardSource source in _trashTargetCards)
        {
            SetWillBeRemoveSources(context, source.InstanceId, true);
        }

        // AS-IS :5167-5169 ShowCardEffect / CreateDebuffEffect = UI (stripped).

        #region cut in effect - Would discard
        // AS-IS :5171-5192: LIVE WhenWouldDigivolutionCardDiscarded cut-in — StackSkillInfos(
        // WhenDigivolutionCardWouldDiscardedCheckHashtable(...)) then, if anything stacked,
        // TriggeredSkillProcess(false, HasExecutedSameEffect) drains it synchronously. Headless has no such
        // timing/gate/producer yet and no synchronous cut-in drive at this leaf: design item
        // MIG3-CUTIN-WOULDDISCARD. Nothing clears willBeRemoveSources today — the round-trip below is wired
        // for when the cut-in lands.
        #endregion

        // AS-IS :5194-5200 fix the target permanent + surviving willBeRemoveSources list.
        Permanent permanentTargetFixed = _permanent;
        List<CardSource> trashDigivolutionCardsFixed = _trashTargetCards
            .Where(cs => cs != null && ReadWillBeRemoveSources(context, cs.InstanceId))
            .ToList();

        // (C1b) AS-IS CardController.cs:5206-5215 — drained from C2 flip. StackSkillInfos({"CardEffect", cardEffect},
        // {"Permanent", permanentTargetFixed}, {"DiscardedCards", trashDigivolutionCardsFixed},
        // OnDigivolutionCardDiscarded), fired BEFORE the removal (AS-IS :5215 precedes AceOverflow :5219). Live
        // _cardEffect re-threaded from the in-scope IDigiBurst + SelectCardEffect callers (RD-C1-CARDEFFECT-IDTHREAD).
        // Design item RD-C1b-DIGIDISCARD-POS: the CARRIER emit for this timing lives in
        // DigivolutionStackHelpers.TrashSpecificSourcesAsync (Headless substrate), a position divergence from the
        // AS-IS in-class :5215 emit — position re-housing is out of C1b scope (main-instance insert here is inert).
        await GManager.instance.autoProcessing.StackSkillInfos(
            new System.Collections.Hashtable
            {
                { "CardEffect", _cardEffect },
                { "Permanent", permanentTargetFixed },
                { "DiscardedCards", trashDigivolutionCardsFixed },
            },
            EffectTiming.OnDigivolutionCardDiscarded).ConfigureAwait(false);

        // AS-IS :5202-5234: OnDigivolutionCardDiscarded window + AceOverflow (over the ORIGINAL unfixed list,
        // :5219 — see the goal-3 memory quirk note) + physical removal loop — the substrate helper owns all
        // three for this exact shape.
        _ = await DigivolutionStackHelpers.TrashSpecificSourcesAsync(
            context.CardInstanceRepository, context.ZoneMover, permanentTargetFixed.InstanceId,
            trashDigivolutionCardsFixed.Select(cs => cs.InstanceId).ToArray(),
            cancellationToken, gameEventQueue: context.GameEventQueue,
            effectRegistry: context.EffectRegistry, context: context,
            causingEffectSourceId: causeId).ConfigureAwait(false);

        foreach (CardSource cardSource in trashDigivolutionCardsFixed)
        {
            // AS-IS :5232-5233 clear willBeRemoveSources; TrashedCards.Add.
            SetWillBeRemoveSources(context, cardSource.InstanceId, false);
            TrashedCards.Add(cardSource);
        }
    }

    // (R3-W3c-4) AS-IS CardController.cs:5158-5160 `cs.CanNotTrashFromDigivolutionCards(_cardEffect)` — the R1-e
    // live scan (per-source willBeRemoveSources stamp OR a usable field/player/self ICanNotTrashFromDigivolutionCards
    // effect, BT9_109). The live causing effect is threaded when available (AS-IS passes `_cardEffect` verbatim);
    // an id-only dormant caller collapses it to its source card. Replaces the dead-registry TrashProtectionScan.
    private static bool CanNotTrashFromDigivolutionCards(EngineContext context, ICardEffect? cardEffect, HeadlessEntityId causeEffectSourceId, HeadlessEntityId sourceId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(sourceId, out CardInstanceRecord? record) || record is null)
        {
            return false;
        }

        if (record.Metadata.TryGetValue(CardEffectCommons.CardEffectCommons.TrashProtectedKey, out object? raw) && raw is true)
        {
            return true;
        }

        var sourceBeingTrashed = new CardSource(context, sourceId, record.OwnerId, record.OwnerId);
        ICardEffect cause = cardEffect ?? CardEffectCommons.BareCauseEffect.For(context, causeEffectSourceId);
        return sourceBeingTrashed.CanNotTrashFromDigivolutionCards(cause);
    }

    // (MIG2 precedent) willBeRemoveSources round-trip — key shared with ITrashLinkCards, helpers duplicated
    // per the self-contained-privates style.
    private static void SetWillBeRemoveSources(EngineContext context, HeadlessEntityId cardId, bool value)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null) return;

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
        if (value)
        {
            metadata[ITrashLinkCards.WillBeRemoveSourcesKey] = true;
        }
        else
        {
            metadata.Remove(ITrashLinkCards.WillBeRemoveSourcesKey);
        }

        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    private static bool ReadWillBeRemoveSources(EngineContext context, HeadlessEntityId cardId) =>
        context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) && record is not null
            && record.Metadata.TryGetValue(ITrashLinkCards.WillBeRemoveSourcesKey, out object? raw) && raw is true;
}

#endregion

#region Trash link cards

/// <summary>
/// (MIG2) 1:1 mirror of AS-IS <c>ITrashLinkCards</c> (CardController.cs:5242-5346): the single carrier for
/// trashing a permanent's LINK cards — guards (host alive, CanNotBeAffected, membership), the
/// willBeRemoveSources mark/refilter round-trip, the BATCH "[When link cards are trashed]"
/// (OnLinkCardDiscarded) window, ACE overflow for the leaving links, then the silent per-card
/// <c>Permanent.RemoveLinkedCard</c> removals. A bare RemoveLinkedCard never opens the window — every
/// trash path that should fire it converges here (AS-IS rule 6, SelectCardEffect Mode.Discard).
///
/// Substrate notes: the AS-IS cause is an <c>ICardEffect</c>; headless identifies the causing effect by its
/// SOURCE ENTITY (goal-1 SwitchDefender precedent) — null on the rule-process paths, exactly like the AS-IS
/// null cardEffect. ShowCardEffect / CreateDebuffEffect / RemoveDigivolveRootEffect are UI (stripped). The
/// AS-IS would-discard cut-in region (:5283-5304) is DEAD in the original (its StackSkillInfos is commented
/// out; the cut-in drain runs against an empty stack) — preserved as documentation, not re-invented.
/// </summary>
public class ITrashLinkCards
{
    /// <summary>(MIG2) AS-IS <c>CardSource.willBeRemoveSources</c> as an instance-metadata flag: marked on the
    /// trash targets before the (dead) would-discard cut-in, re-filtered after, cleared per removal.</summary>
    public const string WillBeRemoveSourcesKey = "willBeRemoveSources";

    public ITrashLinkCards(Permanent permanent, List<CardSource> trashTargetCards, HeadlessEntityId? causeEffectSourceId, ICardEffect? cardEffect = null)
    {
        _permanent = permanent;

        _trashTargetCards = trashTargetCards is null ? null : new List<CardSource>(trashTargetCards);

        _causeEffectSourceId = causeEffectSourceId;
        _cardEffect = cardEffect;
    }

    public bool IsTrashed(CardSource cardSource)
    {
        return TrashedLinkCards.Any(trashed => trashed.InstanceId == cardSource.InstanceId);
    }

    Permanent _permanent = null!;
    List<CardSource>? _trashTargetCards = new();
    public List<CardSource> TrashedLinkCards = new();
    readonly HeadlessEntityId? _causeEffectSourceId;
    // (C1b RD-C1-CARDEFFECT-IDTHREAD) AS-IS `ICardEffect cardEffect` re-threaded alongside the cause id — null on
    // the rule-process paths (AutoProcessing DigimonLackLinkCondition), exactly like the AS-IS null cardEffect.
    readonly ICardEffect? _cardEffect;

    public async Task TrashLinkCards(CancellationToken cancellationToken = default)
    {
        if (_trashTargetCards == null) return;
        if (_permanent == null) return;
        if (_permanent.TopCard == null) return;
        // AS-IS :5268 `_cardEffect != null && TopCard.CanNotBeAffected(_cardEffect)` — the S2 immunity gate,
        // keyed on the causing effect's source (null cause = rules trash, never blocked).
        if (_causeEffectSourceId is { } causeId && !causeId.IsEmpty)
        {
            // (R3-W3c-2) rehomed from the registry gate to the AS-IS-literal live scan (the live cause effect is
            // threaded, _cardEffect); AS-IS :5268 `TopCard.CanNotBeAffected(_cardEffect)`.
            if (_permanent.TopCard.CanNotBeAffected(_cardEffect))
            {
                return;
            }
        }

        if (_permanent.HasNoLinkCards) return;

        EngineContext context = _permanent.TopCard.Context;

        // AS-IS :5271 membership filter — only cards still linked to the host.
        List<CardSource> hostLinks = _permanent.LinkedCards;
        _trashTargetCards = _trashTargetCards
            .Where(cardSource => hostLinks.Any(linked => linked.InstanceId == cardSource.InstanceId))
            .ToList();

        if (_trashTargetCards.Count == 0) return;

        // AS-IS :5276 mark willBeRemoveSources.
        foreach (CardSource source in _trashTargetCards)
        {
            SetWillBeRemoveSources(context, source.InstanceId, true);
        }

        // AS-IS :5278-5281 ShowCardEffect / CreateDebuffEffect = UI (stripped).

        #region cut in effect - Would discard
        // AS-IS :5283-5304: the "[When link cards would be trashed]" cut-in StackSkillInfos is COMMENTED OUT
        // in the original (WhenLinkCardWouldDiscard was never created); the HasAwaitingActivateEffects() +
        // TriggeredSkillProcess drain that follows runs against an empty cut-in stack. Dead in AS-IS —
        // documented here, not re-invented.
        #endregion

        // AS-IS :5307-5312 fix the trash target permanent and the surviving willBeRemoveSources list.
        Permanent permanentTarget_Fixed = _permanent;

        List<CardSource> trashLinkCards_Fixed = _trashTargetCards
            .Where(cardsource => cardsource != null && ReadWillBeRemoveSources(context, cardsource.InstanceId))
            .ToList();

        #region "When link cards are trashed" effect
        // AS-IS :5314-5328: ONE batch window per TrashLinkCards call — hashtable {CardEffect, Permanent,
        // DiscardedCards} -> StackSkillInfos(OnLinkCardDiscarded). Substrate: one event, subject = host,
        // payload = the fixed discarded-card ids (+ the causing effect source when present).
        var extraMetadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["discardedCardIds"] = trashLinkCards_Fixed.Select(card => card.InstanceId.Value).ToArray(),
        };
        if (_causeEffectSourceId is { } cause && !cause.IsEmpty)
        {
            extraMetadata["causeEffectSourceId"] = cause.Value;
        }

        TriggerEventEmitter.Emit(
            context.GameEventQueue,
            TriggerTimings.OnLinkCardDiscarded,
            actor: permanentTarget_Fixed.OwnerId,
            subject: permanentTarget_Fixed.InstanceId,
            extraMetadata: extraMetadata);

        // (C1b) AS-IS CardController.cs:5314-5327 — drained from C2 flip. StackSkillInfos({"CardEffect", cardEffect},
        // {"Permanent", permanentTarget_Fixed}, {"DiscardedCards", trashLinkCards_Fixed}, OnLinkCardDiscarded). Live
        // _cardEffect re-threaded from the in-scope SelectCardEffect caller (RD-C1-CARDEFFECT-IDTHREAD); the carrier
        // Emit above stays (main inert). The MetadataActionProcessor caller passes id-only (off-limits this batch).
        await GManager.instance.autoProcessing.StackSkillInfos(
            new System.Collections.Hashtable
            {
                { "CardEffect", _cardEffect },
                { "Permanent", permanentTarget_Fixed },
                { "DiscardedCards", trashLinkCards_Fixed },
            },
            EffectTiming.OnLinkCardDiscarded).ConfigureAwait(false);
        #endregion

        // AS-IS :5332 `new AceOverflowClass(_trashTargetCards).Overflow()` — (MIG3-3a) now the mirror class
        // itself (its IsExistOnBattleArea filter resolves a link card through PermanentOfThisCard, so an
        // on-field host's links pass exactly like the AS-IS permanent-membership read).
        await new AceOverflowClass(_trashTargetCards).Overflow(cancellationToken).ConfigureAwait(false);

        foreach (CardSource cardSource in trashLinkCards_Fixed)
        {
            // AS-IS :5336 RemoveDigivolveRootEffect = UI (stripped).

            if (!cardSource.IsToken)
            {
                // AS-IS :5340 permanent.RemoveLinkedCard(cardSource) — the SILENT removal (no window; the
                // batch window above is the only emit).
                await permanentTarget_Fixed.RemoveLinkedCard(cardSource, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            SetWillBeRemoveSources(context, cardSource.InstanceId, false);
            TrashedLinkCards.Add(cardSource);
        }
    }

    private static void SetWillBeRemoveSources(EngineContext context, HeadlessEntityId cardId, bool value)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
        if (value)
        {
            metadata[WillBeRemoveSourcesKey] = true;
        }
        else
        {
            metadata.Remove(WillBeRemoveSourcesKey);
        }

        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    private static bool ReadWillBeRemoveSources(EngineContext context, HeadlessEntityId cardId) =>
        context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) && record is not null
            && record.Metadata.TryGetValue(WillBeRemoveSourcesKey, out object? raw) && raw is true;
}

#endregion

#region Return digivolution cards to deck bottom

/// <summary>
/// (MIG3-3b) 1:1 mirror of AS-IS <c>ReturnToLibraryBottomDigivolutionCardsClass</c> (CardController.cs:
/// 5352-5406): returns selected digivolution sources to the bottom of the owner's deck. No top-promotion.
/// Guards: host alive, non-null list; membership filter (:5373); optional cause's CanNotBeAffected gate
/// (:5379 — a null cause is NEVER blocked). Opens ONE OnDigivolutionCardReturnToDeckBottom window for the
/// whole batch (:5391-5400) BEFORE the physical moves (:5404) — NOT zone-derived, manual emit, matching the
/// existing DigivolutionStackHelpers precedent for this timing. No cut-in, no AceOverflow, no
/// willBeRemoveSources at all — the AS-IS odd-one-out shape, preserved.
///
/// Substrate notes: AS-IS extracts CardEffect from a ctor Hashtable at run time — the indirection is dropped
/// for a direct ctor cause id (the immunity LOGIC is preserved exactly). Physical mutation =
/// <see cref="DigivolutionStackHelpers.PlaySpecificSourceAsync"/> (detach + move; Library destination inserts
/// at the BOTTOM via the zone mover's default insertion — AS-IS AddLibraryBottomCards).
/// </summary>
public class ReturnToLibraryBottomDigivolutionCardsClass
{
    public ReturnToLibraryBottomDigivolutionCardsClass(Permanent permanent, List<CardSource> cardSources, HeadlessEntityId? causeEffectSourceId, ICardEffect? cardEffect = null)
    {
        _permanent = permanent;
        _cardSources = cardSources is null ? null : new List<CardSource>(cardSources);
        _causeEffectSourceId = causeEffectSourceId;
        _cardEffect = cardEffect;
    }

    Permanent _permanent = null!;
    List<CardSource>? _cardSources;
    readonly HeadlessEntityId? _causeEffectSourceId;
    // (C1b RD-C1-CARDEFFECT-IDTHREAD / R3-W3c-2) AS-IS live `_cardEffect` threaded for the S2 immunity live scan.
    // No mirror caller yet — AS-IS-shaped for when a return-digivolution-cards card lands.
    readonly ICardEffect? _cardEffect;

    public async Task ReturnToLibraryBottomDigivolutionCards(CancellationToken cancellationToken = default)
    {
        if (_permanent == null) return; // AS-IS :5369
        if (_permanent.TopCard == null) return; // AS-IS :5370 — structurally dead, kept for 1:1 shape.
        if (_cardSources == null) return; // AS-IS :5371

        EngineContext context = _permanent.TopCard.Context;

        // AS-IS :5373 membership filter.
        List<CardSource> hostSources = _permanent.DigivolutionCards.ToList();
        _cardSources = _cardSources.Where(cs => hostSources.Any(s => s.InstanceId == cs.InstanceId)).ToList();

        if (_cardSources.Count == 0) return; // AS-IS :5375

        // AS-IS :5377-5379 GetCardEffectFromHashtable + CanNotBeAffected — null cause is never blocked.
        // (R3-W3c-2) rehomed to the AS-IS-literal live scan (threaded _cardEffect).
        if (_causeEffectSourceId is { IsEmpty: false }
            && _permanent.TopCard.CanNotBeAffected(_cardEffect))
        {
            return;
        }

        // AS-IS :5383-5385 ShowCardEffect / CreateDebuffEffect = UI (stripped).

        #region "When digivolution cards are returned to deck" effect

        var extraMetadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["deckBottomCardIds"] = _cardSources.Select(cs => cs.InstanceId.Value).ToArray(),
        };
        if (_causeEffectSourceId is { IsEmpty: false } cause)
        {
            extraMetadata["causeEffectSourceId"] = cause.Value;
        }

        // AS-IS :5400 StackSkillInfos(OnDigivolutionCardReturnToDeckBottom) — BEFORE the moves (:5404 order).
        TriggerEventEmitter.Emit(
            context.GameEventQueue, TriggerTimings.OnDigivolutionCardReturnToDeckBottom,
            actor: _permanent.OwnerId, subject: _permanent.InstanceId, extraMetadata: extraMetadata);

        #endregion

        // AS-IS :5404 CardObjectController.AddLibraryBottomCards(_cardSources).
        foreach (CardSource cardSource in _cardSources)
        {
            await DigivolutionStackHelpers.PlaySpecificSourceAsync(
                context.CardInstanceRepository, context.ZoneMover, _permanent.InstanceId, cardSource.InstanceId,
                ChoiceZone.Library, cancellationToken).ConfigureAwait(false);
        }
    }
}

#endregion

#region Reduce Security

/// <summary>(MIG3-3a) A would-be OnLoseSecurity window record for <see cref="IReduceSecurity"/>'s collect-mode
/// (the AS-IS <c>ref List&lt;SkillInfo&gt;</c> non-null branch): deferred so a future caller (the ISecurityCheck
/// mirror, slice 3d) can batch it with a sibling OnSecurityCheck emission instead of firing standalone.</summary>
public sealed record PendingSecurityTrigger(
    string Timing,
    HeadlessPlayerId ActorId,
    HeadlessEntityId? SubjectId,
    IReadOnlyDictionary<string, object?> ExtraMetadata);

/// <summary>
/// (MIG3-3a) 1:1 mirror of AS-IS <c>IReduceSecurity</c> (CardController.cs:5412-5456): the LOAD-BEARING "a card
/// left security" window primitive, dual-mode on the AS-IS <c>ref List&lt;SkillInfo&gt; refSkillInfos</c>:
/// null (the <c>ContinuousController.nullSkillInfos</c> sentinel — <see cref="IDestroySecurity"/>) = stack NOW;
/// non-null (ISecurityCheck) = COLLECT the candidates into the caller's list so they batch with a sibling
/// OnSecurityCheck emission. Headless: null collector = the OnLoseSecurity window is carried by the caller's
/// Security-departure zone moves (SecurityLossBatchId collapse — a manual emit here would double-fire, see the
/// file-header seam note); non-null = append a <see cref="PendingSecurityTrigger"/> for the caller.
///
/// AS-IS quirks kept in doc form: the hashtable key literally "SkillInfo" carries the (possibly-null) ref list
/// itself — a self-referential quirk of the AS-IS GetSkillInfos(hashtable, timing) reader, substituted by the
/// <c>refCollector</c> parameter. GManager.OnSecurityStackChanged = UI refresh (stripped).
/// </summary>
public class IReduceSecurity
{
    public IReduceSecurity(EngineContext context, HeadlessPlayerId playerId, List<PendingSecurityTrigger>? refCollector, ICardEffect? cardEffect)
    {
        _context = context;
        _playerId = playerId;
        _refCollector = refCollector;
        _cardEffect = cardEffect;
        // (C1c RD-C1b-CARDARG) AS-IS `new IReduceSecurity(player, ref skillInfos, cardEffect)` — live cardEffect
        // carried for the OnLoseSecurity window; substrate cause id derived from it (was the MIG3 arg).
        _causeEffectSourceId = cardEffect?.EffectSourceCard?.InstanceId;
    }

    readonly EngineContext _context;
    readonly HeadlessPlayerId _playerId;
    readonly List<PendingSecurityTrigger>? _refCollector;
    readonly ICardEffect? _cardEffect;
    readonly HeadlessEntityId? _causeEffectSourceId;

    public async Task ReduceSecurity(CancellationToken cancellationToken = default)
    {
        // AS-IS :5427 GManager.OnSecurityStackChanged?.Invoke(_player) = UI (stripped).

        var hashtable = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["playerId"] = _playerId.Value,
        };
        if (_causeEffectSourceId is { IsEmpty: false } cause)
        {
            hashtable["causeEffectSourceId"] = cause.Value;
        }

        if (_refCollector is null)
        {
            // AS-IS :5444 StackSkillInfos(hashtable, OnLoseSecurity) — carried by the caller's Security->
            // departure CardMoved events (zone-derived OnLoseSecurity + SecurityLossBatchId collapse); a manual
            // emit here would double-fire against that derivation.

            // (C1c) AS-IS CardController.cs:5432-5444 — drained from C2 flip. StackSkillInfos({"Player", _player},
            // {"SkillInfo", null}, {"CardEffect", _cardEffect}, OnLoseSecurity), the null-ref (emit-now) branch.
            // Live _cardEffect re-threaded (RD-C1b-CARDARG); the zone-derived carrier stays (main undrained -> inert).
            await GManager.instance.autoProcessing.StackSkillInfos(
                new System.Collections.Hashtable
                {
                    { "Player", new Player(_context, _playerId) },
                    { "SkillInfo", null },
                    { "CardEffect", _cardEffect },
                },
                EffectTiming.OnLoseSecurity).ConfigureAwait(false);
        }
        else
        {
            // AS-IS :5448-5451 `foreach (SkillInfo in AutoProcessing.GetSkillInfos(hashtable, OnLoseSecurity))
            // _refSkillInfos.Add(skillInfo);` — collect instead of firing.
            _refCollector.Add(new PendingSecurityTrigger(TriggerTimings.OnLoseSecurity, _playerId, null, hashtable));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

#endregion

#region Add Security

/// <summary>
/// (MIG3-3a) 1:1 mirror of AS-IS <c>IAddSecurity</c> (CardController.cs:5462-5510): the "[When security cards
/// are added]" (OnAddSecurity) window for a single card ALREADY placed in security (the placement move happens
/// in the caller) — carried by the caller's ->Security zone move (zone-derived, per-card batch id; a manual
/// emit would double-fire) — plus the "[When face up cards are added]" (OnFaceUpSecurityIncreased) window when
/// the card is face up (:5494), which is NOT zone-derived: the manual emit here is its sole source.
///
/// Substrate notes: AS-IS <c>!_cardSource.IsFlipped</c> ("is face up") maps to
/// <c>SecurityFaceState.IsFaceUpInSecurity</c> — the security face flag is deliberately distinct from the
/// field-level <c>CardSource.IsFlipped</c>. GManager.OnSecurityStackChanged = UI (stripped).
/// </summary>
public class IAddSecurity
{
    public IAddSecurity(CardSource source)
    {
        _player = source.Owner;
        _cardSource = source;
    }

    readonly HeadlessPlayerId _player;
    readonly CardSource _cardSource;

    public async Task AddSecurity(CancellationToken cancellationToken = default)
    {
        EngineContext context = _cardSource.Context;

        // AS-IS :5475 GManager.OnSecurityStackChanged = UI (stripped).

        // (C1) AS-IS CardController.cs:5481-5489 — drained from C2 flip. StackSkillInfos({"Player", _player},
        // {"CardSources", [ _cardSource ]}, OnAddSecurity) — UNCONDITIONAL, before the face-up half. No
        // CardEffect member. (_player is a HeadlessPlayerId here; the AS-IS live Player == the mirror Player view.)
        await GManager.instance.autoProcessing.StackSkillInfos(
            new System.Collections.Hashtable
            {
                { "Player", new Player(context, _player) },
                { "CardSources", new List<CardSource> { _cardSource } },
            },
            EffectTiming.OnAddSecurity).ConfigureAwait(false);

        // AS-IS :5494 `if (!_cardSource.IsFlipped)` — the face-up half, sole source.
        if (SecurityFaceState.IsFaceUpInSecurity(context, _cardSource.InstanceId))
        {
            var hashtable = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["playerId"] = _player.Value,
                ["cardSourceIds"] = new[] { _cardSource.InstanceId.Value },
            };
            TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnFaceUpSecurityIncreased, actor: _player, subject: _cardSource.InstanceId, extraMetadata: hashtable);

            // (C1) AS-IS CardController.cs:5496-5506 — drained from C2 flip. StackSkillInfos({"Player", _player},
            // {"CardSources", [ _cardSource ]}, OnFaceUpSecurityIncreased) inside the face-up guard. No CardEffect.
            await GManager.instance.autoProcessing.StackSkillInfos(
                new System.Collections.Hashtable
                {
                    { "Player", new Player(context, _player) },
                    { "CardSources", new List<CardSource> { _cardSource } },
                },
                EffectTiming.OnFaceUpSecurityIncreased).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

#endregion

#region Flip Security Face Up

/// <summary>
/// (MIG3-3a) 1:1 mirror of AS-IS <c>IFlipSecurity</c> (CardController.cs:5516-5552): flips a face-down security
/// card face up, then fires OnFaceUpSecurityIncreased. Two guards preserved literally: the ENTRY guard (in
/// security AND currently face-down, :5529) and the POST-SetFace re-check (:5546 — always true right after
/// SetFace(), an AS-IS quirk kept rather than collapsed to an unconditional emit).
///
/// Substrate notes: SetFace()/IsFlipped map to SecurityFaceState.Stamp/IsFaceUpInSecurity (see
/// <see cref="IAddSecurity"/>). This and <see cref="IAddSecurityFromLibrary"/> are the first wiring of the
/// pre-built SecurityFaceState substrate (it had no callers before slice 3a).
/// </summary>
public class IFlipSecurity
{
    public IFlipSecurity(CardSource source)
    {
        _player = source.Owner;
        _cardSource = source;
    }

    readonly HeadlessPlayerId _player;
    readonly CardSource _cardSource;

    public async Task FlipFaceUp(CancellationToken cancellationToken = default)
    {
        EngineContext context = _cardSource.Context;
        var zones = (IZoneStateReader)context.ZoneMover;

        // AS-IS :5529 `if (!_player.SecurityCards.Contains(_cardSource) || !_cardSource.IsFlipped) yield break;`
        // — must be IN security AND currently face-DOWN (face-up == IsFaceUpInSecurity).
        if (!zones.GetCards(_player, ChoiceZone.Security).Contains(_cardSource.InstanceId)
            || SecurityFaceState.IsFaceUpInSecurity(context, _cardSource.InstanceId))
        {
            return;
        }

        // AS-IS :5532 _cardSource.SetFace().
        SecurityFaceState.Stamp(context.CardInstanceRepository, _cardSource.InstanceId, faceUp: true);

        var hashtable = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["playerId"] = _player.Value,
            ["cardSourceIds"] = new[] { _cardSource.InstanceId.Value },
        };

        // AS-IS :5546 post-SetFace re-check quirk, preserved literally.
        if (SecurityFaceState.IsFaceUpInSecurity(context, _cardSource.InstanceId))
        {
            TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnFaceUpSecurityIncreased, actor: _player, subject: _cardSource.InstanceId, extraMetadata: hashtable);

            // (C1) AS-IS CardController.cs:5538-5548 — drained from C2 flip. StackSkillInfos({"Player", _player},
            // {"CardSources", [ _cardSource ]}, OnFaceUpSecurityIncreased) inside the post-SetFace guard. No CardEffect.
            await GManager.instance.autoProcessing.StackSkillInfos(
                new System.Collections.Hashtable
                {
                    { "Player", new Player(context, _player) },
                    { "CardSources", new List<CardSource> { _cardSource } },
                },
                EffectTiming.OnFaceUpSecurityIncreased).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

#endregion

#region Suspend permanents

/// <summary>
/// (MIG3-3b) 1:1 mirror of AS-IS <c>SuspendPermanentsClass</c> (CardController.cs:5558-5655): suspends (taps)
/// a batch of permanents — the class design item RD9-87 names as the not-yet-wired OnTappedAnyone /
/// CanSuspend / DPWhenSuspended source (this port supplies it; rewiring the attack-suspend call sites is a
/// separate step). <c>PermanentCondition</c> (:5587-5614) preserved with its exact nesting — a null /
/// null-TopCard permanent PASSES the filter (AS-IS defensive-dead-code quirk, kept). Per surviving target:
/// IsSuspended = true THEN DPWhenSuspended = DP — the ORDER matters (:5618-5620, DP read AFTER the flag flips
/// so a "gains DP while suspended" modifier is captured). ONE OnTappedAnyone window per batch (:5636-5648).
///
/// Substrate notes: the AS-IS ctor Hashtable (CardEffect + IsBlock + IsAttack) becomes direct params; IsAttack
/// is read (:5583) but never referenced again (verified dead) — dropped. isSuspended reuses
/// DeDigivolveHelpers.IsSuspendedKey (the key Permanent.IsSuspended reads); dpWhenSuspended is a NEW key (no
/// prior headless writer). CanSuspend = !ContinuousRestrictionGate.EvaluateSuspend(...).IsRestricted.
/// OnTappedAnyone is NOT zone-derived — manual emit; zero prior consumers, batch-list payload style
/// (design item MIG3-TAPPEDANYONE-PAYLOAD). ShowPermanentData / WaitForSeconds = UI (stripped).
/// </summary>
public class SuspendPermanentsClass
{
    /// <summary>AS-IS <c>Permanent.DPWhenSuspended</c> — the DP snapshot the instant a permanent suspends
    /// (AS-IS :5620). No headless metadata key existed before this translation.</summary>
    public const string DpWhenSuspendedKey = "dpWhenSuspended";

    public SuspendPermanentsClass(List<Permanent> permanents, ICardEffect? cardEffect, bool isBlock)
    {
        _permanents = permanents;
        _cardEffect = cardEffect;
        // (C1c RD-C1b-CARDARG) AS-IS ctor Hashtable member `{"CardEffect", cardEffect}` — live cardEffect carried
        // for the OnTappedAnyone window; substrate cause id derived from it (was the MIG3 param). null cause
        // (e.g. keyword paths with a null activateClass) -> null id, byte-identical.
        _causeEffectSourceId = cardEffect?.EffectSourceCard?.InstanceId;
        _isBlock = isBlock;
    }

    public bool IsSuspended(Permanent permanent) => SuspendedPermanents.Any(p => p.InstanceId == permanent.InstanceId);

    readonly List<Permanent> _permanents;
    public List<Permanent> SuspendedPermanents { get; } = new();
    readonly ICardEffect? _cardEffect;
    readonly HeadlessEntityId? _causeEffectSourceId;
    readonly bool _isBlock;

    public async Task Tap(CancellationToken cancellationToken = default)
    {
        if (_permanents.Count == 0) return; // AS-IS :5577

        Permanent? contextSource = _permanents.FirstOrDefault(p => p != null);
        if (contextSource is null) return;
        EngineContext context = contextSource.TopCard.Context;

        // AS-IS :5587-5614 PermanentCondition — nesting preserved literally (a null / null-TopCard permanent
        // PASSES: `return true` falls through the outer nested ifs untouched).
        bool PermanentCondition(Permanent permanent)
        {
            if (permanent != null)
            {
                if (permanent.TopCard != null) // structurally dead, kept for 1:1 shape.
                {
                    if (permanent.IsSuspended) return false;

                    if (!permanent.CanSuspend) return false; // (R1-d) AS-IS !Permanent.CanSuspend (was the unioned gate)

                    if (_causeEffectSourceId is { IsEmpty: false } cause)
                    {
                        // (R3-W3c-2) rehomed to the AS-IS-literal live scan (threaded _cardEffect).
                        if (permanent.TopCard.CanNotBeAffected(_cardEffect))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        List<Permanent> suspendTargetPermanents = _permanents.Where(PermanentCondition).ToList();

        foreach (Permanent permanent in suspendTargetPermanents)
        {
            if (permanent == null) continue; // defensive: AS-IS would NPE here too; never hit by real callers.

            // AS-IS :5618 permanent.IsSuspended = true; — flips BEFORE the DP read below.
            SetIsSuspended(context, permanent.InstanceId, true);

            // AS-IS :5620 permanent.DPWhenSuspended = permanent.DP; — order preserved.
            SetDpWhenSuspended(context, permanent.InstanceId, permanent.DP);

            // AS-IS :5622-5625 ShowingPermanentCard.ShowPermanentData = UI (stripped).

            SuspendedPermanents.Add(permanent);
        }

        if (suspendTargetPermanents.Count >= 1)
        {
            #region "Effects when permanents suspend" (OnTappedAnyone)

            // AS-IS :5636-5648 {Permanents, IsBlock[, CardEffect]} -> StackSkillInfos(OnTappedAnyone) — manual
            // (not zone-derived); design item MIG3-TAPPEDANYONE-PAYLOAD (also the emission half of RD9-87).
            var extraMetadata = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["permanentIds"] = suspendTargetPermanents.Where(p => p != null).Select(p => p.InstanceId.Value).ToArray(),
                ["isBlock"] = _isBlock,
            };
            if (_causeEffectSourceId is { IsEmpty: false } cause2)
            {
                extraMetadata["causeEffectSourceId"] = cause2.Value;
            }

            TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnTapped, extraMetadata: extraMetadata);

            // (C1c) AS-IS CardController.cs:5636-5648 — drained from C2 flip. StackSkillInfos({"Permanents",
            // suspendTargetPermanents}, {"IsBlock", IsBlock}[, {"CardEffect", CardEffect} if non-null],
            // OnTappedAnyone). Live _cardEffect re-threaded (RD-C1b-CARDARG); the carrier Emit above stays (main
            // instance undrained -> inert). CardEffect Add is conditional exactly per AS-IS :5642-5645.
            var tappedHashtable = new System.Collections.Hashtable
            {
                { "Permanents", suspendTargetPermanents },
                { "IsBlock", _isBlock },
            };
            if (_cardEffect != null)
            {
                tappedHashtable.Add("CardEffect", _cardEffect);
            }
            await GManager.instance.autoProcessing.StackSkillInfos(tappedHashtable, EffectTiming.OnTappedAnyone).ConfigureAwait(false);

            #endregion

            // AS-IS :5652 WaitForSeconds(0.3f) = UI (stripped).
        }
    }

    private static void SetIsSuspended(EngineContext context, HeadlessEntityId permanentId, bool value)
    {
        if (!context.CardInstanceRepository.TryGetInstance(permanentId, out CardInstanceRecord? record) || record is null) return;
        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal) { [DeDigivolveHelpers.IsSuspendedKey] = value };
        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    private static void SetDpWhenSuspended(EngineContext context, HeadlessEntityId permanentId, int dp)
    {
        if (!context.CardInstanceRepository.TryGetInstance(permanentId, out CardInstanceRecord? record) || record is null) return;
        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal) { [DpWhenSuspendedKey] = dp };
        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }
}

#endregion

#region Unsuspend permanents

/// <summary>
/// (MIG3-3b) 1:1 mirror of AS-IS <c>IUnsuspendPermanents</c> (CardController.cs:5661-5761): unsuspends a batch.
/// Ctor pre-filters to battle-area residents (:5665). The unsuspend-target predicate (:5675-5680) is evaluated
/// TWICE — before and after (:5723-5728) the "[When permanents would unsuspend]" (WhenUntapAnyone) cut-in,
/// literally re-applied rather than cached. Per survivor: IsSuspended = false (:5734). ONE OnUnTappedAnyone
/// window per post-cut-in batch (:5746-5754).
///
/// AS-IS :5682-5720 the WhenUntapAnyone cut-in (manual GetSkillInfos + PutStackedSkill + synchronous drain —
/// a structurally DIFFERENT pattern from the StackSkillInfos cut-ins, preserved in doc form) has NO headless
/// timing/producer at all: design item MIG3-CUTIN-WHENUNTAP. Inert today; the re-filter below re-applies the
/// identical predicate, exactly the AS-IS structure.
///
/// Substrate notes: CanUnsuspend = the existing CardEffectCommons.CanUnsuspend mirror; null cause never
/// blocked. OnUnTappedAnyone NOT zone-derived — manual emit, zero prior consumers
/// (design item MIG3-UNTAPPEDANYONE-PAYLOAD). ShowUnsuspendEffect / WillUntapObject / waits = UI (stripped).
/// </summary>
public class IUnsuspendPermanents
{
    public IUnsuspendPermanents(List<Permanent> permanents, ICardEffect? cardEffect)
    {
        // AS-IS :5665 `permanents.Clone().Filter(CardEffectCommons.IsPermanentExistsOnBattleArea)`.
        _permanents = (permanents ?? new List<Permanent>())
            .Where(p => CardEffectCommons.CardEffectCommons.IsPermanentExistsOnBattleArea(p))
            .ToList();
        _cardEffect = cardEffect;
        // (C1c RD-C1b-CARDARG) AS-IS `new IUnsuspendPermanents(list, cardEffect)` — live cardEffect carried for the
        // OnUnTappedAnyone window; substrate cause id derived from it (was the MIG3 arg).
        _causeEffectSourceId = cardEffect?.EffectSourceCard?.InstanceId;
    }

    readonly List<Permanent> _permanents;
    readonly ICardEffect? _cardEffect;
    readonly HeadlessEntityId? _causeEffectSourceId;

    public async Task Unsuspend(CancellationToken cancellationToken = default)
    {
        // (substrate-necessitated) EngineContext from a list member; equivalent to the AS-IS natural no-op on
        // an empty list.
        if (_permanents.Count == 0) return;

        EngineContext context = _permanents[0].TopCard.Context;

        // AS-IS :5675-5680 — applied TWICE (before/after the would-unsuspend cut-in, :5723-5728).
        bool IsUnsuspendTarget(Permanent permanent) =>
            permanent != null
            && permanent.TopCard != null // structurally dead, kept for 1:1 shape.
            && permanent.IsSuspended
            && CardEffectCommons.CardEffectCommons.CanUnsuspend(permanent)
            // (R3-W3c-2) rehomed to the AS-IS-literal live scan (threaded _cardEffect).
            && (_causeEffectSourceId is not { IsEmpty: false } cause
                || !permanent.TopCard.CanNotBeAffected(_cardEffect));

        List<Permanent> untappedPermanents = _permanents.Where(IsUnsuspendTarget).ToList();
        _ = untappedPermanents; // AS-IS keeps the pre-cut-in list alive for the cut-in region below.

        #region "When permanents would unsuspend" effect (cut-in)
        // AS-IS :5682-5720: WhenUntapAnyone cut-in — GetSkillInfos + manual PutStackedSkill + synchronous
        // TriggeredSkillProcess(false, HasExecutedSameEffect) drain (the MANUAL-push variant, structurally
        // distinct from the StackSkillInfos cut-ins elsewhere). No headless timing/producer/drive point yet:
        // design item MIG3-CUTIN-WHENUNTAP. Inert today.
        #endregion

        // AS-IS :5723-5728 untappedPermanets_Fixed — re-filter after the (today inert) cut-in.
        List<Permanent> untappedPermanentsFixed = _permanents.Where(IsUnsuspendTarget).ToList();

        if (untappedPermanentsFixed.Count >= 1)
        {
            foreach (Permanent permanent in untappedPermanentsFixed)
            {
                SetIsSuspended(context, permanent.InstanceId, false); // AS-IS :5734
                // AS-IS :5736-5739 ShowPermanentData = UI (stripped).
            }

            #region "When permanents are unsuspended" effect

            // AS-IS :5746-5754 {CardEffect, Permanents} -> StackSkillInfos(OnUnTappedAnyone) — manual emit.
            var extraMetadata = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["permanentIds"] = untappedPermanentsFixed.Select(p => p.InstanceId.Value).ToArray(),
            };
            if (_causeEffectSourceId is { IsEmpty: false } cause3)
            {
                extraMetadata["causeEffectSourceId"] = cause3.Value;
            }

            TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnUntapped, extraMetadata: extraMetadata);

            // (C1c) AS-IS CardController.cs:5746-5754 — drained from C2 flip. StackSkillInfos({"CardEffect",
            // _cardEffect}, {"Permanents", untappedPermanents_Fixed}, OnUnTappedAnyone) — the tail MAIN-instance
            // window (distinct from the AS-IS :5682-5720 WhenUntapAnyone CUT-IN, which stays deferred =
            // MIG3-CUTIN-WHENUNTAP, cut-in insts excluded from C1 per the worksheet). Live _cardEffect re-threaded
            // (RD-C1b-CARDARG); the carrier Emit above stays (main instance undrained -> inert).
            await GManager.instance.autoProcessing.StackSkillInfos(
                new System.Collections.Hashtable
                {
                    { "CardEffect", _cardEffect },
                    { "Permanents", untappedPermanentsFixed },
                },
                EffectTiming.OnUnTappedAnyone).ConfigureAwait(false);

            #endregion

            // AS-IS :5758 WaitForSeconds(0.3f) = UI (stripped).
        }
    }

    private static void SetIsSuspended(EngineContext context, HeadlessEntityId permanentId, bool value)
    {
        if (!context.CardInstanceRepository.TryGetInstance(permanentId, out CardInstanceRecord? record) || record is null) return;
        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal) { [DeDigivolveHelpers.IsSuspendedKey] = value };
        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }
}

#endregion

#region Trash deck cards

/// <summary>
/// (MIG3-3a) 1:1 mirror of AS-IS <c>ITrashDeckCards</c> (CardController.cs:5767-5821): trashes the subset of
/// <c>cardSources</c> still actually in their owner's LIBRARY (:5788-5794 — a card an intervening effect
/// already moved is silently skipped), then the "[When cards are trashed from security]"-labelled window — the
/// AS-IS region comment is a copy-paste artifact; the timing actually fired is OnDiscardLibrary (:5815-5816) —
/// opens ONCE for the whole batch, carried by the zone-derived timing on the shared discard batch id.
/// </summary>
public class ITrashDeckCards
{
    public ITrashDeckCards(List<CardSource> cardSources, ICardEffect? cardEffect)
    {
        this.cardSources = new List<CardSource>();

        foreach (CardSource cardSource in cardSources)
        {
            this.cardSources.Add(cardSource);
        }

        _cardEffect = cardEffect;
        // (C1c RD-C1b-CARDARG) AS-IS `new ITrashDeckCards(cardSources, cardEffect)` — live cardEffect carried for
        // the OnDiscardLibrary window; substrate cause id derived from it (was the MIG3 arg).
        _causeEffectSourceId = cardEffect?.EffectSourceCard?.InstanceId;
    }

    List<CardSource> cardSources { get; set; } = new List<CardSource>();
    readonly ICardEffect? _cardEffect;
    readonly HeadlessEntityId? _causeEffectSourceId;

    public async Task TrashDeckCards(CancellationToken cancellationToken = default)
    {
        List<CardSource> trashCards = new List<CardSource>();

        foreach (CardSource cardSource in cardSources)
        {
            var zones = (IZoneStateReader)cardSource.Context.ZoneMover;
            if (zones.GetCards(cardSource.Owner, ChoiceZone.Library).Contains(cardSource.InstanceId))
            {
                trashCards.Add(cardSource);
            }
        }

        if (trashCards.Count >= 1)
        {
            EngineContext context = trashCards[0].Context;

            // ONE shared batch id = the AS-IS single StackSkillInfos(OnDiscardLibrary) call (:5815-5816); the
            // zone-derived timing collapses on it (a manual emit here would double-fire).
            long discardBatchId = context.NextDiscardBatchId();

            foreach (CardSource cardSource in trashCards)
            {
                await context.ZoneMover.TrashCardAsync(
                    cardSource.Owner, cardSource.InstanceId, discardBatchId, _causeEffectSourceId,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            // (C1c) AS-IS CardController.cs:5808-5816 — drained from C2 flip. StackSkillInfos({"DiscardedCards",
            // trashCards}, {"CardEffect", cardEffect}, OnDiscardLibrary). Live _cardEffect re-threaded
            // (RD-C1b-CARDARG); the zone-derived OnDiscardLibrary above stays (main instance undrained -> inert).
            await GManager.instance.autoProcessing.StackSkillInfos(
                new System.Collections.Hashtable
                {
                    { "DiscardedCards", trashCards },
                    { "CardEffect", _cardEffect },
                },
                EffectTiming.OnDiscardLibrary).ConfigureAwait(false);
        }
    }
}

#endregion

#region Overflow of ace

/// <summary>
/// (MIG3-3a) 1:1 mirror of AS-IS <c>AceOverflowClass</c> (CardController.cs:5827-5852): filters
/// <c>cardSources</c> to (an un-flipped ACE on the battle area) OR (any breeding-area Digimon — the breeding
/// clause BYPASSES the ACE/flip gate entirely, per the AS-IS operator precedence `(A &amp;&amp; B &amp;&amp; C) || D`, :5839),
/// orders turn-player-owned cards first, then charges each owner <c>-OverflowMemory</c> (a no-op for a non-ACE
/// breeding Digimon, whose overflow reads 0).
///
/// Substrate notes: IsACE/OverflowMemory/IsFlipped fold into <c>AceOverflowGate.OverflowFor(record)</c> (0 for
/// non-ACE or flipped — exactly the AS-IS `IsACE &amp;&amp; !IsFlipped` half plus the printed value). PlayLog = UI.
/// </summary>
public class AceOverflowClass
{
    public AceOverflowClass(List<CardSource> cardSources)
    {
        _cardSources = cardSources is null ? new List<CardSource>() : new List<CardSource>(cardSources);
    }

    List<CardSource> _cardSources;

    public async Task Overflow(CancellationToken cancellationToken = default)
    {
        if (_cardSources.Count == 0)
        {
            return;
        }

        EngineContext context = _cardSources[0].Context;
        HeadlessPlayerId? turnPlayer = context.TurnController.Current.TurnPlayerId;

        // AS-IS :5839 precedence: `(IsACE && !IsFlipped && OnBattleArea) || OnBreedingAreaDigimon` — the
        // breeding-area Digimon clause bypasses the ACE/flip gate.
        _cardSources = _cardSources
            .Where(cardSource =>
                (OverflowFor(context, cardSource) > 0 && CardEffectCommons.CardEffectCommons.IsExistOnBattleArea(cardSource))
                || CardEffectCommons.CardEffectCommons.IsExistOnBreedingAreaDigimon(cardSource))
            .OrderBy(cardSource => turnPlayer is { } tp && cardSource.Owner == tp ? -1 : 1)
            .ToList();

        foreach (CardSource cardSource in _cardSources)
        {
            int overflow = OverflowFor(context, cardSource);
            if (overflow > 0)
            {
                context.MemoryController.Add(AceOverflowGate.MemoryDelta(overflow, cardSource.Owner, turnPlayer));
            }

            // AS-IS :5847-5849 PlayLog = UI (stripped).
            cancellationToken.ThrowIfCancellationRequested();
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static int OverflowFor(EngineContext context, CardSource cardSource) =>
        context.CardInstanceRepository.TryGetInstance(cardSource.InstanceId, out CardInstanceRecord? record) && record is not null
            ? AceOverflowGate.OverflowFor(record)
            : 0;
}

#endregion

#region TrashStack

/// <summary>
/// (MIG3-3b) 1:1 mirror of AS-IS <c>ITrashStack</c> (CardController.cs:5858-5986): the [Trash Stack] shape —
/// trashes up to <c>trashCount</c> cards from a Digimon's stack top-down, promoting the next under-source each
/// step, with NEITHER the de-digivolve immunity guard NOR the rookie/level-3 floor (AS-IS StopCondition
/// :5899-5911 checks ONLY HasNoDigivolutionCards and the count; guards :5882-5887 have no ImmuneFromDeDigivolve).
/// The <c>fromTop</c> ctor param is accepted but DEAD — AS-IS never branches on it (the loop always reads
/// TopCard); preserved exactly, not "fixed". ONE WhenTopCardTrashed window per batch, UNCONDITIONAL once the
/// count gate was reached (same AS-IS quirk as IDegeneration).
///
/// Substrate notes: same as IDegeneration (ArmorPurgeTopAsync — deliberately not DeDigivolveAsync, whose
/// immunity/floor re-checks this class must NOT inherit). AS-IS <c>StackCards.Count</c> (:5889 = TopCard +
/// DigivolutionCards) has no headless property — computed inline as DigivolutionCards.Count + 1 (provably
/// inert cap: each step needs an under-source to promote). SetChangedLocationTime = MIG3-LOCATIONTIME.
/// </summary>
public class ITrashStack
{
    public ITrashStack(Permanent permanent, int trashCount, HeadlessEntityId? causeEffectSourceId, bool fromTop = true, ICardEffect? cardEffect = null)
    {
        _permanent = permanent;
        _trashCount = trashCount;
        _causeEffectSourceId = causeEffectSourceId;
        _fromTop = fromTop; // AS-IS :5867/5872 — accepted, never branched on (dead param, preserved).
        _cardEffect = cardEffect;
    }

    Permanent _permanent = null!;
    int _trashCount;
    readonly HeadlessEntityId? _causeEffectSourceId;
    // (C1b RD-C1-CARDEFFECT-IDTHREAD / R3-W3c-2) AS-IS live `_cardEffect` threaded for the S2 immunity live scan.
    // No mirror caller yet — AS-IS-shaped for when a trash-stack card lands.
    readonly ICardEffect? _cardEffect;
#pragma warning disable CS0414 // AS-IS dead field (fromTop stored, never read in the AS-IS loop either).
    readonly bool _fromTop;
#pragma warning restore CS0414

    public async Task TrashStack(CancellationToken cancellationToken = default)
    {
        if (_causeEffectSourceId is not { IsEmpty: false }) return; // AS-IS :5882-5883
        if (_permanent == null) return; // AS-IS :5884
        if (_permanent.TopCard == null) return; // AS-IS :5885 — structurally dead, kept for 1:1 shape.

        EngineContext context = _permanent.TopCard.Context;

        // AS-IS :5886 ImmuneFromStackTrashing(_cardEffect). (R3-W3c B6) rehomed to the AS-IS-literal live getter.
        if (_permanent.ImmuneFromStackTrashing(_cardEffect!)) return;
        // AS-IS :5887 TopCard.CanNotBeAffected(_cardEffect). (R3-W3c-2) rehomed to the AS-IS-literal live scan.
        if (_permanent.TopCard.CanNotBeAffected(_cardEffect)) return;

        // AS-IS :5889 `_permanent.StackCards.Count` = TopCard + DigivolutionCards (no headless StackCards
        // property — provably inert cap, kept for literal fidelity).
        _trashCount = Math.Min(_permanent.DigivolutionCards.Count + 1, _trashCount);

        if (_trashCount < 1) return; // AS-IS :5891 complement.

        int count = 0;
        List<CardSource> selectedCards = new();
        // (MIG3 review P1-1) walk the live top id across steps — see IDegeneration.
        HeadlessEntityId currentTopId = _permanent.InstanceId;

        while (true)
        {
            var current = new Permanent(context, currentTopId, _permanent.OwnerId);

            // AS-IS :5899-5911 StopCondition — HasNoDigivolutionCards or count reached; NO level-3 floor.
            if (current.HasNoDigivolutionCards) break;
            if (count >= _trashCount) break;

            // AS-IS :5921 CreateDebuffEffect (first iteration only) = UI (stripped).

            CardSource cardSource = current.TopCard;
            selectedCards.Add(cardSource);

            HeadlessEntityId? promotedId = IDegeneration.NextPromotedSourceId(context, currentTopId);

            await new AceOverflowClass(new List<CardSource> { cardSource }).Overflow(cancellationToken).ConfigureAwait(false);

            bool promoted = await DeDigivolveHelpers.ArmorPurgeTopAsync(
                context.CardInstanceRepository, context.ZoneMover, cardSource.InstanceId,
                gameEventQueue: null, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!promoted || promotedId is not { } nextTopId) break;
            currentTopId = nextTopId;

            // AS-IS :5940 SetChangedLocationTime() — design item MIG3-LOCATIONTIME.

            count++;
        }

        #region "When Top Card is Trashed" effect

        // UNCONDITIONAL (matches AS-IS scope — not gated on selectedCards.Count). (MIG3 review P2-3)
        // subject = the surviving top after the walk.
        var extraMetadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["cardSourceIds"] = selectedCards.Select(cs => cs.InstanceId.Value).ToArray(),
        };
        TriggerEventEmitter.Emit(
            context.GameEventQueue, TriggerTimings.WhenTopCardTrashed,
            actor: _permanent.OwnerId, subject: currentTopId, extraMetadata: extraMetadata);

        // (C1) AS-IS CardController.cs:5949-5958 — drained from C2 flip. StackSkillInfos({"Permanent",
        // _permanent}, {"CardSources", selectedCards}, WhenTopCardTrashed). No CardEffect member.
        await GManager.instance.autoProcessing.StackSkillInfos(
            new System.Collections.Hashtable { { "Permanent", _permanent }, { "CardSources", selectedCards } },
            EffectTiming.WhenTopCardTrashed).ConfigureAwait(false);

        #endregion

        // AS-IS :5962-5981 add log (gated on selectedCards.Count >= 1) = UI (stripped).
    }
}

#endregion
}


// ===== (R2-C) PlayCardClass / CardSourceAsIsPlayAccessors / IBattle relocated back into CardController.cs
// AS-IS these are top-level classes in the SAME CardController.cs file (:118-933 PlayCardClass, :4427 IBattle).
// The mirror keeps them in namespace `...CardEffectCommons` (unchanged) to avoid rewiring ~30 consumers
// (incl. the parallel-owned MatchStateMutationSink.cs); a file can hold two block-scoped namespaces.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;


// AS-IS CardController.cs:118-933.
public class PlayCardClass
{
    // AS-IS :120-134.
    public PlayCardClass(List<CardSource> cardSources, Hashtable hashtable, bool payCost, Permanent targetPermanent, bool isTapped, SelectCardEffect.Root root,
    bool activateETB)
    {
        if (cardSources != null)
        {
            CardSources = cardSources.Filter(cardSource => cardSource != null).Clone();
        }

        _hashtable = hashtable;
        PayCost = payCost;
        _targetPermanent = targetPermanent;
        _isTapped = isTapped;
        Root = root;
        _activateETB = activateETB;
    }

    // AS-IS :136-142.
    public void SetJogress(int[] jogressEvoRootsFrameIDs)
    {
        if (jogressEvoRootsFrameIDs != null)
        {
            _jogressEvoRootsFrameIDs = jogressEvoRootsFrameIDs.CloneArray();
        }
    }

    // AS-IS :144-150. The AS-IS guard tail `&& BurstTamerFrameID <= card.Owner.fieldCardFrames.Count - 1`
    // needs the field-frame model — STOP RD-P6C1-1 (a negative id = the AS-IS not-set fallthrough, kept).
    public void SetBurst(int BurstTamerFrameID, CardSource card)
    {
        if (0 <= BurstTamerFrameID)
        {
            // AS-IS: if (0 <= BurstTamerFrameID && BurstTamerFrameID <= card.Owner.fieldCardFrames.Count - 1)
            //            _burstTamerFrameID = BurstTamerFrameID;
            throw new NotSupportedException(
                "STOP: SetBurst needs the field-frame model (AS-IS Player.fieldCardFrames) — no mirror " +
                "frame/slot model exists (design item RD-P6C1-1, docs/audit/rebuild_p6_cluster1_notes.md).");
        }
    }

    // AS-IS :152-158.
    public void SetAppFusion(int[] AppFusionFrameID)
    {
        if (AppFusionFrameID != null)
        {
            _appFusionFrameIDs = AppFusionFrameID.CloneArray(); ;
        }
    }

    // AS-IS :160-163.
    public void SetShowEffect()
    {
        _showEffect = true;
    }

    // AS-IS :165-169.
    public void SetIgnoreLevel()
    {
        _ignoreLevel = true;
        SetIgnoreRequirements(CardEffectCommons.IgnoreRequirement.Level);
    }

    // AS-IS :171-174.
    public void SetIgnoreRequirements(CardEffectCommons.IgnoreRequirement ignore)
    {
        _ignoreRequirement = ignore;
    }

    // AS-IS :176-179.
    private bool GetIgnoreRequirement(CardEffectCommons.IgnoreRequirement ignore)
    {
        return _ignoreRequirement.Equals(ignore) || _ignoreRequirement.Equals(CardEffectCommons.IgnoreRequirement.All);
    }

    // AS-IS :181-184.
    public void SetFixedCost(int FixedCost)
    {
        _fixedCost = FixedCost;
    }

    // AS-IS :186-189.
    public void SetReducedCost(int ReducedCost)
    {
        _reducedCost = ReducedCost;
    }

    // AS-IS :191-194.
    public void SetIsBreedingArea()
    {
        _isBreedingArea = true;
    }

    // AS-IS :196-211.
    public List<CardSource> CardSources { get; private set; } = new List<CardSource>();
    Hashtable _hashtable = null;
    public bool PayCost { get; private set; }
    Permanent _targetPermanent = null;
    bool _isTapped = false;
    public SelectCardEffect.Root Root { get; private set; } = SelectCardEffect.Root.None;
    bool _activateETB = true;
    bool _showEffect = false;
    bool _ignoreLevel = false;
    CardEffectCommons.IgnoreRequirement _ignoreRequirement = CardEffectCommons.IgnoreRequirement.None;
    int _fixedCost = -1;
    int _reducedCost = 0;
    int[] _jogressEvoRootsFrameIDs = null;
    int _burstTamerFrameID = -1;
    int[] _appFusionFrameIDs = null;
    bool _isBreedingArea = false;

    // AS-IS :213.
    public bool isJogress => _jogressEvoRootsFrameIDs != null && _jogressEvoRootsFrameIDs.Length == 2;

    // AS-IS :215-237. `card.burstDigivolutionCondition` -> `card.BurstDigivolutionConditionOf()`
    // (adaptation (6); re-read per access, exactly like the AS-IS property re-scan).
    bool IsBurst(CardSource card)
    {
        Permanent burstTamer = BurstTamer(card);

        if (burstTamer != null)
        {
            if (burstTamer.TopCard != null)
            {
                if (card.BurstDigivolutionConditionOf() != null)
                {
                    if (card.BurstDigivolutionConditionOf().tamerCondition != null)
                    {
                        if (card.BurstDigivolutionConditionOf().tamerCondition(burstTamer))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    // AS-IS :239-249. The frame lookup (`card.Owner.fieldCardFrames[_burstTamerFrameID].GetFramePermanent()`)
    // needs the frame model — STOP RD-P6C1-1. `_burstTamerFrameID < 0` (never SetBurst) = the AS-IS null
    // fallthrough, kept — the only reachable path until RD-P6C1-1 lands (SetBurst itself STOPs).
    Permanent BurstTamer(CardSource card)
    {
        _ = card;

        if (0 <= _burstTamerFrameID)
        {
            // AS-IS: if (0 <= _burstTamerFrameID && _burstTamerFrameID <= card.Owner.fieldCardFrames.Count - 1)
            //        { Permanent tamer = card.Owner.fieldCardFrames[_burstTamerFrameID].GetFramePermanent(); return tamer; }
            throw new NotSupportedException(
                "STOP: BurstTamer needs the field-frame model (AS-IS Player.fieldCardFrames[i].GetFramePermanent) " +
                "— design item RD-P6C1-1, docs/audit/rebuild_p6_cluster1_notes.md.");
        }

        return null;
    }

    // AS-IS :251-275. `card.appFusionCondition` -> the EXISTING mirror `card.AppFusionConditionOf()`
    // (adaptation (6)); the frame lookup for the host digimon is STOP RD-P6C1-1. `linkCard == null`
    // (never SetAppFusion / LinkedCard fallthrough) = the AS-IS false path, kept.
    bool IsAppFusion(CardSource card)
    {
        CardSource linkCard = LinkedCard(card);

        if (linkCard != null)
        {
            if (card.AppFusionConditionOf() != null)
            {
                if (card.AppFusionConditionOf().digimonCondition != null)
                {
                    // AS-IS :259-267: Permanent digimon = card.Owner.fieldCardFrames[_appFusionFrameIDs[0]].GetFramePermanent();
                    //                 if (card.appFusionCondition.linkedCondition != null)
                    //                     if (card.appFusionCondition.linkedCondition(digimon, linkCard)) return true;
                    throw new NotSupportedException(
                        "STOP: IsAppFusion needs the field-frame model (AS-IS Player.fieldCardFrames) — " +
                        "design item RD-P6C1-1, docs/audit/rebuild_p6_cluster1_notes.md.");
                }
            }
        }

        return false;
    }

    // AS-IS :277-294. The frame lookup needs the frame model — STOP RD-P6C1-1; `_appFusionFrameIDs` unset =
    // the AS-IS null fallthrough, kept (the only reachable path until RD-P6C1-1 lands).
    public CardSource LinkedCard(CardSource card)
    {
        _ = card;

        if (_appFusionFrameIDs != null && _appFusionFrameIDs.Length == 2)
        {
            // AS-IS :281-291: if (0 <= _appFusionFrameIDs[0] && _appFusionFrameIDs[0] <= card.Owner.fieldCardFrames.Count - 1)
            //                 { Permanent targetPermanent = card.Owner.fieldCardFrames[_appFusionFrameIDs[0]].GetFramePermanent();
            //                   if (targetPermanent.LinkedCards.Count > _appFusionFrameIDs[1])
            //                   { CardSource link = targetPermanent.LinkedCards[_appFusionFrameIDs[1]]; return link; } }
            throw new NotSupportedException(
                "STOP: LinkedCard needs the field-frame model (AS-IS Player.fieldCardFrames) — design item " +
                "RD-P6C1-1, docs/audit/rebuild_p6_cluster1_notes.md.");
        }

        return null;
    }

    // AS-IS :296-1042. `IEnumerator PlayCard()` -> `async Task PlayCard()` (see file header, adaptation (2)).
    public async Task PlayCard()
    {
        bool burstDigivolved = false;
        bool appFusion = false;
        bool isEvolution = false;

        List<CardSource> playedCards_fixed = new List<CardSource>();

        foreach (CardSource card in CardSources)
        {
            GManager.instance.GetComponent<SelectDigiXrosClass>().ResetSelectDigiXrosClass();
            // AS-IS :307 `GManager.instance.GetComponent<SelectAssemblyClass>().ResetSelectAssemblyClass();` —
            // the mirror SelectAssemblyClass is the STATIC feasibility half (material matching lives in the
            // parameterized play action), so there is no component state to reset (adaptation (7), RD-P6C1-5).
            GManager.instance.GetComponent<SelectDNACondition>().ResetSelectDNAConditionClass();

            if (card == null)
            {
                continue;
            }

            #region Set Root

            ICardEffect CardEffect = null;

            CardEffect = CardEffectCommons.GetCardEffectFromHashtable(this._hashtable);

            if (CardEffectCommons.IsExistOnTrash(card))
            {
                Root = SelectCardEffect.Root.Trash;
            }
            else if (new Player(card.Context, card.Owner).HandCards.Contains(card))
            {
                Root = SelectCardEffect.Root.Hand;
            }
            else if (new Player(card.Context, card.Owner).LibraryCards.Contains(card))
            {
                Root = SelectCardEffect.Root.Library;
            }
            else if (new Player(card.Context, card.Owner).GetFieldPermanents().Count((permanent) => permanent.DigivolutionCards.Contains(card)) >= 1)
            {
                Root = SelectCardEffect.Root.DigivolutionCards;
            }
            else if (new Player(card.Context, card.Owner).GetFieldPermanents().Count((permanent) => permanent.LinkedCards.Contains(card)) >= 1)
            {
                Root = SelectCardEffect.Root.LinkedCards;
            }
            else if (new Player(card.Context, card.Owner).SecurityCards.Contains(card))
            {
                Root = SelectCardEffect.Root.Security;
            }
            else if (CardEffectCommons.IsExistOnExecutingArea(card))
            {
                Root = SelectCardEffect.Root.Execution;
            }

            #endregion

            #region Set target(s)

            List<Permanent> targetPermanents = new List<Permanent>();

            if (card.IsPermanent())
            {
                if (!isJogress)
                {
                    if (CardEffectCommons.IsOwnerPermanent(_targetPermanent, card))
                    {
                        targetPermanents.Add(_targetPermanent);
                    }
                }
                else
                {
                    // AS-IS :377-392: resolve the two jogress evolution roots from
                    // `card.Owner.fieldCardFrames[JogressFrameID].GetFramePermanent()` (+ the
                    // SetPermanentIndexText display loop = UI) — the frame model has no mirror: STOP RD-P6C1-1.
                    throw new NotSupportedException(
                        "STOP: jogress target resolution needs the field-frame model (AS-IS " +
                        "Player.fieldCardFrames[JogressFrameID].GetFramePermanent) — design item RD-P6C1-1, " +
                        "docs/audit/rebuild_p6_cluster1_notes.md.");
                }
            }

            #endregion

            #region Determine if Evolution

            if (targetPermanents.Count >= 1)
            {
                if (!isJogress)
                {
                    if (IsBurst(card))
                    {
                        if (card.CanBurstDigivolutionFromTargetPermanent(targetPermanents[0], PayCost))
                        {
                            isEvolution = true;
                        }
                    }
                    else if (IsAppFusion(card))
                    {
                        if (card.CanAppFusionFromTargetPermanent(targetPermanents[0], PayCost))
                        {
                            isEvolution = true;
                        }
                    }
                    else
                    {
                        if (card.CanEvolve(targetPermanents[0], true) || GetIgnoreRequirement(CardEffectCommons.IgnoreRequirement.Level) || _ignoreLevel)
                        {
                            isEvolution = true;
                        }
                    }
                }
                else
                {
                    if (targetPermanents.Count == 2)
                    {
                        isEvolution = true;
                    }
                }
            }

            #endregion

            List<CardSource> oldTrashCards = new List<CardSource>();

            foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForNonTurnPlayer)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    permanent.oldIsTapped_playCard = permanent.IsSuspended;
                }
            }

            foreach (CardSource cardSource in new Player(card.Context, card.Owner).TrashCards)
            {
                oldTrashCards.Add(cardSource);
            }

            // effect of removing digivolution/linked cards
            // AS-IS :441-445: `if (card.IsPermanent && !isEvolution && card.PermanentOfThisCard() != null &&
            // (Root == DigivolutionCards || Root == LinkedCards)) yield return ... GManager.instance.
            // GetComponent<Effects>().RemoveDigivolveRootEffect(card, card.PermanentOfThisCard());` —
            // Effects.RemoveDigivolveRootEffect (Effects.cs:2162-2265) is a pure ShowUseHandCard/DOTween
            // display animation (no game-state change; the actual digivolution-card removal happens in the
            // play flow itself) = UI, stripped (adaptation (4)).

            #region select digivolution cost

            int baseCost = -1;

            bool costSelected = false;

            await SelectCost();

            // AS-IS :455. `IEnumerator SelectCost()` -> `async Task SelectCost()` (adaptation (2)).
            async Task SelectCost()
            {
                if (!isJogress)
                {
                    if (PayCost)
                    {
                        if (_fixedCost < 0)
                        {
                            Permanent targetPermanent = null;

                            if (targetPermanents.Count >= 1)
                            {
                                targetPermanent = targetPermanents[0];
                            }

                            if (targetPermanent != null)
                            {
                                List<int> CostList = new List<int>();

                                bool isBurst = IsBurst(card);
                                bool isAppFusion = IsAppFusion(card);

                                if (isBurst || isAppFusion)
                                {
                                    if (isBurst)
                                        CostList.Add(card.BurstDigivolutionConditionOf().cost);

                                    if (isAppFusion)
                                        CostList.Add(card.AppFusionConditionOf().cost);
                                }
                                else
                                {
                                    foreach (int cost in card.CostList(targetPermanent, ignoreLevel: GetIgnoreRequirement(CardEffectCommons.IgnoreRequirement.Level), checkAvailability: false))
                                    {
                                        int evoCost = cost;

                                        if (_reducedCost > 0)
                                            evoCost -= _reducedCost;

                                        CostList.Add(evoCost);
                                    }
                                }

                                CostList = CostList.Distinct().ToList();

                                if (CostList.Count >= 1)
                                {
                                    if (CostList.Count == 1)
                                    {
                                        baseCost = CostList[0];
                                    }
                                    else
                                    {
                                        costSelected = true;

                                        // AS-IS :506-530: the `MoveToExecuteCardEffect` bool + ShowingHandCard
                                        // visibility probe + `!card.Owner.isYou && GManager.instance.IsAI` +
                                        // `card.Owner.isYou && ContinuousController.instance.
                                        // autoMinDigivolutionCost` branches (which could reset costSelected on
                                        // the AI/auto-min CLIENT) + the Effects.MoveToExecuteCardEffect
                                        // animation await — Unity-client presentation steering only; the
                                        // mirror ChoiceProvider is the decider (adaptation (4)).

                                        SelectCountEffect selectCountEffect = GManager.instance.GetComponent<SelectCountEffect>();

                                        if (selectCountEffect != null)
                                        {
                                            selectCountEffect.SetUp(
                                                SelectPlayer: card.Owner,
                                                targetPermanent: null,
                                                MaxCount: 1,
                                                CanNoSelect: false,
                                                Message: "Which digivolution cost do you pay?",
                                                Message_Enemy: "The opponent is choosing which digivolution cost to pay.",
                                                SelectCountCoroutine: SelectCountCoroutine);

                                            selectCountEffect.SetCandidates(CostList);
                                            selectCountEffect.SetPreferMin(true);
                                            selectCountEffect.SetIsDigivolutionCost(true);

                                            await selectCountEffect.Activate();

                                            // AS-IS :558. `IEnumerator SelectCountCoroutine(int count)` ->
                                            // `async Task SelectCountCoroutine(int count)` (adaptation (2));
                                            // lone `yield return null;` -> `await Task.CompletedTask;`.
                                            async Task SelectCountCoroutine(int count)
                                            {
                                                baseCost = count;
                                                await Task.CompletedTask;
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                baseCost = card.BasePlayCostFromEntity();
                            }
                        }
                    }
                }
            }

            #endregion

            #region select DNA condition

            int baseDNA = 0;

            if (isJogress)
            {
                baseDNA = GManager.instance.GetComponent<SelectDNACondition>()._selectedCount;
            }

            #endregion

            #region HashTable Setting

            Hashtable hashtable = CardEffectCommons.WouldEnterFieldHashtable(
                payCost: PayCost,
                card: card,
                root: Root,
                isEvolution: isEvolution,
                playCardClass: this,
                cardEffect: CardEffect,
                isJogress: isJogress,
                targetPermanents: targetPermanents
            );

            #endregion

            // AS-IS :606 `cardSource.PermanentOfThisCard()` -> `ICardEffect.ResolvePermanentOfThisCard(cardSource)` (adaptation (3)).
            List<SkillInfo> skillInfos_BeforePayCost = AutoProcessing.GetSkillInfos(hashtable, EffectTiming.BeforePayCost)
            .Concat(AutoProcessing.GetSkillInfosOfCards(hashtable, EffectTiming.BeforePayCost, new List<CardSource>() { card }
                .Filter(cardSource => !CardEffectCommons.IsExistOnHand(cardSource) && !CardEffectCommons.IsExistOnTrash(cardSource) && !CardEffectCommons.IsExistInSecurity(cardSource) && ICardEffect.ResolvePermanentOfThisCard(cardSource) == null)))
            .ToList();

            await AutoProcessing.ActivateBackgroundEffects(hashtable, EffectTiming.BeforePayCost);

            #region IsShowEffect()

            bool IsShowEffect()
            {
                if (skillInfos_BeforePayCost.Count >= 2)
                {
                    return true;
                }
                else if (skillInfos_BeforePayCost.Count == 1)
                {
                    if (skillInfos_BeforePayCost[0].CardEffect.CanActivate(skillInfos_BeforePayCost[0].Hashtable))
                    {
                        return true;
                    }
                }
                else if (card.HasDigiXros() && !isEvolution)
                {
                    return true;
                }
                else if (IsBurst(card))
                {
                    return true;
                }
                else if (IsAppFusion(card))
                {
                    return true;
                }

                return false;
            }

            #endregion

            #region effect

            if (PayCost || IsShowEffect())
            {
                if (!costSelected)
                {
                    if (card.IsOption || IsShowEffect())
                    {
                        // AS-IS :649-673: the `noHandCard` ShowingHandCard visibility probe + the
                        // Effects.MoveToExecuteCardEffect display move = UI, stripped (adaptation (4)).
                    }
                    else
                    {
                        if (CardEffect == null)
                        {
                            // AS-IS :679: Effects.ShrinkUpUseHandCard(Effects.ShowUseHandCard) = UI, stripped
                            // (adaptation (4)).
                        }
                    }
                }
            }

            #endregion

            #region show expected cost

            // AS-IS :688-704: computes the expected paying cost ONLY to feed
            // `GManager.instance.memoryObject.ShowMemoryPredictionLine(card.Owner.ExpectedMemory(cost))` — the
            // memory-gauge prediction overlay = UI; the whole region (including its side-effect-free
            // GetPayingCostWithBaseCost probe) is stripped (adaptation (4)). The AUTHORITATIVE cost fix happens
            // below in `#region fix cost to pay`.

            #endregion

            #region process cut in effects before paying cost

            if (skillInfos_BeforePayCost.Count >= 1)
            {
                foreach (SkillInfo skillInfo in skillInfos_BeforePayCost)
                {
                    GManager.instance.autoProcessing_CutIn.PutStackedSkill(skillInfo);
                }

                // AS-IS :745-757 `if (IsShowEffect()) targetPermanent.ShowWillEvolutionEffect();` loop —
                // WillEvolutionObject display = UI, stripped (adaptation (4)).

                await GManager.instance.autoProcessing_CutIn.TriggeredSkillProcess(false, AutoProcessing.HasExecutedSameEffect);

                // AS-IS :763-769 `targetPermanent.HideWillEvolutionEffect();` loop = UI, stripped
                // (adaptation (4)).
            }

            #endregion

            if (CardSources.Count == 1) //Do Digixros in this loop if playing 1 card as they will be needed to calculate cost, else will be done just before play
            {

                #region select DigiXros

                if (card.HasDigiXros() && !isEvolution)
                {
                    GManager.instance.GetComponent<SelectDigiXrosClass>().SetExcludedCards(CardSources);
                    await GManager.instance.GetComponent<SelectDigiXrosClass>().Select(card);
                }

                #endregion

                #region select Assembly

                if (card.HasAssembly && !isEvolution)
                {
                    // AS-IS :755-756: `GManager.instance.GetComponent<SelectAssemblyClass>().SetExcludedCards(
                    // CardSources);` + `yield return ... .Select(card);` — the AS-IS interactive Assembly
                    // material pre-selection component; the mirror SelectAssemblyClass is the STATIC
                    // feasibility half (materials ride the parameterized play action), so the component flow
                    // has no mirror: STOP RD-P6C1-5.
                    throw new NotSupportedException(
                        "STOP: Assembly pre-play material selection (AS-IS SelectAssemblyClass.Select) has no " +
                        "mirror component flow — design item RD-P6C1-5, docs/audit/rebuild_p6_cluster1_notes.md.");
                }

                #endregion

            }

            #region Bounce Tamer of Burst digivolution

            if (IsBurst(card))
            {
                // AS-IS :770-786: `yield return ... GManager.instance.selectBurstDigivolutionEffect.BounceTamer(
                // BurstTamer(card));` then the `!TamerBounced` retry (`_burstTamerFrameID = -1; SelectCost();`)
                // else `burstDigivolved = true;` — SelectBurstDigivolutionEffect (a 345-line component: the
                // tamer bounce is GAME STATE) has no mirror: STOP RD-P6C1-6. Unreachable today — IsBurst()
                // needs a burst frame id and SetBurst/BurstTamer STOP first (RD-P6C1-1).
                throw new NotSupportedException(
                    "STOP: Burst digivolution tamer bounce (AS-IS GManager.selectBurstDigivolutionEffect) has " +
                    "no mirror — design item RD-P6C1-6, docs/audit/rebuild_p6_cluster1_notes.md.");
            }

            #endregion

            #region Add Link Card of App Fusion

            if (IsAppFusion(card))
            {
                // AS-IS :792-808: `yield return ... GManager.instance.selectAppFusionEffect.AddToSources(
                // LinkedCard(card));` then the `!LinkAdded` retry (`_appFusionFrameIDs = new int[0];
                // SelectCost();`) else `appFusion = true;` — SelectAppFusionEffect (241-line component: the
                // link-card re-source is GAME STATE) has no mirror: STOP RD-P6C1-6. Unreachable today —
                // IsAppFusion() STOPs on the frame model first (RD-P6C1-1).
                throw new NotSupportedException(
                    "STOP: App-Fusion link-card sourcing (AS-IS GManager.selectAppFusionEffect) has no mirror " +
                    "— design item RD-P6C1-6, docs/audit/rebuild_p6_cluster1_notes.md.");
            }

            #endregion

            #region fix cost to pay

            int Cost = 0;

            if (PayCost)
            {
                if (!isJogress)
                {
                    Cost = card.GetPayingCostWithBaseCost(baseCost, Root, targetPermanents, checkAvailability: false, FixedCost: _fixedCost);
                    Cost = card.GetPayingCostWithBaseCost(baseCost, Root, targetPermanents, checkAvailability: false, FixedCost: _fixedCost);
                }
                else
                {
                    if (card.JogressConditionOf().Count > 0)
                    {
                        Cost = card.GetPayingCostWithBaseCost(card.JogressConditionOf()[baseDNA].cost, Root, targetPermanents, checkAvailability: false, FixedCost: _fixedCost);
                    }
                }

                // AS-IS :826 memoryObject.ShowMemoryPredictionLine(card.Owner.ExpectedMemory(Cost)) = UI,
                // stripped (adaptation (4)).
            }

            #endregion

            #region end play cards

            bool endPlayCard = false;
            bool playFailed = false;

            if (PayCost)
            {
                if (Cost > new Player(card.Context, card.Owner).MaxMemoryCost)
                {
                    endPlayCard = true;
                    playFailed = true;
                }
            }

            if (isEvolution)
            {
                if (targetPermanents != null)
                {
                    if (targetPermanents.Count >= 1)
                    {
                        foreach (Permanent permanent in targetPermanents)
                        {
                            if (permanent != null)
                            {
                                if (permanent.TopCard == null)
                                {
                                    endPlayCard = true;
                                }
                            }
                        }

                        if (!endPlayCard)
                        {
                            if (!isJogress && !IsBurst(card) && !IsAppFusion(card))
                            {
                                if (!GetIgnoreRequirement(CardEffectCommons.IgnoreRequirement.Level))
                                {
                                    // AS-IS :813: `if (!GetIgnoreRequirement(Level) && !card.
                                    // CanPlayCardTargetFrame(targetPermanents[0].PermanentFrame, PayCost,
                                    // CardEffect, root: Root, fixedCost: -1)) { endPlayCard = true; playFailed
                                    // = true; }` — needs Permanent.PermanentFrame (frame model, RD-P6C1-1) AND
                                    // the play-cost/requirement engine (RD-P6C1-2): STOP (the short-circuit on
                                    // GetIgnoreRequirement(Level) is preserved).
                                    throw new NotSupportedException(
                                        "STOP: CanPlayCardTargetFrame needs the frame model + the play-cost/" +
                                        "requirement engine — design items RD-P6C1-1/RD-P6C1-2, " +
                                        "docs/audit/rebuild_p6_cluster1_notes.md.");
                                }
                            }
                            else if (isJogress)
                            {
                                if (!card.CanJogressFromTargetPermanents(targetPermanents, PayCost))
                                {
                                    endPlayCard = true;
                                    playFailed = true;
                                }
                            }
                            else if (IsBurst(card))
                            {
                                if (!card.CanBurstDigivolutionFromTargetPermanent(targetPermanents[0], PayCost))
                                {
                                    endPlayCard = true;
                                    playFailed = true;
                                }
                            }
                            else if (IsAppFusion(card))
                            {
                                if (!card.CanAppFusionFromTargetPermanent(targetPermanents[0], PayCost))
                                {
                                    endPlayCard = true;
                                    playFailed = true;
                                }
                            }
                        }
                    }
                }
            }

            if (endPlayCard)
            {
                // AS-IS :785 PlayLog = UI (stripped, adaptation (4)).

                GManager.instance.GetComponent<SelectDigiXrosClass>().ResetSelectDigiXrosClass();
                GManager.instance.GetComponent<SelectDNACondition>().ResetSelectDNAConditionClass();

                // AS-IS :790 SelectAssemblyClass component reset — no mirror component state (see the loop-top
                // note; adaptation (7), RD-P6C1-5).

                // AS-IS :791: Effects.FailedPlayCardEffect(card) — a DOTween shake on the brainstorm hand-card
                // display (Effects.cs:2267-2306) = UI, stripped (adaptation (4)).

                if (new Player(card.Context, card.Owner).HandCards.Contains(card))
                {
                    // AS-IS :795-797: `yield return ... CardObjectController.RemoveFromAllArea(card);` +
                    // `yield return ... CardObjectController.AddHandCards(new List<CardSource>() { card },
                    // false, null);` — the failed-play hand restore; the AS-IS static zone-move helper class
                    // has no mirror: STOP RD-P6C1-8 (== cluster-2 design item RD-P6C2-1).
                    throw new NotSupportedException(
                        "STOP: failed-play hand restore needs CardObjectController.RemoveFromAllArea/" +
                        "AddHandCards — no mirror zone-move statics (design item RD-P6C1-8, " +
                        "docs/audit/rebuild_p6_cluster1_notes.md).");
                }

                // AS-IS :801 fire-and-forget OffMemoryPredictionLine() = UI, stripped (adaptation (4)).

                // AS-IS :803-809: the brainStormObject.BrainStormHandCards loop + CloseBrainstrorm — the
                // brainstorm hand display = UI, stripped (adaptation (4)).

                // AS-IS :811-821: the player.FieldPermanentObjects / fieldPermanentCard.OffPermanentIndexText()
                // loop — the jogress index-badge display = UI, stripped (adaptation (4)).

                if (playFailed)
                {
                    foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer)
                    {
                        foreach (Permanent permanent in player.GetFieldPermanents())
                        {
                            permanent.IsSuspended = permanent.oldIsTapped_playCard;
                        }
                    }

                    foreach (CardSource cardSource in oldTrashCards)
                    {
                        if (!CardEffectCommons.IsExistOnTrash(cardSource))
                        {
                            // AS-IS :843: `yield return ... CardObjectController.AddTrashCard(cardSource);` —
                            // the failed-play trash restore; STOP RD-P6C1-8.
                            throw new NotSupportedException(
                                "STOP: failed-play trash restore needs CardObjectController.AddTrashCard — no " +
                                "mirror zone-move statics (design item RD-P6C1-8, " +
                                "docs/audit/rebuild_p6_cluster1_notes.md).");
                        }
                    }
                }
            }

            #endregion

            // AS-IS :851/:961 `card.Owner.UntilCalculateFixedCostEffect = new List<Func<EffectTiming, ICardEffect>>();`
            // — (R2-C) cleared ATOMICALLY across BOTH mirror carriers: the player bucket AND the
            // EffectDuration.UntilCalculateFixedCost registry binding set (same atomic clear the live pay chokes perform).
            Headless.Effects.EffectDurationExpiry.ExpireFixedCostCalc(card.Context, card.Owner);

            if (endPlayCard)
            {
                continue;
            }

            #region pay cost

            if (PayCost)
            {
                // memory lose
                if (Cost <= new Player(card.Context, card.Owner).MaxMemoryCost)
                {
                    await card.Owner.AddMemory(-1 * Cost, null);
                }

                // AS-IS :861 fire-and-forget OffMemoryPredictionLine() = UI, stripped (adaptation (4)).
            }

            #endregion

            #region cut in effect after paying cost

            await GManager.instance.autoProcessing_CutIn.StackSkillInfos(hashtable, EffectTiming.AfterPayCost);

            // cur in effect process
            await GManager.instance.autoProcessing_CutIn.TriggeredSkillProcess(
                false,
                AutoProcessing.HasExecutedSameEffect);

            #endregion

            // add to played cards
            playedCards_fixed.Add(card);
        }

        #region filter cards

        bool isDualCardAsOption(CardSource cardSource) => cardSource.IsDigimon && cardSource.IsOption && !isEvolution;
        List<CardSource> permanentCards = playedCards_fixed.Filter(cardSource => cardSource.IsPermanent() && !isDualCardAsOption(cardSource));
        List<CardSource> optionCards = playedCards_fixed.Filter(cardSource => !cardSource.IsPermanent() || isDualCardAsOption(cardSource));

        // (the split lists + burst/appFusion/breeding flags are consumed by the AS-IS hand-off behind the STOP)
        _ = permanentCards;
        _ = optionCards;
        _ = burstDigivolved;
        _ = appFusion;
        _ = _isTapped;
        _ = _activateETB;
        _ = _showEffect;

        // AS-IS :868-960 `#region play permanent` + `#region use option` — the final hand-off:
        //     PlayPermanentClass playPermanent = new PlayPermanentClass(permanentCards, _hashtable, _targetPermanent, _isTapped, Root, _activateETB);
        //     if (isJogress) playPermanent.SetJogress(_jogressEvoRootsFrameIDs);
        //     if (burstDigivolved) playPermanent.SetBurstDigivolved();
        //     if (appFusion) playPermanent.SetAppFusion(_appFusionFrameIDs);
        //     if (_isBreedingArea) playPermanent.SetIsBreedingArea();
        //     yield return ContinuousController.instance.StartCoroutine(playPermanent.PlayPermanent());
        //     UseOptionClass useOption = new UseOptionClass(optionCards, _hashtable, Root) { _showEffect = _showEffect };
        //     yield return ContinuousController.instance.StartCoroutine(useOption.UseOption());
        // — the sibling nested CardController classes `PlayPermanentClass`/`UseOptionClass` are UNPORTED
        // (explicitly out of this port's 4-type scope; the verified headless play executors live in
        // PlayCardAction/PlayCardsBridge but do NOT match this seam — the cost was already paid above, so
        // re-entering the bridge would double-pay): STOP RD-P6C1-4.
        throw new NotSupportedException(
            "STOP: PlayCardClass.PlayCard reached the PlayPermanentClass/UseOptionClass hand-off — the sibling " +
            "AS-IS classes are unported (design item RD-P6C1-4, docs/audit/rebuild_p6_cluster1_notes.md).");

        #endregion
    }

    // AS-IS :1044-1049 `IEnumerator OffMemoryPredictionLine()` — a WaitForSeconds-delayed
    // `GManager.instance.memoryObject.OffMemoryPredictionLine()` (the memory-gauge prediction overlay) = UI,
    // stripped WITH its two fire-and-forget call sites (:801/:861) (adaptation (4)).
}

/// <summary>(P6C1) AS-IS <c>CardSource</c> members the AS-IS-verbatim play pipeline reads, bridged as
/// extensions because their AS-IS home (<c>CardSource.cs</c>) belongs to another P6 remediation cluster —
/// relocate them into the mirror <c>CardSource</c> when that file is free (design item RD-P6C1-9,
/// docs/audit/rebuild_p6_cluster1_notes.md). Two kinds:
/// <list type="bullet">
/// <item>REAL 1:1 accessors (the AS-IS property bodies verbatim over the live <c>EffectList(None)</c> scan —
/// the same shape the existing mirror <c>AppFusionConditionOf</c>/<c>AssemblyConditionOf</c> established):
/// <see cref="JogressConditionOf"/>, <see cref="BurstDigivolutionConditionOf"/>, <see cref="DigiXrosConditionOf"/>,
/// <see cref="HasDigiXros"/>, <see cref="IsPermanent"/>, <see cref="BasePlayCostFromEntity"/>.</item>
/// <item>STOP bridges for the play/digivolution cost+requirement engine (the MIG5 PLAY-COST gap — AS-IS
/// <c>EvoCosts</c>/<c>GetChangedCostItselef</c>/<c>GetChangedPayingCost</c>/requirement scans are a whole
/// unported subsystem): <see cref="CanEvolve"/>, <see cref="CostList"/>, <see cref="GetPayingCostWithBaseCost"/>,
/// <see cref="CanJogressFromTargetPermanents"/>, <see cref="CanBurstDigivolutionFromTargetPermanent"/>,
/// <see cref="CanAppFusionFromTargetPermanent"/> — design item RD-P6C1-2; they keep the AS-IS call-site text
/// verbatim and throw, never guess.</item>
/// </list></summary>
public static class CardSourceAsIsPlayAccessors
{
    /// <summary>(P6C1) AS-IS <c>CardSource.jogressCondition</c> (CardSource.cs:2707-2722) — verbatim: every
    /// usable <c>IAddJogressConditionEffect</c>'s non-null condition from this card's live effect list.</summary>
    public static List<JogressCondition> JogressConditionOf(this CardSource card)
    {
        List<JogressCondition> addJogressConditionEffect =
        card.EffectList(EffectTiming.None)
        .Filter(cardEffect => cardEffect is IAddJogressConditionEffect
            && cardEffect.CanUse(null)
            && ((IAddJogressConditionEffect)cardEffect).GetJogressCondition(card) != null)
        .Select(cardEffect => ((IAddJogressConditionEffect)cardEffect).GetJogressCondition(card))
        .ToList();

        return addJogressConditionEffect;
    }

    /// <summary>(P6C1) AS-IS <c>CardSource.burstDigivolutionCondition</c> (CardSource.cs:2987-3009) — verbatim:
    /// the first usable <c>IAddBurstDigivolutionConditionEffect</c>'s non-null condition.</summary>
    public static BurstDigivolutionCondition BurstDigivolutionConditionOf(this CardSource card)
    {
        foreach (ICardEffect cardEffect in card.EffectList(EffectTiming.None))
        {
            if (cardEffect is IAddBurstDigivolutionConditionEffect)
            {
                if (cardEffect.CanUse(null))
                {
                    BurstDigivolutionCondition burstDigivolutionCondition = ((IAddBurstDigivolutionConditionEffect)cardEffect).GetBurstDigivolutionCondition(card);

                    if (burstDigivolutionCondition != null)
                    {
                        return burstDigivolutionCondition;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>(P6C1) AS-IS <c>CardSource.digiXrosCondition</c> (CardSource.cs:2959-2981) — verbatim: the
    /// first usable <c>IAddDigiXrosConditionEffect</c>'s non-null condition.</summary>
    public static DigiXrosCondition DigiXrosConditionOf(this CardSource card)
    {
        foreach (ICardEffect cardEffect in card.EffectList(EffectTiming.None))
        {
            if (cardEffect is IAddDigiXrosConditionEffect)
            {
                if (cardEffect.CanUse(null))
                {
                    DigiXrosCondition digiXrosCondition = ((IAddDigiXrosConditionEffect)cardEffect).GetDigiXrosCondition(card);

                    if (digiXrosCondition != null)
                    {
                        return digiXrosCondition;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>(P6C1) AS-IS <c>CardSource.HasDigiXros</c> (CardSource.cs:2569) — verbatim
    /// (<c>digiXrosCondition != null</c>).</summary>
    public static bool HasDigiXros(this CardSource card) => card.DigiXrosConditionOf() != null;

    /// <summary>(P6C1) AS-IS <c>CardSource.IsPermanent</c> (CardSource.cs:3488 → CEntity_Base.cs:238):
    /// Digimon OR Tamer OR Digi-Egg (static printed card kind).</summary>
    public static bool IsPermanent(this CardSource card) => card.IsDigimon || card.IsTamer || card.IsDigiEgg;

    /// <summary>(P6C1) AS-IS <c>CardSource.BasePlayCostFromEntity</c> (CardSource.cs:757-763 —
    /// <c>_cEntity_Base.PlayCost</c>, the raw printed play cost): the mirror carrier of exactly that value is
    /// <c>CardSource.GetCostItself</c> (<c>Definition?.PlayCost ?? 0</c>).</summary>
    public static int BasePlayCostFromEntity(this CardSource card) => card.GetCostItself;

    // (R4 S3b-2) The AS-IS CardSource.CanEvolve STOP extension stub is retired — the real 1:1 instance method
    // (EvoCosts/CostList/CanEvolve, the printed+added digivolution cost engine) now lives on CardSource
    // (CardSource.cs, R4 S3b-2); `card.CanEvolve(...)` calls resolve to that instance method.

    // (R2-C) The AS-IS CardSource.CostList STOP extension stub was retired — it is now the real (still-STOP,
    // printed-EvoCost-engine RD-P6C1-2) 1:1 instance method on CardSource (CardSource.cs, R2-C).

    // (R2-C) The AS-IS CardSource.GetPayingCostWithBaseCost STOP extension stub was retired — it is now the real
    // 1:1 instance method on CardSource (CardSource.cs, R2-C). `card.GetPayingCostWithBaseCost(...)` calls here
    // resolve to that instance method.

    /// <summary>(P6C1) AS-IS <c>CardSource.CanJogressFromTargetPermanents(targetPermanents, PayCost)</c>
    /// (CardSource.cs:2846). STOP: RD-P6C1-2.</summary>
    public static bool CanJogressFromTargetPermanents(this CardSource card, List<Permanent> targetPermanents, bool PayCost)
    {
        _ = card;
        _ = targetPermanents;
        _ = PayCost;
        throw new NotSupportedException(
            "STOP: CardSource.CanJogressFromTargetPermanents (AS-IS CardSource.cs:2846) — the AS-IS jogress " +
            "requirement/cost check has no mirror (design item RD-P6C1-2, docs/audit/rebuild_p6_cluster1_notes.md).");
    }

    /// <summary>(P6C1) AS-IS <c>CardSource.CanBurstDigivolutionFromTargetPermanent(targetPermanent, PayCost)</c>
    /// (CardSource.cs:3211). STOP: RD-P6C1-2.</summary>
    public static bool CanBurstDigivolutionFromTargetPermanent(this CardSource card, Permanent targetPermanent, bool PayCost)
    {
        _ = card;
        _ = targetPermanent;
        _ = PayCost;
        throw new NotSupportedException(
            "STOP: CardSource.CanBurstDigivolutionFromTargetPermanent (AS-IS CardSource.cs:3211) — the AS-IS " +
            "burst-digivolution requirement/cost check has no mirror (design item RD-P6C1-2, " +
            "docs/audit/rebuild_p6_cluster1_notes.md).");
    }

    /// <summary>(P6C1) AS-IS <c>CardSource.CanAppFusionFromTargetPermanent(targetPermanent, PayCost, root)</c>
    /// (CardSource.cs:3378). STOP: RD-P6C1-2.</summary>
    public static bool CanAppFusionFromTargetPermanent(this CardSource card, Permanent targetPermanent, bool PayCost, SelectCardEffect.Root root = SelectCardEffect.Root.Hand)
    {
        _ = card;
        _ = targetPermanent;
        _ = PayCost;
        _ = root;
        throw new NotSupportedException(
            "STOP: CardSource.CanAppFusionFromTargetPermanent (AS-IS CardSource.cs:3378) — the AS-IS " +
            "app-fusion requirement/cost check has no mirror (design item RD-P6C1-2, " +
            "docs/audit/rebuild_p6_cluster1_notes.md).");
    }
}


/// <summary>AS-IS <c>IBattle</c> (CardController.cs:4427) — the per-battle context holder.</summary>
public class IBattle
{
    public IBattle(Permanent AttackingPermanent, Permanent DefendingPermanent, CardSource DefendingCard, bool IsWithoutAttack = false)
    {
        this.AttackingPermanent = AttackingPermanent;
        this.DefendingPermanent = DefendingPermanent;
        this.DefendingCard = DefendingCard;
        this.IsWithoutAttack = IsWithoutAttack;
    }

    public Permanent AttackingPermanent { get; private set; } = null;
    public Permanent DefendingPermanent { get; private set; } = null;
    CardSource DefendingCard { get; set; } = null;
    bool IsWithoutAttack { get; set; } = false;
    public Hashtable hashtable { get; set; } = new Hashtable();

    public Permanent enemyPermanent(Permanent permanent)
    {
        if (permanent != null)
        {
            if (permanent == AttackingPermanent)
            {
                return DefendingPermanent;
            }
            else if (permanent == DefendingPermanent)
            {
                return AttackingPermanent;
            }
        }

        return null;
    }

    public int CompareStats()
    {
        int statCheck = 0;

        if (AttackingPermanent.HasIceclad || DefendingPermanent.HasIceclad)
            statCheck = AttackingPermanent.DigivolutionCards.Count - DefendingPermanent.DigivolutionCards.Count;
        else
            statCheck = AttackingPermanent.DP - DefendingPermanent.DP;

        // ADAPTATION: AS-IS `Mathf.Clamp(int, -1, 1)` (UnityEngine) — System.Math.Clamp is the established
        // mirror substitute (identical int semantics; Player.cs MaxMemoryCost precedent).
        statCheck = Math.Clamp(statCheck, -1, 1);

        return statCheck;
    }
}

#region Destroy permanents

/// <summary>
/// (R3-A) 1:1 mirror of AS-IS <c>DestroyPermanentsClass</c> (CardController.cs:3648-3874): the effect/rule
/// deletion pipeline over a list of permanents. Ctor takes the target list + the causing <c>Hashtable</c>
/// (from which the LIVE causing <c>ICardEffect</c>, <c>IBattle</c>, and DP-zero flag are read exactly as AS-IS —
/// <see cref="CardEffectCommons.GetCardEffectFromHashtable"/> / <see cref="CardEffectCommons.GetBattleFromHashtable"/>
/// / <see cref="CardEffectCommons.IsDPZeroDelete"/>). <see cref="Destroy"/> performs, in AS-IS order: filter by the
/// causing effect's per-target immunity (<c>!TopCard.CanNotBeAffected(cardEffect)</c> AND
/// <c>CanBeDestroyedBySkill(cardEffect)</c> — this is the RD-R2-02 resolution: the real ICardEffect is threaded into
/// those getters, no more source-id-only cause seam) → mark willBeRemoveField on all → PRE cut-in windows
/// (WhenPermanentWouldBeDeleted / WhenRemoveField, the owner's would-be-deleted replacements: Evade/Fragment/Decoy/…)
/// → fix survivors (willBeRemoveField still true) → POST windows (OnDestroyedAnyone / OnLeaveFieldAnyone) → record
/// the "just before deletion" parameters → per-permanent trash (DiscardEvoRoots the sources, RemoveField the top off
/// the field, AddTrashCard the top).
///
/// Substrate/ADAPTATION: IEnumerator→Task; ShowDeleteEffect/HideDeleteEffect/ShrinkSecurityDigimonDisplay/
/// ShowCardEffect/DestroyPermanentEffect/PlayLog = UI (stripped). The AS-IS
/// <c>autoProcessing_CutIn.HasAwaitingActivateEffects()</c> gate is a private method on the AutoProcessing surface
/// (owned by the parallel R3-B batch — read-only here), so it is inlined verbatim over the public
/// <c>StackedSkillInfos</c> (AS-IS AutoProcessing.cs:750-766); when R3-B lands HasAwaitingActivateEffects the call
/// can replace this local. Window drive uses the existing mirror <c>autoProcessing</c>/<c>autoProcessing_CutIn</c>
/// surface (R3-B's home); the cut-in DRAIN (TriggeredSkillProcess) still routes to R3-B's MultipleSkills STOP
/// (RD-P6C1-3) for a batch that actually stacked a would-be-deleted replacement — an existing R3-B design item, not
/// a new one here. NOTE (R3-A cutover): the LIVE effect-delete path is still MatchStateMutationSink.ApplyDelete —
/// routing the sink's Delete emission through this class (bigbang §3-R2, item 4) is blocked until R3-B lands the
/// cut-in drain (else every replacement-window deletion would throw) and is tracked as design item RD-R3-01.
/// </summary>
public class DestroyPermanentsClass
{
    public DestroyPermanentsClass(List<Permanent> destroyTargetPermanents, Hashtable hashtable, bool notShowCards = false)
    {
        _destroytargetPermanents = destroyTargetPermanents.Clone();
        _hashtable = hashtable;
        _notShowCards = notShowCards;
    }

    public bool IsDestroyed(Permanent permanent)
    {
        return DestroyedPermanents.Contains(permanent);
    }

    List<Permanent> _destroytargetPermanents = new List<Permanent>();
    public List<Permanent> DestroyedPermanents { get; private set; } = new List<Permanent>();
    Hashtable _hashtable = null;
    bool _notShowCards = false;

    public async Task Destroy(CancellationToken cancellationToken = default)
    {
        if (_destroytargetPermanents == null) return;

        ICardEffect cardEffect = CardEffectCommons.GetCardEffectFromHashtable(_hashtable);
        IBattle battle = CardEffectCommons.GetBattleFromHashtable(_hashtable);
        bool isDPZero = CardEffectCommons.IsDPZeroDelete(_hashtable);

        _destroytargetPermanents = _destroytargetPermanents.Filter(permanent =>
        permanent != null
        && permanent.TopCard != null
        && (cardEffect == null ||
        (!permanent.TopCard.CanNotBeAffected(cardEffect)
        && permanent.CanBeDestroyedBySkill(cardEffect))));

        if (_destroytargetPermanents.Count == 0) return;

        _destroytargetPermanents.ForEach(permanent => permanent.willBeRemoveField = true);

        #region cut in effect

        // "When permanents would be deleted" effect

        await GManager.instance.autoProcessing_CutIn.StackSkillInfos(
            CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(
                _destroytargetPermanents,
                cardEffect,
                battle
            ),
            EffectTiming.WhenPermanentWouldBeDeleted).ConfigureAwait(false);

        // "When permanents would remove field" effect
        await GManager.instance.autoProcessing_CutIn.StackSkillInfos(
            CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(
                _destroytargetPermanents,
                cardEffect,
                battle
            ),
            EffectTiming.WhenRemoveField).ConfigureAwait(false);

        if (HasAwaitingActivateEffects(GManager.instance.autoProcessing_CutIn))
        {
            // AS-IS ShowDeleteEffect / ShrinkSecurityDigimonDisplay / HideDeleteEffect = UI (stripped).

            // cut in effect process
            await GManager.instance.autoProcessing_CutIn.TriggeredSkillProcess(false, null).ConfigureAwait(false);
        }

        #endregion

        // fix delete target permanents
        List<Permanent> destroyTargetPermanents_Fixed = _destroytargetPermanents.Filter(permanent =>
            permanent != null
            && permanent.TopCard != null
            && permanent.willBeRemoveField);

        #region "When permanents are deleted" effect

        await GManager.instance.autoProcessing
            .StackSkillInfos(CardEffectCommons.OnDeletionHashtable(
                destroyTargetPermanents_Fixed,
                cardEffect,
                battle,
                isDPZero
                ),
                EffectTiming.OnDestroyedAnyone).ConfigureAwait(false);

        #endregion

        #region "When permanents leave the battle area" effect

        await GManager.instance.autoProcessing.StackSkillInfos(
            CardEffectCommons.OnDeletionHashtable(
                destroyTargetPermanents_Fixed,
                cardEffect,
                battle,
                isDPZero
            ),
            EffectTiming.OnLeaveFieldAnyone).ConfigureAwait(false);

        #endregion

        #region record parameters just before deletion

        foreach (Permanent permanent in destroyTargetPermanents_Fixed)
        {
            permanent.DPJustBeforeRemoveField = permanent.DP;

            if (permanent.TopCard.HasLevel)
            {
                permanent.LevelJustBeforeRemoveField = permanent.Level;
            }

            if (permanent.TopCard.HasPlayCost)
            {
                permanent.CostJustBeforeRemoveField = permanent.TopCard.GetCostItself;
            }

            permanent.CardNamesJustBeforeRemoveField = new List<string>(permanent.TopCard.CardNames);
            permanent.CardTraitsJustBeforeRemoveField = new List<string>(permanent.TopCard.CardTraits);

            foreach (CardSource cardSource in permanent.cardSources)
            {
                cardSource.PermanentJustBeforeRemoveField = permanent;
            }
        }

        #endregion

        // AS-IS "add log" (:3787-3805) + "show cards" (:3807-3816) = UI / PlayLog (stripped).

        #region trash permanent cards

        foreach (Permanent permanent in destroyTargetPermanents_Fixed)
        {
            #region record wheter to be deleted by battle

            if (battle != null)
            {
                if (permanent.TopCard != null)
                {
                    if (CardEffectCommons.GetLoserPermanentsFromHashtable(battle.hashtable).Contains(permanent))
                        permanent.IsDestroyedByBattle = true;
                }
            }

            #endregion

            #region record used effect

            if (permanent.TopCard != null)
            {
                permanent.DestroyingEffect = cardEffect;
            }

            #endregion

            // AS-IS DestroyPermanentEffect (:3844) = UI (stripped).

            await permanent.DiscardEvoRoots(cancellationToken: cancellationToken).ConfigureAwait(false);

            CardSource topCard = permanent.TopCard;

            await CardObjectController.RemoveField(permanent, cancellationToken: cancellationToken).ConfigureAwait(false);

            await CardObjectController.AddTrashCard(topCard, cancellationToken).ConfigureAwait(false);

            DestroyedPermanents.Add(permanent);
        }

        #endregion

        #region hide icon

        foreach (Permanent permanent in _destroytargetPermanents)
        {
            if (permanent != null)
            {
                if (permanent.TopCard != null)
                {
                    permanent.willBeRemoveField = false;
                }
            }
        }

        #endregion
    }

    // (R3-A) AS-IS AutoProcessing.HasAwaitingActivateEffects (AutoProcessing.cs:750-766), inlined verbatim over the
    // public StackedSkillInfos because the AS-IS method lives on the R3-B-owned AutoProcessing surface (read-only).
    private static bool HasAwaitingActivateEffects(AutoProcessing autoProcessing)
    {
        if (autoProcessing.StackedSkillInfos.Count >= 2)
        {
            return true;
        }
        else if (autoProcessing.StackedSkillInfos.Count == 1)
        {
            if (autoProcessing.StackedSkillInfos[0].CardEffect.CanActivate(autoProcessing.StackedSkillInfos[0].Hashtable))
            {
                return true;
            }
        }

        return false;
    }
}

#endregion
}
