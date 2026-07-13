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

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

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
    public IDiscardHands(List<IDiscardHand> discardHands, HeadlessEntityId? causeEffectSourceId)
    {
        foreach (IDiscardHand discardHand in discardHands)
        {
            this.discardHands.Add(discardHand);
        }

        _causeEffectSourceId = causeEffectSourceId;
    }

    List<IDiscardHand> discardHands { get; set; } = new List<IDiscardHand>();
    readonly HeadlessEntityId? _causeEffectSourceId;
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
    public DrawClass(EngineContext context, HeadlessPlayerId playerId, int drawCount, HeadlessEntityId? causeEffectSourceId)
    {
        _context = context;
        _playerId = playerId;
        _drawCount = drawCount;
        _causeEffectSourceId = causeEffectSourceId;
    }

    readonly EngineContext _context;
    readonly HeadlessPlayerId _playerId;
    readonly int _drawCount;
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
    public IAddTrashCardsFromLibraryTop(EngineContext context, HeadlessPlayerId playerId, int addTrashCount, HeadlessEntityId? causeEffectSourceId)
    {
        _context = context;
        _playerId = playerId;
        _addTrashCount = addTrashCount;
        _causeEffectSourceId = causeEffectSourceId;
    }

    public void SetNotShowCards()
    {
        _notShowCards = true;
    }

    readonly EngineContext _context;
    readonly HeadlessPlayerId _playerId;
    readonly int _addTrashCount;
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

        await new ITrashDeckCards(discardedCards, _causeEffectSourceId).TrashDeckCards(cancellationToken).ConfigureAwait(false);

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
                    if (_permanent.DigivolutionCards.Some((cardSource) => !cardSource.CanNotTrashFromDigivolutionCards(CauseEffectSourceId)))
                    {
                        return true;
                    }
                }
                else
                {
                    if (_permanent.DigivolutionCards.Count((cardSource) => !cardSource.CanNotTrashFromDigivolutionCards(CauseEffectSourceId)) >= _digiBurstCount)
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
                        canTargetCondition: (cardSource) => !cardSource.CanNotTrashFromDigivolutionCards(CauseEffectSourceId),
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

                #endregion

                // trash digivolution cards (AS-IS :2233; ICardEffect -> cause id, the mirror carrier's shape).
                await new ITrashDigivolutionCards(_permanent, selectedCards, CauseEffectSourceId).TrashDigivolutionCards().ConfigureAwait(false);

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

    // AS-IS Permanent.ImmuneFromStackTrashing(_cardEffect) — the same ImmuneStackTrashingKey restriction scan
    // ITrashDigivolutionCards applies (self-contained-privates style; the continuous scan needs a cause —
    // without one nothing matches a causing-effect-keyed immunity, as in the AS-IS null-cause scan).
    private bool ImmuneFromStackTrashing()
    {
        if (CauseEffectSourceId is not { IsEmpty: false } causeId)
        {
            return false;
        }

        EngineContext context = _permanent.TopCard.Context;
        return RestrictionScan.IsRestricted(context, MatchStateMutationSink.ImmuneStackTrashingKey, _permanent.InstanceId, causeId);
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

    public IDestroySecurity(EngineContext context, HeadlessPlayerId playerId, int destroySecurityCount, HeadlessEntityId? causeEffectSourceId, bool fromTop)
    {
        _context = context;
        _playerId = playerId;
        _destroySecurityCount = destroySecurityCount;
        _causeEffectSourceId = causeEffectSourceId;
        _trashMode = fromTop ? TrashMode.TopSecurity : TrashMode.BottomSecurity;
    }

    public IDestroySecurity(EngineContext context, HeadlessPlayerId playerId, CardSource card, HeadlessEntityId? causeEffectSourceId)
    {
        _context = context;
        _playerId = playerId;
        _destroySecurityCount = 1;
        _causeEffectSourceId = causeEffectSourceId;
        _trashMode = TrashMode.SelectedCard;
        _selectedCard = card;
    }

    readonly EngineContext _context;
    readonly HeadlessPlayerId _playerId;
    readonly int _destroySecurityCount;
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
                await new IReduceSecurity(_context, _playerId, refCollector: null, _causeEffectSourceId)
                    .ReduceSecurity(cancellationToken).ConfigureAwait(false);

                // AS-IS :4369-4377 "[When security cards are trashed]" (OnDiscardSecurity) — zone-derived from
                // the same moves (batch-collapsed); a manual emit here would double-fire (sink precedent).
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
    public IDegeneration(Permanent permanent, int degenerationCount, HeadlessEntityId? causeEffectSourceId, bool? degenerationCountRuling = null)
    {
        _permanent = permanent;
        _degenerationCount = degenerationCount;
        _causeEffectSourceId = causeEffectSourceId;
        _degenerationCountRuling = degenerationCountRuling;
    }

    Permanent _permanent = null!;
    int _degenerationCount;
    readonly HeadlessEntityId? _causeEffectSourceId;
    readonly bool? _degenerationCountRuling;

    public async Task Degeneration(CancellationToken cancellationToken = default)
    {
        // AS-IS :4803-4804 `_cardEffect == null` / `EffectSourceCard == null` — mandatory causing effect.
        if (_causeEffectSourceId is not { IsEmpty: false } causeId) return;
        if (_permanent == null) return; // AS-IS :4805
        if (_permanent.TopCard == null) return; // AS-IS :4806 — structurally dead, kept for 1:1 shape.

        EngineContext context = _permanent.TopCard.Context;

        if (ImmuneFromDeDigivolve(context, _permanent.InstanceId)) return; // AS-IS :4807

        // AS-IS :4808 ImmuneFromStackTrashing(_cardEffect).
        if (RestrictionScan.IsRestricted(context, MatchStateMutationSink.ImmuneStackTrashingKey, _permanent.InstanceId, causeId)) return;

        // AS-IS :4809 TopCard.CanNotBeAffected(_cardEffect).
        if (ContinuousImmunityGate.BlocksOpponentEffect(context.EffectRegistry, context.CardInstanceRepository, _permanent.InstanceId, causeId, context)) return;

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
    public IMassDegeneration(List<Permanent> permanents, int degenerationCount, HeadlessEntityId? causeEffectSourceId, bool? degenerationCountRuling = null)
    {
        _permanents = permanents;
        _degenerationCount = degenerationCount;
        _causeEffectSourceId = causeEffectSourceId;
        _degenerationCountRuling = degenerationCountRuling; // ctor parity; unread (dead count-select region).
    }

    readonly List<Permanent> _permanents;
    readonly int _degenerationCount;
    readonly HeadlessEntityId? _causeEffectSourceId;
#pragma warning disable CS0414 // AS-IS dead field (count-select region commented out) — kept for ctor/field parity.
    readonly bool? _degenerationCountRuling;
#pragma warning restore CS0414

    public async Task Degeneration(CancellationToken cancellationToken = default)
    {
        if (_causeEffectSourceId is not { IsEmpty: false } causeId) return; // AS-IS :4970-4971

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
            && !RestrictionScan.IsRestricted(context, MatchStateMutationSink.ImmuneStackTrashingKey, permanent.InstanceId, causeId)
            && !ContinuousImmunityGate.BlocksOpponentEffect(context.EffectRegistry, context.CardInstanceRepository, permanent.InstanceId, causeId, context);

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
    public ITrashDigivolutionCards(Permanent permanent, List<CardSource> trashTargetCards, HeadlessEntityId? causeEffectSourceId)
    {
        _permanent = permanent;
        _trashTargetCards = trashTargetCards is null ? null : new List<CardSource>(trashTargetCards);
        _causeEffectSourceId = causeEffectSourceId;
    }

    public bool IsTrashed(CardSource cardSource) => TrashedCards.Any(trashed => trashed.InstanceId == cardSource.InstanceId);

    Permanent _permanent = null!;
    List<CardSource>? _trashTargetCards;
    public List<CardSource> TrashedCards { get; } = new();
    readonly HeadlessEntityId? _causeEffectSourceId;

    public async Task TrashDigivolutionCards(CancellationToken cancellationToken = default)
    {
        if (_trashTargetCards == null) return; // AS-IS :5150
        if (_causeEffectSourceId is not { IsEmpty: false } causeId) return; // AS-IS :5151
        if (_permanent == null) return; // AS-IS :5152
        if (_permanent.TopCard == null) return; // AS-IS :5153 — structurally dead, kept for 1:1 shape.

        EngineContext context = _permanent.TopCard.Context;

        // AS-IS :5154 ImmuneFromStackTrashing(_cardEffect).
        if (RestrictionScan.IsRestricted(context, MatchStateMutationSink.ImmuneStackTrashingKey, _permanent.InstanceId, causeId)) return;

        // AS-IS :5155 TopCard.CanNotBeAffected(_cardEffect).
        if (ContinuousImmunityGate.BlocksOpponentEffect(context.EffectRegistry, context.CardInstanceRepository, _permanent.InstanceId, causeId, context)) return;

        if (_permanent.HasNoDigivolutionCards) return; // AS-IS :5156

        // AS-IS :5158-5160 membership + CanNotTrashFromDigivolutionCards protection filter.
        List<CardSource> hostSources = _permanent.DigivolutionCards.ToList();
        _trashTargetCards = _trashTargetCards
            .Where(cs => hostSources.Any(s => s.InstanceId == cs.InstanceId) && !CanNotTrashFromDigivolutionCards(context, causeId, cs.InstanceId))
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

    // AS-IS CardSource.CanNotTrashFromDigivolutionCards = the per-source stamp OR the continuous scan
    // (self-contained duplicate of DigivolutionStackHelpers' private IsTrashProtected).
    private static bool CanNotTrashFromDigivolutionCards(EngineContext context, HeadlessEntityId causeEffectSourceId, HeadlessEntityId sourceId)
    {
        bool stamped = context.CardInstanceRepository.TryGetInstance(sourceId, out CardInstanceRecord? record) && record is not null
            && record.Metadata.TryGetValue(CardEffectCommons.CardEffectCommons.TrashProtectedKey, out object? raw) && raw is true;
        return stamped || TrashProtectionScan.IsProtected(context.EffectRegistry, context.CardInstanceRepository, context, sourceId, causeEffectSourceId);
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

    public ITrashLinkCards(Permanent permanent, List<CardSource> trashTargetCards, HeadlessEntityId? causeEffectSourceId)
    {
        _permanent = permanent;

        _trashTargetCards = trashTargetCards is null ? null : new List<CardSource>(trashTargetCards);

        _causeEffectSourceId = causeEffectSourceId;
    }

    public bool IsTrashed(CardSource cardSource)
    {
        return TrashedLinkCards.Any(trashed => trashed.InstanceId == cardSource.InstanceId);
    }

    Permanent _permanent = null!;
    List<CardSource>? _trashTargetCards = new();
    public List<CardSource> TrashedLinkCards = new();
    readonly HeadlessEntityId? _causeEffectSourceId;

    public async Task TrashLinkCards(CancellationToken cancellationToken = default)
    {
        if (_trashTargetCards == null) return;
        if (_permanent == null) return;
        if (_permanent.TopCard == null) return;
        // AS-IS :5268 `_cardEffect != null && TopCard.CanNotBeAffected(_cardEffect)` — the S2 immunity gate,
        // keyed on the causing effect's source (null cause = rules trash, never blocked).
        if (_causeEffectSourceId is { } causeId && !causeId.IsEmpty)
        {
            EngineContext context0 = _permanent.TopCard.Context;
            if (ContinuousImmunityGate.BlocksOpponentEffect(
                context0.EffectRegistry, context0.CardInstanceRepository, _permanent.InstanceId, causeId, context0))
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
    public ReturnToLibraryBottomDigivolutionCardsClass(Permanent permanent, List<CardSource> cardSources, HeadlessEntityId? causeEffectSourceId)
    {
        _permanent = permanent;
        _cardSources = cardSources is null ? null : new List<CardSource>(cardSources);
        _causeEffectSourceId = causeEffectSourceId;
    }

    Permanent _permanent = null!;
    List<CardSource>? _cardSources;
    readonly HeadlessEntityId? _causeEffectSourceId;

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
        if (_causeEffectSourceId is { IsEmpty: false } causeId
            && ContinuousImmunityGate.BlocksOpponentEffect(context.EffectRegistry, context.CardInstanceRepository, _permanent.InstanceId, causeId, context))
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
    public IReduceSecurity(EngineContext context, HeadlessPlayerId playerId, List<PendingSecurityTrigger>? refCollector, HeadlessEntityId? causeEffectSourceId)
    {
        _context = context;
        _playerId = playerId;
        _refCollector = refCollector;
        _causeEffectSourceId = causeEffectSourceId;
    }

    readonly EngineContext _context;
    readonly HeadlessPlayerId _playerId;
    readonly List<PendingSecurityTrigger>? _refCollector;
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
        // AS-IS :5489 StackSkillInfos(hashtable, OnAddSecurity) {Player, CardSources} — zone-derived from the
        // caller's ->Security move (per-card add-security batch id); no manual emit (double-fire).

        // AS-IS :5494 `if (!_cardSource.IsFlipped)` — the face-up half, sole source.
        if (SecurityFaceState.IsFaceUpInSecurity(context, _cardSource.InstanceId))
        {
            var hashtable = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["playerId"] = _player.Value,
                ["cardSourceIds"] = new[] { _cardSource.InstanceId.Value },
            };
            TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnFaceUpSecurityIncreased, actor: _player, subject: _cardSource.InstanceId, extraMetadata: hashtable);
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

    public SuspendPermanentsClass(List<Permanent> permanents, HeadlessEntityId? causeEffectSourceId, bool isBlock)
    {
        _permanents = permanents;
        _causeEffectSourceId = causeEffectSourceId;
        _isBlock = isBlock;
    }

    public bool IsSuspended(Permanent permanent) => SuspendedPermanents.Any(p => p.InstanceId == permanent.InstanceId);

    readonly List<Permanent> _permanents;
    public List<Permanent> SuspendedPermanents { get; } = new();
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

                    if (ContinuousRestrictionGate.EvaluateSuspend(context, permanent.InstanceId).IsRestricted) return false; // !CanSuspend

                    if (_causeEffectSourceId is { IsEmpty: false } cause)
                    {
                        if (ContinuousImmunityGate.BlocksOpponentEffect(context.EffectRegistry, context.CardInstanceRepository, permanent.InstanceId, cause, context))
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
    public IUnsuspendPermanents(List<Permanent> permanents, HeadlessEntityId? causeEffectSourceId)
    {
        // AS-IS :5665 `permanents.Clone().Filter(CardEffectCommons.IsPermanentExistsOnBattleArea)`.
        _permanents = (permanents ?? new List<Permanent>())
            .Where(p => CardEffectCommons.CardEffectCommons.IsPermanentExistsOnBattleArea(p))
            .ToList();
        _causeEffectSourceId = causeEffectSourceId;
    }

    readonly List<Permanent> _permanents;
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
            && (_causeEffectSourceId is not { IsEmpty: false } cause
                || !ContinuousImmunityGate.BlocksOpponentEffect(context.EffectRegistry, context.CardInstanceRepository, permanent.InstanceId, cause, context));

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
    public ITrashDeckCards(List<CardSource> cardSources, HeadlessEntityId? causeEffectSourceId)
    {
        this.cardSources = new List<CardSource>();

        foreach (CardSource cardSource in cardSources)
        {
            this.cardSources.Add(cardSource);
        }

        _causeEffectSourceId = causeEffectSourceId;
    }

    List<CardSource> cardSources { get; set; } = new List<CardSource>();
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
    public ITrashStack(Permanent permanent, int trashCount, HeadlessEntityId? causeEffectSourceId, bool fromTop = true)
    {
        _permanent = permanent;
        _trashCount = trashCount;
        _causeEffectSourceId = causeEffectSourceId;
        _fromTop = fromTop; // AS-IS :5867/5872 — accepted, never branched on (dead param, preserved).
    }

    Permanent _permanent = null!;
    int _trashCount;
    readonly HeadlessEntityId? _causeEffectSourceId;
#pragma warning disable CS0414 // AS-IS dead field (fromTop stored, never read in the AS-IS loop either).
    readonly bool _fromTop;
#pragma warning restore CS0414

    public async Task TrashStack(CancellationToken cancellationToken = default)
    {
        if (_causeEffectSourceId is not { IsEmpty: false } causeId) return; // AS-IS :5882-5883
        if (_permanent == null) return; // AS-IS :5884
        if (_permanent.TopCard == null) return; // AS-IS :5885 — structurally dead, kept for 1:1 shape.

        EngineContext context = _permanent.TopCard.Context;

        if (RestrictionScan.IsRestricted(context, MatchStateMutationSink.ImmuneStackTrashingKey, _permanent.InstanceId, causeId)) return; // AS-IS :5886
        if (ContinuousImmunityGate.BlocksOpponentEffect(context.EffectRegistry, context.CardInstanceRepository, _permanent.InstanceId, causeId, context)) return; // AS-IS :5887

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

        #endregion

        // AS-IS :5962-5981 add log (gated on selectedCards.Count >= 1) = UI (stripped).
    }
}

#endregion
