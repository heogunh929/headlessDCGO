namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using BareCauseEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.BareCauseEffect;
using CardEffectCommons = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;
using CardSource = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardSource;
using EffectTiming = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.EffectTiming;
using ICardEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.ICardEffect;
using OnEnterFieldHashtableParams = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.OnEnterFieldHashtableParams;
using Permanent = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent;
using SelectCardEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect;

// ============================================================================================================
// (R3-W1b, batch W2) SkillInfo window SUPPLY layer — DORMANT.
//
// GOAL. AS-IS game code opens a trigger window by calling `autoProcessing.StackSkillInfos(hashtable, timing)`
// at the emit point, passing an AS-IS `Hashtable` payload it constructs INLINE (or via a
// CardEffectCommons.<Timing>Hashtable builder). The port instead emits GameEvents (metadata dictionaries)
// onto GameEventQueue / ZoneMover.Events, and the LIVE window seeds itself from those via
// WindowResolverWiring.CollectUnifiedSeed (registry-backed TimingWindowTriggers). At cutover (batch C),
// GameFlowProcessor.AutoProcessAsync's seed step must instead feed the MIRROR AutoProcessing: convert each
// drained GameEvent into the AS-IS (Hashtable, EffectTiming) pair and call the mirror
// AutoProcessing.StackSkillInfos — with GameEventQueue retained purely as emit-transport substrate
// (approved ADAPTATION A4). This file is that CONVERTER + the A3 minimum-batch feeder.
//
// DORMANT. No live seam is rewired by this file (cutover is batch C). The only callers are the W2 tests.
// It references NOTHING that mutates state — it reads a GameEvent + reconstructs a transient Permanent view
// (the same (context, instanceId, ownerId) bridge HashtableSetting.cs already uses) and calls a mirror
// HashtableSetting builder, so it is a pure translation with no side effects.
//
// FAITHFULLY-HANDLED SET (see docs/audit/window_supply_correspondence_2026-07-15.md for the full table). A
// timing is HANDLED only when BOTH hold: (1) the AS-IS StackSkillInfos payload is built by a builder that
// EXISTS in the mirror HashtableSetting.cs (so byte-identical keys are reused, never synthesised), and
// (2) every argument that builder needs is reconstructable from the GameEvent (subject id + owner + zones +
// metadata). Today that is the ATTACK family, whose AS-IS payload is
// `CardEffectCommons.OnAttackCheckHashtableOfPermanent(AttackingPermanent, attackEffect)`
// (AttackProcess.cs:98-99): OnAllyAttack, OnCounterTiming, OnEndAttack. attackEffect is null for a plain
// declared attack (AttackProcess.SetUp passes attackEffect=null); an EFFECT-driven attack carries only the
// cause id (`attackCauseEffectId`), NOT the live ICardEffect, so that remains a GAP (design item RDW-05).
//
// Every OTHER emitted timing is UNHANDLED here (returns false from TryBuildHashtable) with a NAMED design
// item, because its AS-IS payload is built INLINE at the emit point with keys the mirror HashtableSetting
// does NOT yet expose (RDW-02 …), or requires a PRE-removal snapshot the post-move event no longer carries
// (RDW-01 deletion), or carries only an id where AS-IS holds a live object (RDW-05). The C batch resolves
// each either by mirroring the inline builder into HashtableSetting or by re-positioning the StackSkillInfos
// call to the AS-IS emit position (where the live payload is still in scope). Guessing a payload is FORBIDDEN
// (the no-simplification rule): an unhandled timing is left unhandled, loudly, not fabricated.
// ============================================================================================================

/// <summary>One AS-IS <c>StackSkillInfos(hashtable, timing)</c> supply call reconstructed from a drained
/// <see cref="GameEvent"/> — the (Hashtable, EffectTiming) pair the mirror <c>AutoProcessing.StackSkillInfos</c>
/// consumes, plus the cross-batch sequencing id (A3) and the source event for diagnostics.</summary>
public readonly record struct SkillWindowSupplyEntry(
    EffectTiming Timing,
    Hashtable? Hashtable,
    long? BatchId,
    GameEvent SourceEvent);

/// <summary>(R3-W1b W2) DORMANT GameEvent → (Hashtable, EffectTiming) converter + A3 minimum-batch feeder for
/// the SkillInfo trigger window. See the file header and the correspondence table for the handled set and the
/// GAP design items.</summary>
public static class SkillWindowSupply
{
    // ----- design-item names for the UNHANDLED timings (recorded, never a literal lint token) ---------------
    // RDW-01  OnDestroyedAnyone / OnLeaveFieldAnyone — OnDeletionHashtable needs the PRE-removal per-permanent
    //         snapshot (TopCard/cardSources/CardNames/…) plus battle + isDPZero + cardEffect, none carried by
    //         the post-move CardMoved deletion event (AS-IS CardController.cs:3736-3756 builds it before the
    //         card leaves the field: the "collect-before-removal" requirement from design §5.5 F2r).
    // RDW-02  Inline-hashtable timings NOT reconstructable from the event alone (a live payload member is only
    //         carried as an id, or not carried at all):
    //         OnDiscardHand/Security/Library, OnAddHand, OnUseOption, OnDraw, OnLoseSecurity, OnAddSecurity,
    //         OnFaceUpSecurityIncreased, OnTappedAnyone/OnUnTappedAnyone, WhenTopCardTrashed,
    //         OnReturnCardsToHandFromTrash, OnDigivolutionCardDiscarded/ReturnToDeckBottom, OnLinkCardDiscarded,
    //         OnAddDigivolutionCards, WhenLinked, OnStartBattle, OnEndBattle, OnUseDigiburst — the C batch
    //         re-positions the StackSkillInfos call to the AS-IS position (where the live payload is in scope)
    //         rather than event-conversion (design §5.5 W2). NOTE (C1): the batch-C1 R/P inserts already landed
    //         many of these directly on the mirror routines (WhenTopCardTrashed, OnUseDigiburst, OnAddSecurity,
    //         OnFaceUpSecurityIncreased, OnAddHand); the rest stayed as design item RD-C1-CARDEFFECT-IDTHREAD
    //         (the mirror substrate threads a cause-id, not the live ICardEffect the AS-IS {"CardEffect"} member
    //         needs — the RDW-05-class id->live-effect resolution the C2 flip must close).
    //   HANDLED here without any of the above blockers (C1 W2 extension): OnMove ({ "Permanent" }) and
    //   OnReturnCardsToLibraryFromTrash ({ "CardSources" }) — both fully reconstructable from (subject id +
    //   owner), no CardEffect member, no builder needed (see TryBuildOnMove / TryBuildReturnCardsToLibraryFromTrash).
    // RDW-07  Turn/phase-boundary timings (OnStartTurn / OnStartMainPhase / OnEndTurn) — AS-IS passes a NULL
    //         hashtable (payload-free), so conversion would be trivial ((null, timing)); but TriggerTimingMap.Derive
    //         does NOT derive these strings from the port's turn StateChanged emits (MetadataActionProcessor
    //         :1022/:872/:910) — they surface only as "StateChanged". The C batch must enrich those emits with an
    //         explicit-timing override (AutoProcessingTriggerCollector.TriggerTimingKey) before the converter can
    //         see them; left unhandled here rather than guessing an emit shape.
    // RDW-03  OnSecurityCheck — inline `{ "AttackingPermanent", "Card" }` (CardController.cs:3948-3952) drawn
    //         from ATTACK context (the live AttackingPermanent), not present on the SecurityCheck event alone.
    // RDW-04  OnPlay / OnEnterFieldAnyone / WhenDigivolving — AS-IS uses the FULL OnEnterFieldHashtable
    //         (OnEnterFieldHashtableParams: evoRoots/Root/oldLevels/digiXrosCount/…); the event carries none of
    //         those, and the Check-variant builders (OnPlayCheckHashtableOfPermanent) are the CanTrigger-probe
    //         shape, NOT the StackSkillInfos payload — using them would diverge, so left unhandled.
    // RDW-05  Effect-driven attack — attackEffect (the live ICardEffect) is a GAP; the event carries only
    //         `attackCauseEffectId`. Plain attacks (attackEffect == null) are handled; effect-driven ones fall
    //         back to a null cardEffect only if the C batch confirms that is AS-IS-faithful for the caller.
    //
    // ===== (C1d) status update — GAPs closed in this batch (emit-enrichment + supply conversion; DORMANT) =======
    //   RDW-04 CLOSED for PLAYER-initiated play/digivolve: OnEnterFieldAnyone / WhenDigivolving now build the AS-IS
    //          OnEnterFieldHashtable (mirror builder) from the enriched play/digivolve emit. Residual: an
    //          EFFECT-driven play's live cardEffect (RD-C1-CARDEFFECT-IDTHREAD) — cardEffect==null here, which the
    //          OnEnterFieldHashtable builder faithfully OMITs (matches AS-IS's null-play case).
    //   RDW-02 CLOSED at the KEY level for OnAddDigivolutionCards + WhenLinked: the inline hashtables are rebuilt
    //          with the full AS-IS key set; the from-flags are computed PRE-move at the emit (resolving
    //          F1-ADDDIGI-FROMFLAGS). Residual VALUE gap: the "CardEffect" member is null (the port threads the
    //          causing effect's SOURCE id, not the live ICardEffect — RD-C1-CARDEFFECT-IDTHREAD). The other RDW-02
    //          timings (discards / add-hand / tap / top-trash / …) stay UNHANDLED per the notes above.
    //   RDW-02 OnDigivolutionCardDiscarded — NOT closed HERE (still no supply conversion), but NO LONGER a live gap:
    //          the window is opened INLINE at the effect-trash seat exactly as AS-IS ITrashDigivolutionCards does
    //          (CardController.cs:5202-5215). The ITrashDigivolutionCards port opens it via its caller
    //          (CardController.cs:1197 → GManager.instance.autoProcessing.StackSkillInfos over TrashSpecificSourcesAsync);
    //          the SINK positional + Commons effect-trash callers open it in the shared substrate
    //          DigivolutionStackHelpers.RemoveSourcesAsync (RD-C5W-ESSTRASHSCAN), pre-move so the payload
    //          {CardEffect(BareCauseEffect carrier), Permanent(pre-removal host), DiscardedCards} matches AS-IS.
    //          The raw OnDigivolutionCardDiscarded emit here stays a no-op for THIS layer (dropped below).
    //   RDW-07 CLOSED: OnStartTurn / OnStartMainPhase / OnEndTurn build the AS-IS null payload. FINDING: the port's
    //          turn emits ALREADY go through TriggerEventEmitter.Emit (MetadataActionProcessor.cs:872/:910/:1022),
    //          which stamps the explicit TriggerTimingKey — so TriggerTimingMap.Derive ALREADY returns these
    //          strings (no new emit key was required; the worksheet's "don't derive" premise was stale).
    //   RDW-01 CLOSED — design item RDW-01-BOUNCE-SNAPSHOT-TRANSPORT: NOT closed HERE (still no supply conversion —
    //          the OnDeletionHashtable PRE-removal snapshot is unreconstructable from the POST-move CardMoved), but
    //          NO LONGER a live gap. The OnLeaveFieldAnyone (+ OnPermamemtReturnedToHand for a hand bounce) window is
    //          opened INLINE at the sink pre-move point exactly as the un-ported AS-IS bounce/put routines do
    //          (HandBounceClaass.Bounce CardController.cs:2692/:2705, DeckBottom/TopBounceClass.DeckBounce
    //          :2374/:2540, IPutSecurityPermanent.PutSecurity :3599): MatchStateMutationSink.StageLeaveWindow records
    //          each field-area departure (ReturnToHand -> HandBounce, ReturnToDeckTop/Bottom -> DeckBounce, a
    //          FIELD-origin AddToSecurity -> SecurityPut) into a per-flush batch, and OpenLeaveWindowsAsync stacks ONE
    //          StackSkillInfos per bounce class over the pre-move Permanent list (collect-before-removal; anyone-
    //          reactor collapses once per class, AS-IS single-list stack). Payload = the boolean-cause
    //          OnDeletionHashtable overload (byEffectCause from the mutation cause id, battle=null, isDPZero=false) —
    //          the leave/return reactor gates (CanTriggerOnPermanentLeave/Deleted) read only the per-permanent
    //          snapshot, never the live effect (RD-C1-CARDEFFECT-IDTHREAD residual). These raw bounce timings stay a
    //          no-op for THIS layer (dropped below). (The field->Trash DELETION timings stay at the mirror
    //          DestroyPermanentsClass / the deferred deletion opener, which already call StackSkillInfos.)

    private const string AttackCauseEffectIdKey = "attackCauseEffectId";

    // ----- (C1d) emit-enrichment metadata keys the play/digivolve/add-source/link emit sites ADD so this layer
    //       can byte-reconstruct the AS-IS Hashtable at cutover. Purely ADDITIVE (behaviour-neutral): no live
    //       consumer reads them before C2 (the live window seeds off the pre-existing keys only). ------------------

    // RDW-04 OnEnterFieldHashtable params — carried on the play (PlayCardAction) / digivolve (DigivolveAction)
    // emit. AS-IS OnEnterFieldHashtable(OnEnterFieldHashtableParams…) (CardController.cs:1681, params class
    // :1100). The port plays/digivolves ONE card from HAND: root is always None, evoRootTops == evoRoots, and the
    // causing ICardEffect is null for a PLAYER-initiated play/digivolve (AS-IS CardEffect =
    // GetCardEffectFromHashtable(_hashtable) == null); an EFFECT-driven play carries only a cause id, so its live
    // cardEffect stays a GAP (design item RD-C1-CARDEFFECT-IDTHREAD).
    public const string OnEnterFieldIsEvolutionKey = "oefIsEvolution"; // presence == "this move carries the params".
    public const string OnEnterFieldIsJogressKey = "oefIsJogress";
    public const string OnEnterFieldDigiXrosCountKey = "oefDigiXrosCount";
    public const string OnEnterFieldAssemblyCountKey = "oefAssemblyCount";
    public const string OnEnterFieldEvoRootIdsKey = "oefEvoRootIds";   // string[] — evoRoots == evoRootTops here.
    public const string OnEnterFieldOldLevelsKey = "oefOldLevels";     // int[]
    public const string OnEnterFieldIsFromDigimonDigivolutionCardsKey = "oefIsFromDigimonDigivolutionCards";

    // RDW-02 OnAddDigivolutionCards from-flags (added to the DigivolutionStackHelpers emit, which already carries
    // "addedCardIds" + "causeSourceId"). AS-IS inline {Permanent, CardEffect, CardSources, isFromSameDigimon,
    // isFromDigimon} (Permanent.cs:1109-1116 / 1213-1220). Computed PRE-move in the helper (resolving the latent
    // F1-ADDDIGI-FROMFLAGS item). CardEffect stays null (the port threads causeSourceId, not the live effect —
    // design item RD-C1-CARDEFFECT-IDTHREAD).
    public const string AddDigivolutionAddedCardIdsKey = "addedCardIds";
    public const string AddDigivolutionIsFromSameDigimonKey = "addDigiIsFromSameDigimon";
    public const string AddDigivolutionIsFromDigimonKey = "addDigiIsFromDigimon";

    // RDW-02 WhenLinked (added to the LinkHelpers emit, which already carries "linkCardId"). AS-IS inline
    // {Permanent, CardEffect, Card, isFromDigimon} (Permanent.cs:1290). isFromDigimon computed PRE-move; CardEffect
    // null (RD-C1-CARDEFFECT-IDTHREAD).
    public const string WhenLinkedLinkCardIdKey = "linkCardId";
    public const string WhenLinkedIsFromDigimonKey = "whenLinkedIsFromDigimon";

    /// <summary>Convert ONE drained <see cref="GameEvent"/> into every AS-IS supply call it opens that this
    /// layer can faithfully reconstruct. An event derives several timings (<see cref="TriggerTimingMap"/>);
    /// each handled timing yields one entry, each unhandled timing is skipped (its GAP is a design item — see
    /// <see cref="UnhandledTimings"/> to enumerate what was dropped for diagnosis). Returns an empty list for
    /// an <see cref="GameEventType.Unknown"/> event.</summary>
    public static IReadOnlyList<SkillWindowSupplyEntry> ConvertEvent(EngineContext context, GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(gameEvent);

        var entries = new List<SkillWindowSupplyEntry>();
        if (gameEvent.Type == GameEventType.Unknown)
        {
            return entries;
        }

        foreach (string timingName in TriggerTimingMap.Derive(gameEvent))
        {
            if (!Enum.TryParse(timingName, ignoreCase: false, out EffectTiming timing) || !Enum.IsDefined(timing))
            {
                continue; // e.g. "OnPlay" has no EffectTiming member — RDW-04.
            }

            if (TryBuildHashtable(context, gameEvent, timing, out Hashtable? hashtable))
            {
                // NOTE: a HANDLED timing may carry a null hashtable — the turn boundaries (RDW-07) mirror the
                // AS-IS `StackSkillInfos(null, timing)` (payload-free). "Handled" == TryBuildHashtable returned
                // true, independent of whether the payload is null.
                entries.Add(new SkillWindowSupplyEntry(
                    timing, hashtable, ReadSequencingBatchId(gameEvent, timing), gameEvent));
            }
        }

        return entries;
    }

    /// <summary>The timings this event DERIVES but which are NOT faithfully reconstructable here (the GAP set
    /// for this event). Exposed so a caller / test can assert exactly what the C batch still has to supply,
    /// and so nothing is silently dropped without being visible.</summary>
    public static IReadOnlyList<EffectTiming> UnhandledTimings(EngineContext context, GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(gameEvent);

        var unhandled = new List<EffectTiming>();
        if (gameEvent.Type == GameEventType.Unknown)
        {
            return unhandled;
        }

        foreach (string timingName in TriggerTimingMap.Derive(gameEvent))
        {
            if (Enum.TryParse(timingName, ignoreCase: false, out EffectTiming timing)
                && Enum.IsDefined(timing)
                && !TryBuildHashtable(context, gameEvent, timing, out _))
            {
                unhandled.Add(timing);
            }
        }

        return unhandled;
    }

    /// <summary>Build the AS-IS <c>Hashtable</c> payload for (<paramref name="gameEvent"/>,
    /// <paramref name="timing"/>) using a MIRROR HashtableSetting builder (byte-identical keys). Returns false
    /// for an UNHANDLED timing — the converter never guesses a payload (no-simplification rule); an unhandled
    /// timing's GAP is a design item (see the RDW-NN notes above).</summary>
    public static bool TryBuildHashtable(
        EngineContext context, GameEvent gameEvent, EffectTiming timing, out Hashtable? hashtable)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(gameEvent);
        hashtable = null;

        switch (timing)
        {
            // ---- ATTACK family: AS-IS OnAttackCheckHashtableOfPermanent(AttackingPermanent, attackEffect) ----
            // AttackProcess.cs:98-99 sets EffectHashtable = OnAttackCheckHashtableOfPermanent(AttackingPermanent,
            // attackEffect); OnCounterTiming (:266/:285, via the cut-in instance) passes it. The mirror builder is
            // REUSED here with the same permanent view. For the cut-in OnCounterTiming, the loop-level routing
            // (autoProcessing_CutIn + the IsCounterEffect two-pass split) is a C-batch concern (RDW-06 / C2b); the
            // PAYLOAD is this same builder.
            // (P1-2 C2r) OnAllyAttack (:199) and OnEndAttack (:480) are NO LONGER converted here: at the C2r flip the
            // AttackProcess inline StackSkillInfos inserts became the SOLE openers and their emits were REMOVED, so
            // SkillWindowSupply never sees an OnAllyAttack/OnEndAttack attack event — TryBuildAttack for those timings
            // is now UNREACHABLE (an effect-driven attack, which supply DROPPED on attackCauseEffectId, now opens its
            // window via the inline insert). Only OnCounterTiming remains supply-carried.
            case EffectTiming.OnCounterTiming:
                return TryBuildAttack(context, gameEvent, out hashtable);

            // ---- OnMove: AS-IS inline { "Permanent", permanent } (CardObjectController.cs:1111) --------------
            // (C1 W2 extension) The Breeding->BattleArea promotion CardMoved carries the promoted card as
            // Subject; the AS-IS payload is a single live Permanent. Reconstructable in full from (subject id +
            // owner) — no builder, no emit enrichment needed (the same Permanent view TryBuildAttack builds).
            case EffectTiming.OnMove:
                return TryBuildOnMove(context, gameEvent, out hashtable);

            // ---- OnReturnCardsToLibraryFromTrash: AS-IS inline { "CardSources", cardSources } ----------------
            // (CardObjectController.cs:800/882). AS-IS opens ONE window for the whole returned-card LIST; the
            // port emits one Trash->Library CardMoved per card, so this yields a single-card CardSources list
            // per event. The N-events -> 1-window collapse is the port's event-model concern (A3 batch
            // sequencing / the loop), NOT a converter fabrication: each entry faithfully carries THIS event's
            // returned card. No CardEffect member in the AS-IS payload, so nothing is id-blocked here.
            case EffectTiming.OnReturnCardsToLibraryFromTrash:
                return TryBuildReturnCardsToLibraryFromTrash(context, gameEvent, out hashtable);

            // ---- (C1d RDW-04) OnPlay / OnEnterFieldAnyone / WhenDigivolving: AS-IS OnEnterFieldHashtable -----
            // The play (PlayCardAction) / digivolve (DigivolveAction) emit is enriched with the params (marker
            // OnEnterFieldIsEvolutionKey). A bare Hand/Breeding->field move WITHOUT the marker stays unhandled.
            case EffectTiming.OnEnterFieldAnyone:
            case EffectTiming.WhenDigivolving:
                return TryBuildOnEnterField(context, gameEvent, out hashtable);

            // ---- (C1d RDW-02) OnAddDigivolutionCards: AS-IS inline {Permanent, CardEffect, CardSources, ---------
            //      isFromSameDigimon, isFromDigimon} (Permanent.cs:1109-1116/1213-1220). -----------------------------
            case EffectTiming.OnAddDigivolutionCards:
                return TryBuildOnAddDigivolutionCards(context, gameEvent, out hashtable);

            // ---- (C1d RDW-02) WhenLinked: AS-IS inline {Permanent, CardEffect, Card, isFromDigimon} (:1290) -----
            case EffectTiming.WhenLinked:
                return TryBuildWhenLinked(context, gameEvent, out hashtable);

            // ---- (C1d RDW-07) turn/phase boundaries: AS-IS StackSkillInfos(null, timing) (TurnStateMachine.cs:564/
            //      905, AutoProcessing.cs:699) — a payload-FREE window. HANDLED with a null hashtable. The port's
            //      TriggerEventEmitter already stamps the explicit timing, so TriggerTimingMap derives these. ------
            case EffectTiming.OnStartTurn:
            case EffectTiming.OnStartMainPhase:
            case EffectTiming.OnEndTurn:
                hashtable = null; // AS-IS null payload (byte-identical to StackSkillInfos(null, timing)).
                return true;

            default:
                return false; // UNHANDLED — RDW-01/03/05/06 per the timing.
        }
    }

    private static bool TryBuildAttack(EngineContext context, GameEvent gameEvent, out Hashtable? hashtable)
    {
        hashtable = null;
        if (gameEvent.Subject is not { IsEmpty: false } attackerId)
        {
            return false; // no attacker subject — cannot form AttackingPermanent (RDW-05 substrate branch).
        }

        // (RDW-05) A live attack cause effect (attackEffect) is NOT reconstructable from the event — it carries
        // only the id. AS-IS passes attackEffect=null for a PLAIN declared attack; only then is the null payload
        // faithful. An effect-driven attack (attackCauseEffectId present) is a GAP: do not fabricate an effect.
        if (gameEvent.Metadata.ContainsKey(AttackCauseEffectIdKey))
        {
            return false;
        }

        HeadlessPlayerId owner = ResolveOwner(context, attackerId, gameEvent);
        Permanent attackingPermanent = new(context, attackerId, owner);
        // AS-IS attackEffect == null for a plain declared attack (AttackProcess.cs:98). The mirror builder still
        // adds { "CardEffect", null } — byte-identical to AS-IS.
        hashtable = CardEffectCommons.OnAttackCheckHashtableOfPermanent(
            attackingPermanent, cardEffect: null!);
        return true;
    }

    /// <summary>AS-IS <c>OnMove</c> payload <c>new Hashtable { { "Permanent", permanent } }</c>
    /// (CardObjectController.cs:1111) — the Digimon promoted out of the breeding area. The moved card is the
    /// event Subject; the live Permanent is the same (context, id, owner) view TryBuildAttack builds.</summary>
    private static bool TryBuildOnMove(EngineContext context, GameEvent gameEvent, out Hashtable? hashtable)
    {
        hashtable = null;
        if (gameEvent.Subject is not { IsEmpty: false } subjectId)
        {
            return false;
        }

        HeadlessPlayerId owner = ResolveOwner(context, subjectId, gameEvent);
        Permanent permanent = new(context, subjectId, owner);
        hashtable = new Hashtable { { "Permanent", permanent } };
        return true;
    }

    /// <summary>AS-IS <c>OnReturnCardsToLibraryFromTrash</c> payload
    /// <c>new Hashtable { { "CardSources", cardSources } }</c> (CardObjectController.cs:800/882). The port emits
    /// one Trash->Library CardMoved per card, so the list carries THIS event's single returned card (the
    /// per-event -> per-window collapse is A3 / loop territory, not a converter concern).</summary>
    private static bool TryBuildReturnCardsToLibraryFromTrash(
        EngineContext context, GameEvent gameEvent, out Hashtable? hashtable)
    {
        hashtable = null;
        if (gameEvent.Subject is not { IsEmpty: false } subjectId)
        {
            return false;
        }

        HeadlessPlayerId owner = ResolveOwner(context, subjectId, gameEvent);
        HeadlessPlayerId controller = gameEvent.Actor ?? owner;
        CardSource cardSource = new(context, subjectId, controller, owner);
        hashtable = new Hashtable { { "CardSources", new List<CardSource> { cardSource } } };
        return true;
    }

    /// <summary>(C1d RDW-04) AS-IS <c>OnEnterFieldHashtable(OnEnterFieldHashtableParams…)</c>
    /// (CardController.cs:1681, params class :1100) for OnEnterFieldAnyone / WhenDigivolving. Built from the
    /// enriched play/digivolve emit (marker <see cref="OnEnterFieldIsEvolutionKey"/>): the entered permanent is
    /// the event Subject (post-move it is on the field, so a live <see cref="Permanent"/> view is faithful); the
    /// per-param members (isEvolution/isJogress/counts/evoRoots/oldLevels/isFromDigimonDigivolutionCards) ride the
    /// event; <c>root</c> is None and <c>evoRootTops == evoRoots</c> for the port's single-card HAND play/digivolve;
    /// <c>cardEffect</c> is null (a PLAYER-initiated play is AS-IS cardEffect==null — an EFFECT-driven play's live
    /// effect is a GAP, design item RD-C1-CARDEFFECT-IDTHREAD). A move WITHOUT the marker is unhandled.</summary>
    private static bool TryBuildOnEnterField(EngineContext context, GameEvent gameEvent, out Hashtable? hashtable)
    {
        hashtable = null;
        if (gameEvent.Subject is not { IsEmpty: false } subjectId
            || !gameEvent.Metadata.ContainsKey(OnEnterFieldIsEvolutionKey))
        {
            return false;
        }

        HeadlessPlayerId owner = ResolveOwner(context, subjectId, gameEvent);
        bool isEvolution = ReadBool(gameEvent, OnEnterFieldIsEvolutionKey);
        bool isJogress = ReadBool(gameEvent, OnEnterFieldIsJogressKey);
        int digiXrosCount = ReadInt(gameEvent, OnEnterFieldDigiXrosCountKey);
        int assemblyCount = ReadInt(gameEvent, OnEnterFieldAssemblyCountKey);
        bool isFromDigimonDigivolutionCards = ReadBool(gameEvent, OnEnterFieldIsFromDigimonDigivolutionCardsKey);
        List<CardSource> evoRoots = ReadCardSources(context, gameEvent, OnEnterFieldEvoRootIdsKey, owner);
        List<int> oldLevels = ReadIntList(gameEvent, OnEnterFieldOldLevelsKey);

        var param = new OnEnterFieldHashtableParams(
            permanent: new Permanent(context, subjectId, owner),
            evoRoots: evoRoots,
            evoRootTops: new List<CardSource>(evoRoots),
            root: SelectCardEffect.Root.None,
            oldLevels: oldLevels,
            isFromDigimonDigivolutionCards: isFromDigimonDigivolutionCards,
            digiXrosCount: digiXrosCount,
            assemblyCount: assemblyCount);

        hashtable = CardEffectCommons.OnEnterFieldHashtable(
            new List<OnEnterFieldHashtableParams> { param },
            isEvolution, isJogress, digiXrosCount, assemblyCount, cardEffect: null!);
        return true;
    }

    /// <summary>(C1d RDW-02) AS-IS inline <c>{Permanent, CardEffect, CardSources, isFromSameDigimon,
    /// isFromDigimon}</c> (Permanent.cs:1109-1116 / 1213-1220). Host = event Subject; the added cards come from
    /// the emit's <c>addedCardIds</c>; the from-flags are pre-computed at the emit site (F1-ADDDIGI-FROMFLAGS).
    /// CardEffect is rebuilt from the threaded cause SOURCE id (design item RD-C1-CARDEFFECT-IDTHREAD closed at
    /// the KEY level, closed at the VALUE level for an effect-driven add): an effect-driven place-under carries a
    /// non-empty <c>causeSourceId</c> ⇒ a <see cref="BareCauseEffect"/> resolving that source (non-null
    /// <c>EffectSourceCard</c>, so both the <c>CardEffect != null</c> guard AND source-reading gates such as
    /// EX6_001's <c>EffectSourceCard != null</c> pass). A cause-LESS place-under (empty <c>causeSourceId</c>) keeps
    /// CardEffect null — AS-IS AddDigivolutionCardsTop/Bottom is itself called with a null cardEffect on several
    /// non-effect paths (CardController.cs:1503 evolve-root re-stack, SelectDigiXrosClass, SelectAssemblyClass), and
    /// there the <c>CardEffect != null</c> gate (OnAddDigivolutionCards.cs:24) rejects — no fire.</summary>
    private static bool TryBuildOnAddDigivolutionCards(EngineContext context, GameEvent gameEvent, out Hashtable? hashtable)
    {
        hashtable = null;
        if (gameEvent.Subject is not { IsEmpty: false } hostId
            || !gameEvent.Metadata.TryGetValue(AddDigivolutionAddedCardIdsKey, out object? raw)
            || raw is not string joined || joined.Length == 0)
        {
            return false;
        }

        HeadlessPlayerId hostOwner = ResolveOwner(context, hostId, gameEvent);
        var cardSources = joined
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => new CardSource(context, new HeadlessEntityId(id), hostOwner, hostOwner))
            .ToList();

        hashtable = new Hashtable
        {
            { "Permanent", new Permanent(context, hostId, hostOwner) },
            // (RD-C1-CARDEFFECT-IDTHREAD, closed for OnAddDigivolutionCards) Rebuild the causing effect from the
            // threaded cause SOURCE id when the emit carries one (effect-driven add); an empty cause keeps it null so
            // the AS-IS CardEffect != null gate rejects (AS-IS null-cardEffect callers — see method doc).
            { "CardEffect", ReadCauseEffectOrNull(context, gameEvent) },
            { "CardSources", cardSources },
            { "isFromSameDigimon", ReadBool(gameEvent, AddDigivolutionIsFromSameDigimonKey) },
            { "isFromDigimon", ReadBool(gameEvent, AddDigivolutionIsFromDigimonKey) },
        };
        return true;
    }

    /// <summary>(C1d RDW-02) AS-IS inline <c>{Permanent, CardEffect, Card, isFromDigimon}</c> (Permanent.cs:1290).
    /// Host = event Subject; the linked card = the emit's <c>linkCardId</c>; isFromDigimon pre-computed at the
    /// emit. CardEffect is null (RD-C1-CARDEFFECT-IDTHREAD).</summary>
    private static bool TryBuildWhenLinked(EngineContext context, GameEvent gameEvent, out Hashtable? hashtable)
    {
        hashtable = null;
        if (gameEvent.Subject is not { IsEmpty: false } hostId
            || !gameEvent.Metadata.TryGetValue(WhenLinkedLinkCardIdKey, out object? raw)
            || raw is not string linkId || linkId.Length == 0)
        {
            return false;
        }

        HeadlessPlayerId hostOwner = ResolveOwner(context, hostId, gameEvent);
        hashtable = new Hashtable
        {
            { "Permanent", new Permanent(context, hostId, hostOwner) },
            // (RD-C1-CARDEFFECT-IDTHREAD, closed for WhenLinked) AS-IS Permanent.AddLinkCard always stacks a
            // NON-NULL causing effect (Permanent.cs:1284 {"CardEffect", cardEffect}) — a link is ALWAYS
            // effect-driven (the two AS-IS callers CardController.cs:3391/3492 pass _cardEffect; there is no
            // rules-based "natural link"), so the CardEffect != null guard in CanTriggerWhenLinked
            // (CanUseEffects/WhenLinked.cs:61) is invariably satisfied when the window opens. Rebuild it from a
            // threaded cause id when the emit carries one (an effect-driven link), else a source-less bare cause
            // preserves the non-null guard (no ported WhenLinked gate reads the cause's SOURCE — every reactor
            // self-gates on the host Permanent / the link Card only).
            { "CardEffect", BareCauseEffect.For(context, ReadCauseSourceId(gameEvent)) },
            { "Card", new CardSource(context, new HeadlessEntityId(linkId), hostOwner, hostOwner) },
            { "isFromDigimon", ReadBool(gameEvent, WhenLinkedIsFromDigimonKey) },
        };
        return true;
    }

    /// <summary>The causing effect rebuilt from the threaded cause SOURCE id, or <c>null</c> when the emit carries
    /// no cause (empty <c>causeSourceId</c>). Unlike WhenLinked — where a link is ALWAYS effect-driven, so an empty
    /// cause still collapses to a source-less non-null <see cref="BareCauseEffect"/> — OnAddDigivolutionCards is
    /// reachable from AS-IS null-cardEffect callers (a cause-less add fires nothing), so an empty cause must yield a
    /// null CardEffect to keep the <c>CardEffect != null</c> gate rejecting.</summary>
    private static ICardEffect? ReadCauseEffectOrNull(EngineContext context, GameEvent gameEvent)
    {
        HeadlessEntityId causeId = ReadCauseSourceId(gameEvent);
        return causeId.IsEmpty ? null : BareCauseEffect.For(context, causeId);
    }

    /// <summary>The threaded causing-effect source id (<c>"causeSourceId"</c>), or default (empty) when the emit
    /// carries no cause — the caller collapses it to a source-less <see cref="BareCauseEffect"/>.</summary>
    private static HeadlessEntityId ReadCauseSourceId(GameEvent gameEvent) =>
        gameEvent.Metadata.TryGetValue("causeSourceId", out object? raw) && raw is string value && value.Length > 0
            ? new HeadlessEntityId(value)
            : default;

    private static bool ReadBool(GameEvent gameEvent, string key) =>
        gameEvent.Metadata.TryGetValue(key, out object? raw) && raw is true;

    private static int ReadInt(GameEvent gameEvent, string key) =>
        gameEvent.Metadata.TryGetValue(key, out object? raw) && raw is int value ? value : 0;

    private static List<int> ReadIntList(GameEvent gameEvent, string key) =>
        gameEvent.Metadata.TryGetValue(key, out object? raw) && raw is IEnumerable<int> values
            ? values.ToList()
            : new List<int>();

    private static List<CardSource> ReadCardSources(EngineContext context, GameEvent gameEvent, string key, HeadlessPlayerId owner)
    {
        var list = new List<CardSource>();
        if (gameEvent.Metadata.TryGetValue(key, out object? raw) && raw is IEnumerable<string> ids)
        {
            foreach (string id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    list.Add(new CardSource(context, new HeadlessEntityId(id), owner, owner));
                }
            }
        }

        return list;
    }

    /// <summary>Owner of the reacting/subject card: the repository's authoritative owner, falling back to the
    /// event <see cref="GameEvent.Actor"/> (the attacking player == the attacker's owner for the attack family).</summary>
    private static HeadlessPlayerId ResolveOwner(EngineContext context, HeadlessEntityId card, GameEvent gameEvent)
    {
        if (context.CardInstanceRepository.TryGetInstance(card, out CardInstanceRecord? record)
            && record is not null && !record.OwnerId.IsEmpty)
        {
            return record.OwnerId;
        }

        return gameEvent.Actor ?? default;
    }

    // ========================================================================================================
    // (A3) MINIMUM-BATCH SEQUENCING — the SUPPLY-layer port of WindowResolver.FilterToMinimumBatch
    // (WindowResolver.cs:324). AS-IS resolves each DestroyPermanentsClass.Destroy() / IReduceSecurity() etc. in
    // its OWN StackSkillInfos window, fully, before the next — the coroutine's temporal ordering. The port can
    // co-drain several batches' CardMoved events into ONE seed, so the feeder must re-impose that ordering:
    // feed only the LOWEST-batch group per TriggeredSkillProcess invocation, holding higher batches for the
    // next pass. This lives ENTIRELY here; the mirror MultipleSkills loop stays batch-free (AS-IS 1:1) —
    // sequencing is a supply concern, not a loop concern (design ADAPTATION A3, D-1 precedent).
    //
    // The batch id is read off the event by the SAME per-timing rules the old wiring stamped onto its
    // TimingWindowTrigger.BatchId (WindowResolverWiring.cs:348 deletion; :990/:1002/:1015/:1026/:1041 the
    // leave/security-loss/discard/add-hand/add-security bridge stamps): all draw from ONE globally-unique
    // counter (EngineContext.Next*BatchId), so ids are comparable ACROSS timings. A batch-less entry (no id) is
    // eligible EVERY pass exactly as FilterToMinimumBatch keeps `t.BatchId is null` in the active set — so it
    // rides the FIRST pass with the minimum batch.
    // ========================================================================================================

    /// <summary>The cross-batch sequencing id for (<paramref name="gameEvent"/>, <paramref name="timing"/>),
    /// mirroring the per-timing <c>TimingWindowTrigger.BatchId</c> the old wiring stamped. Returns null for a
    /// batch-less timing (always eligible). A real (non-zero) stamped id sequences; the sentinel 0 (an
    /// unstamped move) is treated as batch-less (null) — exactly as the old stamp guarded `deletionBatchId != 0`
    /// (WindowResolverWiring.cs:348).</summary>
    public static long? ReadSequencingBatchId(GameEvent gameEvent, EffectTiming timing)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);

        long id = timing switch
        {
            // Deletion / field-departure — DeletionBatchIdKey (WindowResolverWiring.cs:348/:990).
            EffectTiming.OnDestroyedAnyone or EffectTiming.OnLeaveFieldAnyone =>
                ReadMeta(gameEvent, MatchStateMutationSink.DeletionBatchIdKey),

            // Security loss — SecurityLossBatchIdKey (:1002).
            EffectTiming.OnLoseSecurity =>
                ReadMeta(gameEvent, MatchStateMutationSink.SecurityLossBatchIdKey),

            // Discards — DiscardBatchIdKey, OnDiscardSecurity falls back to SecurityLossBatchIdKey (:1015,
            // ReadDiscardBatchId's fallback).
            EffectTiming.OnDiscardHand or EffectTiming.OnDiscardLibrary =>
                ReadMeta(gameEvent, MatchStateMutationSink.DiscardBatchIdKey),
            EffectTiming.OnDiscardSecurity =>
                ReadMeta(gameEvent, MatchStateMutationSink.DiscardBatchIdKey) is var d && d != 0
                    ? d
                    : ReadMeta(gameEvent, MatchStateMutationSink.SecurityLossBatchIdKey),

            // Add-hand — AddHandBatchIdKey (:1026).
            EffectTiming.OnAddHand =>
                ReadMeta(gameEvent, MatchStateMutationSink.AddHandBatchIdKey),

            // Add-security — AddSecurityBatchIdKey, falling back to the event Sequence for an unstamped move
            // (:1041, ReadAddSecurityBatchId).
            EffectTiming.OnAddSecurity =>
                ReadMeta(gameEvent, MatchStateMutationSink.AddSecurityBatchIdKey) is var a && a != 0
                    ? a
                    : gameEvent.Sequence,

            _ => 0L, // batch-less timing.
        };

        return id != 0 ? id : null;
    }

    private static long ReadMeta(GameEvent gameEvent, string key) =>
        gameEvent.Metadata.TryGetValue(key, out object? raw) && raw is long id ? id : 0L;

    /// <summary>(A3) Split a co-drained set of supply entries into the ORDERED passes the feeder hands to
    /// successive <c>TriggeredSkillProcess</c> invocations: pass 0 = every batch-less entry PLUS the
    /// lowest-batch group; each later pass = the next ascending batch group alone. This is the feeder-side
    /// equivalent of <c>WindowResolver.FilterToMinimumBatch</c> re-running per loop pass (batch-less entries
    /// stay eligible every pass but resolve in the first, so they belong to pass 0). Original collection order
    /// is preserved WITHIN each pass (stable). An empty input yields no passes.</summary>
    public static IReadOnlyList<IReadOnlyList<SkillWindowSupplyEntry>> SequenceByMinimumBatch(
        IReadOnlyList<SkillWindowSupplyEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            return Array.Empty<IReadOnlyList<SkillWindowSupplyEntry>>();
        }

        var batchless = new List<SkillWindowSupplyEntry>();
        // Ascending batch id -> its entries, insertion-stable within a batch.
        var byBatch = new SortedDictionary<long, List<SkillWindowSupplyEntry>>();
        foreach (SkillWindowSupplyEntry entry in entries)
        {
            if (entry.BatchId is long b)
            {
                if (!byBatch.TryGetValue(b, out List<SkillWindowSupplyEntry>? group))
                {
                    group = new List<SkillWindowSupplyEntry>();
                    byBatch[b] = group;
                }

                group.Add(entry);
            }
            else
            {
                batchless.Add(entry);
            }
        }

        var passes = new List<IReadOnlyList<SkillWindowSupplyEntry>>();
        bool first = true;
        foreach (KeyValuePair<long, List<SkillWindowSupplyEntry>> kvp in byBatch)
        {
            if (first)
            {
                // Pass 0: batch-less (eligible every pass, resolved here) + the minimum batch.
                var pass0 = new List<SkillWindowSupplyEntry>(batchless);
                pass0.AddRange(kvp.Value);
                passes.Add(pass0);
                first = false;
            }
            else
            {
                passes.Add(kvp.Value);
            }
        }

        if (first)
        {
            // No batched entries at all — a single pass of the batch-less entries.
            passes.Add(batchless);
        }

        return passes;
    }
}
