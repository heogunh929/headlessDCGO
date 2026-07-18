namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Globalization;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// Production <see cref="IEffectMutationSink"/> that applies card effect mutations to the
/// authoritative runtime store (<see cref="ICardInstanceRepository"/> metadata), which the
/// block/battle/security processors and the observation encoder read.
///
/// W2 vocabulary (synchronous, card-instance metadata):
/// <list type="bullet">
/// <item>Keyword grants → boolean flags (Blocker/Rush/Reboot/PreventBattleDeletion/SecurityCheck/
/// Blitz/Retaliation/ArmorPurge).</item>
/// <item><see cref="AddDpModifierKind"/> → appends a typed <see cref="DpModifier"/> to the target's
/// <c>dpModifiers</c> list (read by <c>BattleResolver</c> and <c>CardObservationView</c>).</item>
/// <item><see cref="SuspendKind"/> / <see cref="UnsuspendKind"/> → the <c>isSuspended</c> flag.</item>
/// <item><see cref="SetFlagKind"/> / <see cref="ClearFlagKind"/> → an arbitrary named flag (for
/// restrictions, once-per-turn markers, custom state).</item>
/// </list>
/// Async vocabulary (zone moves, draw, memory) is W2-follow: those need a deferred flush with the
/// engine context and are not yet handled here — they are recorded as unsupported.
///
/// (R2-D) 잔류=존이동 적용 substrate; 룰 판정은 전부 미러 호출. Every game-RULE judgment this sink makes is
/// delegated to its AS-IS home rather than duplicated locally: the no-arg immunity/restriction judgments read
/// the mirror Permanent getters directly (SuspendKind → !Permanent.CanSuspend; effect-delete general immunity →
/// !Permanent.CanBeDestroyed()); the cause-conditional and player-scope judgments read the shared substrate seams
/// the mirror CardController / CardEffectCommons themselves consult (RestrictionScan / IsRestrictedByCauseNewModel,
/// and — since B군 P0-1 — the live TopCard.CanNotBeAffected getter directly, no longer the dead-registry
/// ContinuousImmunityGate.BlocksOpponentEffect). What remains sink-local is pure
/// zone-move APPLICATION substrate — repository Upsert, IZoneMover calls, metadata/batch-id stamping and CardMoved
/// event emission. STOPs (R3): (RD-R2-02) RESOLVED AT ITS AS-IS HOME (R3-A): the mirror
/// <see cref="Assets.Scripts.Script.DestroyPermanentsClass"/> now exists and threads the LIVE causing ICardEffect
/// (read from its Hashtable via CardEffectCommons.GetCardEffectFromHashtable) into the AS-IS per-target immunity
/// filter <c>!TopCard.CanNotBeAffected(cardEffect) &amp;&amp; permanent.CanBeDestroyedBySkill(cardEffect)</c> — no more
/// source-id-only cause seam. The LIVE sink Delete path below still uses the local RestrictionScan /
/// IsRestrictedByCauseNewModel seam; routing ApplyDelete's emission THROUGH DestroyPermanentsClass (bigbang §3-R2
/// item 4) is deferred as design item RD-R3-01 — blocked until the parallel R3-B batch lands the mirror cut-in
/// DRAIN (AutoProcessing.TriggeredSkillProcess, RD-P6C1-3): until then a would-be-deleted replacement window would
/// throw, and the deletion-replacement machine below is still co-consumed by BattleResolver / SecurityResolver
/// (out of R3-A scope). (RD-R2-03) the player-security judgment stays the authoritative scan here because the
/// mirror Player.CanAddSecurity is a stub (MIG3-CANADDSECURITY); (RD-R2-04 / RD-R3-01) the deletion-replacement
/// WINDOW pipeline (DeletionReplacementTiming/Gate option queue, batch-defer, Fortitude, DestroyPermanentsClass
/// window order) stays intact below = the live path until the RD-R3-01 cutover.
/// </summary>
public sealed class MatchStateMutationSink : IEffectMutationSink
{
    public const string TargetEntityIdKey = "targetEntityId";
    public const string DpModifiersKey = "dpModifiers";
    public const string SuspendedFlagKey = "isSuspended";

    // Mutation kinds (the effect→state vocabulary contract for Phase 4 card porting).
    public const string AddDpModifierKind = "AddDpModifier";
    public const string SuspendKind = "Suspend";
    public const string UnsuspendKind = "Unsuspend";
    public const string SetFlagKind = "SetFlag";
    public const string ClearFlagKind = "ClearFlag";

    // CV-B1: effect-driven deletion (destroy a Digimon). Unlike TrashCard (a raw zone move), Delete
    // honours deletion-prevention — the static `cannotBeDeleted` flag and continuous Delete/Prevent
    // replacements (same source the BattleDeletionGate consults) — and stamps `deletedByEffect` for any
    // OnDeletion triggers. (OnDeletion timing emission is wired separately in CV-A4.)
    public const string DeleteKind = "Delete";

    /// <summary>(B3) marks a Delete mutation as the DP&lt;=0 rule process (AS-IS <c>{"DPZero": true}</c>
    /// hashtable flag on DestroyPermanentsClass) — stamped onto the deleted card so DP-zero-aware
    /// on-deletion predicates (AS-IS <c>IsDPZeroDelete</c>) can read it.</summary>
    public const string IsDpZeroKey = "isDpZero";
    public const string CannotBeDeletedFlagKey = "cannotBeDeleted";
    public const string DeletedByEffectKey = "deletedByEffect";

    /// <summary>(W6-T) the CAUSING effect's source card id, stamped with every effect deletion — the AS-IS
    /// on-deletion hashtable carries the CardEffect (IsByEffect reads its EffectSourceCard).</summary>
    public const string DeletedBySourceEntityIdKey = "deletedBySourceEntityId";
    public const string DeletionPreventedKey = "deletionPrevented";

    /// <summary>(C-Del 3c-1 promote-to-defer) marks a deferred deletion whose survival is owned by the AS-IS PRE
    /// cut-in "would be deleted" window (an INTERACTIVE Evade/Barrier/Fragment/ArmorPurge replacement paused the
    /// sink's inline drain). Unlike a gate-deferred card (finalized on the DeletionReplacementTiming decline), a
    /// promoted card is finalized by the sweep on its <c>willBeRemoveField</c>: the resumed window body clears it
    /// (the card SURVIVES — the sweep clears pendingDeletion, never trashing) or leaves it set (the card DIES —
    /// the sweep trashes it in the batch-unit OnDeletion window). See GameFlowProcessor.StateBasedDeletionSweep.</summary>
    public const string PreWindowPromotedKey = "preWindowPromoted";

    /// <summary>(D-1 / VR-8) The delete-BATCH id stamped onto a deleted card's CardMoved (field-&gt;trash) event
    /// metadata — one id per AS-IS <c>DestroyPermanentsClass.Destroy()</c> call. The window collapse keys its
    /// OnDeletion / OnLeaveFieldAnyone dedup by (reactor, this id), so N cards in ONE batch fire the reactor once
    /// while an independent second delete-process (a distinct id) fires it again. Also stashed on a DEFERRED
    /// deletion's instance metadata so the deferred-finalize move re-stamps the ORIGINATING batch's id.</summary>
    public const string DeletionBatchIdKey = "deletionBatchId";

    /// <summary>(F1-M1 P1-1) The security-LOSS batch id stamped onto a security card's CardMoved
    /// (Security-&gt;non-Security) event metadata — one id per AS-IS <c>IReduceSecurity.ReduceSecurity()</c> call,
    /// i.e. per <c>IDestroySecurity.DestroySecurity()</c> (the effect-driven multi-trash calls IReduceSecurity ONCE
    /// after trashing N cards, CardController.cs:4358-4363 → a SINGLE StackSkillInfos(OnLoseSecurity) broadcast for
    /// the whole batch, hashtable {Player}). The OnLoseSecurity activated-bridge collapse keys its dedup by
    /// (reactor, this id), so N cards trashed by ONE effect fire the reactor once while an independent second
    /// security removal (a distinct id) fires it again. Distinct from <see cref="DeletionBatchIdKey"/> BECAUSE a
    /// security move is NOT a field deletion (it must NOT derive OnDeletion / OnLeaveFieldAnyone in TriggerTimingMap);
    /// the id sequence is shared with the deletion counter so a mixed drain never collides in the window's cross-batch
    /// ordering. The sentinel 0 (an unstamped security move — e.g. the attack security-CHECK per-card reveal, which is
    /// per-card by design) collapses all-together within one drain, preserving per-card firing across per-iteration
    /// windows.</summary>
    public const string SecurityLossBatchIdKey = "securityLossBatchId";

    /// <summary>(F1-Tier1 OnDiscard*) The DISCARD batch id stamped onto a discarded card's CardMoved
    /// (Hand-&gt;Trash / Library-&gt;Trash) event metadata — one id per AS-IS logical discard operation
    /// (<c>DiscardHands</c> → one <c>StackSkillInfos(OnDiscardHand)</c>; <c>TrashDeckCards</c> → one
    /// <c>StackSkillInfos(OnDiscardLibrary)</c>), i.e. per sink flush so an effect that discards N cards fires the
    /// OnDiscard* reactor ONCE. The activated-bridge collapse (<c>WindowResolverWiring</c>) keys its dedup by
    /// (reactor, this id). Shares the deletion/security-loss counter for global uniqueness (see
    /// <c>EngineContext.NextDiscardBatchId</c>). OnDiscardSecurity reuses <see cref="SecurityLossBatchIdKey"/> (a
    /// security discard is one IReduceSecurity == one security-loss batch). Sentinel 0 = an unstamped move.</summary>
    public const string DiscardBatchIdKey = "discardBatchId";

    /// <summary>(F1-Tier1 OnDiscardHand/Security) The CAUSE effect's source card id stamped onto an effect-driven
    /// discard CardMoved — the headless mirror of the AS-IS hashtable <c>{"CardEffect", cardEffect}</c>. AS-IS
    /// <c>CanTriggerOnTrashHand/Security</c> requires <c>CardEffect != null</c> (the discard must be effect-driven)
    /// AND lets a reactor gate on the causing effect's <c>EffectSourceCard</c> (e.g. ST16_14: source owner ==
    /// reactor owner). A NON-effect trash (attack security-CHECK reveal, hand-size trim) carries no cause id, so
    /// the gate rejects it. OnDiscardLibrary has no <c>CardEffect</c> check in AS-IS (WhenDiscardLibrary.cs) and
    /// does not consult this key.</summary>
    public const string DiscardCauseEffectIdKey = "discardCauseEffectId";

    /// <summary>(F1-Tier1 OnAddHand) The ADD-HAND batch id stamped onto a card's -&gt;Hand CardMoved by an
    /// EFFECT-driven hand add (<c>DrawCards</c> / <c>ReturnToHand</c>) — one id per sink flush == one AS-IS
    /// <c>AddHandCards(list, isDraw, cardEffect)</c> == one <c>StackSkillInfos(OnAddHand)</c> over the whole added
    /// list, so an effect that draws/returns N cards fires the OnAddHand reactor ONCE. The activated-bridge collapse
    /// (<c>WindowResolverWiring</c>) keys its dedup by (reactor, timing, this id). Shares the deletion/discard counter
    /// for global uniqueness (<c>EngineContext.NextAddHandBatchId</c>). Sentinel 0 = an unstamped move (a
    /// turn/mulligan/setup draw — those also carry no cause id, so they fail the OnAddHand CardEffect!=null gate).</summary>
    public const string AddHandBatchIdKey = "addHandBatchId";

    /// <summary>(F1-Tier1 OnAddHand) The CAUSE effect's source card id stamped onto an effect-driven hand-add
    /// CardMoved — the headless mirror of the AS-IS hashtable <c>{"CardEffect", cardEffect}</c> passed to
    /// <c>AddHandCards</c>. AS-IS <c>CanTriggerOnHandAdded</c> (OnCardsAddedToHand.cs:19) REQUIRES
    /// <c>CardEffect != null</c> (the hand add must be effect-driven) AND lets a reactor gate on the causing effect's
    /// <c>EffectSourceCard</c>. A NON-effect hand add (turn draw / mulligan / initial deal — AS-IS cardEffect=null)
    /// carries no cause id, so the gate rejects it. Distinct from <see cref="DiscardCauseEffectIdKey"/> so a single
    /// sink flush that both discards and adds-to-hand does not cross-thread the two causes.</summary>
    public const string AddHandCauseEffectIdKey = "addHandCauseEffectId";

    /// <summary>(F1-Tier1 OnAddSecurity, design item F1-ADD-COUNTER P2-1) The ADD-SECURITY batch id stamped onto a
    /// card's -&gt;Security CardMoved by an effect / replacement / player security add — one id PER SINGLE added
    /// card (OnAddSecurity is NOT collapsed: AS-IS fires one <c>StackSkillInfos(OnAddSecurity)</c> per
    /// <c>IAddSecurity</c>, resolved sequentially). N cards of one recovery carry N ASCENDING ids so the
    /// activated-bridge (<c>WindowResolverWiring</c>) sequences the co-drained per-card triggers in add order
    /// (one at a time, no spurious order prompt). Allocated from the SHARED deletion counter
    /// (<c>EngineContext.NextSecurityAddBatchId</c>) so it lives in the SAME globally-unique id space as
    /// deletion/discard/add-hand/security-loss — a mixed drain never collides in the window's cross-batch ordering.
    /// Sentinel 0 = an unstamped security move (a context-less bare unit test); the reader falls back to the
    /// driving event's monotonic <c>Sequence</c> so those still sequence per-card.</summary>
    public const string AddSecurityBatchIdKey = "addSecurityBatchId";

    /// <summary>(F1 reveal-remainder) Boolean flag stamped on a reveal-remainder trash mutation and threaded onto
    /// its Library-&gt;Trash CardMoved — the headless mirror of the AS-IS <c>CardSource.IsBeingRevealed</c> being
    /// TRUE at the moment a revealed remainder card is trashed. AS-IS <c>RevealDeckTopCardsAndSelect</c> sends the
    /// unselected remainder through <c>TrashRevealedCards</c> → <c>TrashDeckCards</c> (which broadcasts
    /// <c>OnDiscardLibrary</c>, CardController.cs:5816) BEFORE resetting <c>IsBeingRevealed=false</c>
    /// (RevealLibrary.cs:174/464). So <c>CanTriggerWhenDiscardLibrary</c> (WhenDiscardLibrary.cs:23-26) any-matches
    /// only <c>!IsBeingRevealed</c> cards, excluding the whole reveal-trashed set. The headless reveal path
    /// (<c>SimplifiedRevealAndSelectEffect</c> / <c>RevealMultiSelectEffect</c> StageMove to Trash) stamps this so
    /// the ported gate (<c>CardPortingFramework.CanTriggerWhenDiscardLibrary</c>) reads it as the
    /// <c>IsBeingRevealed</c> mirror and rejects the discard. A DIRECT effect-driven library trash (a plain
    /// <c>TrashDeckCards</c> — <c>IsBeingRevealed==false</c>) does NOT carry this flag and fires normally.</summary>
    public const string RevealTrashFlagKey = "revealTrash";

    // W2-follow: async / controller-backed kinds (applied on flush or via the memory controller).
    public const string TrashCardKind = "TrashCard";
    public const string ReturnToHandKind = "ReturnToHand";
    public const string ReturnToDeckTopKind = "ReturnToDeckTop";
    public const string ReturnToDeckBottomKind = "ReturnToDeckBottom";
    public const string AddToSecurityKind = "AddToSecurity";
    public const string DrawCardsKind = "DrawCards";
    // B-6: effect-driven security operations (player-scoped batches over IZoneMover primitives).
    public const string RecoverKind = "Recover";              // top N library -> security (AS-IS IRecovery/IAddSecurityFromLibrary)
    public const string TrashSecurityKind = "TrashSecurity";  // N security -> trash (AS-IS IDestroySecurity); each Security->Trash CardMoved derives OnLoseSecurity + OnDiscardSecurity
    public const string ShuffleSecurityKind = "ShuffleSecurity"; // (BT1_087) shuffle the player's security stack (AS-IS RandomUtility.ShuffledDeckCards)
    // B-9: create N token Digimon on the controller's battle area (AS-IS CardEffectCommons.PlayToken).
    public const string CreateTokenKind = "CreateToken";
    public const string TokenDefinitionIdKey = "tokenDefinitionId";
    public const string TokenInstanceIdKey = "tokenInstanceId";
    public const string TokenTappedKey = "tokenTapped";
    public const string AddMemoryKind = "AddMemory";
    public const string SetMemoryKind = "SetMemory";
    // F-3.7: effect-driven play — moves the target from its source zone onto the battle area face up
    // and marks it as having entered this turn (summoning sickness). "Play for free"; a memory cost, if
    // any, is paid by the effect before emitting this mutation.
    public const string PlayCardKind = "PlayCard";
    // B-10: effect-driven trash / return of a Digimon's digivolution sources, and trash of its link cards.
    public const string TrashDigivolutionCardsKind = "TrashDigivolutionCards"; // target = host; count, fromBottom
    public const string ReturnDigivolutionCardsKind = "ReturnDigivolutionCards"; // target = host; count, toDeck
    public const string TrashLinkCardsKind = "TrashLinkCards";                   // target = host; count (default all)
    // (C-Act re-home) TrainKind retired: <Training> now fires through the AS-IS window/activated path
    // (CardEffectFactory.TrainingEffect -> SuspendPermanentsClass.Tap + Permanent.AddDigivolutionCardsBottom),
    // not this invented mutation. The old firing-half (TrainingActivatedEffect + this Kind + TrainAsync) is gone.
    public const string MaterialSaveKind = "MaterialSave";                       // (C-23) source = from-host; toEntityId, count -> move sources to another stack
    public const string ToEntityIdKey = "toEntityId";
    public const string ImmuneStackTrashingKey = "immuneStackTrashing"; // (PRIM-W4) host source-trash immunity flag
    public const string DeDigivolveKind = "DeDigivolve";                // (PRIM-W5) target = card; count -> remove N top digivolution cards
    // G10-007: play a SPECIFIC digivolution source out from under its host as a new battle-area Digimon
    // (cost-free). target = the under-card to play; HostEntityIdKey = the host it sits under.
    public const string PlayDigivolutionAsDigimonKind = "PlayDigivolutionAsDigimon";
    public const string HostEntityIdKey = "hostEntityId";
    public const string FromBottomKey = "fromBottom";
    public const string ToDeckKey = "toDeck";
    // (B-2 DigiBurst rework) explicit selected source ids (csv) for TrashDigivolutionCardsKind — the AS-IS
    // ITrashDigivolutionCards(permanent, selectedCards, …) shape; absent = positional count/fromBottom.
    public const string SelectedCardIdsKey = "selectedCardIds";

    // Value keys.
    public const string DpValueKey = "value";
    public const string DpAbsoluteKey = "absolute";
    public const string DpActivatedOrderKey = "activatedOrder";
    public const string FlagKeyKey = "flagKey";
    public const string PlayerIdKey = "playerId";
    public const string CountKey = "count";
    public const string FaceUpKey = "faceUp";
    public const string FromTopKey = "fromTop";
    // N-3: optional override to insert a returned card at the security BOTTOM instead of the default top.
    public const string ToBottomKey = "toBottom";
    public const string AmountKey = "amount";
    // F-3.7: the zone the played card comes from (defaults to Hand).
    public const string FromZoneKey = "fromZone";
    public const string EnteredThisTurnKey = "enteredThisTurn";
    // D-8: optional memory cost paid when an effect plays a card "for cost" (PlayForCost). The effect
    // resolves the (reduced) cost via the cost pipeline and passes it; absent/0 = play for free.
    public const string MemoryCostKey = "memoryCost";
    // (G3 / BT3_109/110) set on a PlayCard mutation to suppress the played card's OWN [On Play]/OnEnterField
    // triggers ("play … Any [On Play] effects on the Digimon played with this effect don't activate" —
    // AS-IS activateETB:false). Threaded onto the CardMoved event so AutoProcessingTriggerCollector drops the
    // moved card's own enter-play triggers (one-shot; other cards' reactions unaffected).
    public const string SuppressOnPlayKey = "suppressOnPlay";

    private static readonly IReadOnlyDictionary<string, string> KindToFlag =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GrantBlocker"] = "hasBlocker",
            ["GrantRush"] = "hasRush",
            // GR-007: align grant→consume. The Reboot/Piercing keyword mutations previously wrote dead flags
            // (scheduleRebootUnsuspend/pendingSecurityCheck) that NO consumer read, while the consumers
            // (HeadlessEarlyPhaseFlow / BattleResolver) read hasReboot/hasPiercing which NO mutation set —
            // so a keyword GRANTED via these mutations was inert. Map them to the consumer presence flags.
            ["ScheduleRebootUnsuspend"] = "hasReboot",
            ["PreventBattleDeletion"] = "preventBattleDeletion",
            ["SetSecurityCheck"] = "hasPiercing",
            // W2: previously-dropped keyword kinds now write a flag (consumers wired per keyword).
            ["RequestBlitzAttack"] = "hasBlitz",
            ["DeleteRetaliationTarget"] = "hasRetaliation",
            ["ApplyArmorPurge"] = "hasArmorPurge",
            // C-4 Decoy / C-5 Barrier / C-7 Evade: defense-keyword grants consumed by DeletionReplacementGate.
            ["GrantEvade"] = "hasEvade",
            ["GrantBarrier"] = "hasBarrier",
            ["GrantDecoy"] = "hasDecoy",
            ["GrantFortitude"] = "hasFortitude",
            // C-3 Raid: switch-defender keyword grant consumed by RaidAttackSwitch.
            ["GrantRaid"] = "hasRaid",
            // C-10 Collision: forced-block keyword grant consumed by BlockTiming.
            ["GrantCollision"] = "hasCollision",
            // C-9 Execute: grants consumed by AttackPermanentAction (attack unsuspended) + AttackPipeline
            // (self-delete at end of attack).
            ["GrantAttackUnsuspended"] = "canAttackUnsuspendedDigimon",
            ["GrantDeleteSelfAtEndOfAttack"] = "deleteSelfAtEndOfAttack",
            // C-11 Fragment / C-17 Ascension / C-19 Scapegoat: deletion-family grants consumed by
            // DeletionReplacementGate.
            ["GrantFragment"] = "hasFragment",
            ["GrantAscension"] = "hasAscension",
            ["GrantScapegoat"] = "hasScapegoat",
            // C-22 Save: post-deletion attach-to-stack grant consumed by DeletionReplacementGate.
            ["GrantSave"] = "hasSave",
            // C-12 Iceclad: battle-comparison keyword grant consumed by BattleResolver (compare by
            // digivolution-source count instead of DP when either combatant has it).
            ["GrantIceclad"] = "hasIceclad",
            // C-13 Decode: post-(effect)-removal play-a-source-for-free grant consumed by DeletionReplacementTiming.
            ["GrantDecode"] = "hasDecode",
            // C-18 Alliance: on-attack suspend-an-ally boost grant consumed by AllianceAttackBoost.
            ["GrantAlliance"] = "hasAlliance",
            // C-20 Vortex (S1): effect-driven attack grant consumed by EffectDrivenAttack.
            ["GrantVortex"] = "hasVortex",
            // C-16 Overclock (S3+S1): end-of-turn delete-trait-ally + untapped attack grant consumed by OverclockEffect.
            ["GrantOverclock"] = "hasOverclock",
            // C-14 Partition (S4): post-(effect)-removal play-two-sources-free grant consumed by DeletionReplacementTiming.
            ["GrantPartition"] = "hasPartition",
            // C-15 Progress (S2): attack-time opponent-effect immunity grant consumed by ProgressImmunity/ContinuousImmunityGate.
            ["GrantProgress"] = "hasProgress",
        };

    private readonly ICardInstanceRepository _repository;
    private readonly IZoneMover? _zoneMover;
    private readonly IHeadlessMemoryController? _memory;
    private readonly EffectRegistry? _effectRegistry;
    private readonly GameEventQueue? _gameEventQueue;
    private readonly ILogSink? _log;
    private readonly List<AppliedMutation> _applied = new();
    private readonly List<EffectMutation> _unsupported = new();
    private readonly List<EffectMutation> _skipped = new();
    private readonly List<Func<CancellationToken, Task>> _pendingAsync = new();

    private readonly Action<HeadlessEntityId, HeadlessPlayerId>? _onCardEnteredPlay;
    // (PRIM-W4 AceOverflow) supplies the current turn player so a leaving ACE's memory penalty is applied with
    // the correct turn-relative sign. Null in contexts without turn state (falls back to owner-is-active).
    private readonly Func<HeadlessPlayerId?>? _currentTurnPlayer;
    // (FR-P3) the engine context, used so the restriction/immunity checks below can honour PLAYER-SCOPE
    // effects with an arbitrary permanentCondition predicate ("your <X> Digimon cannot be ...") — not just the
    // card's own self restriction. Null in contexts without a full EngineContext (falls back to self-only).
    private readonly EngineContext? _context;

    // (D-1 / VR-8) delete-batch id for THIS sink. When an explicit id is supplied (the DP-zero rule sweep passes
    // ONE id for all lack-DP deaths — AS-IS DigimonLackDPProcess is a SINGLE DestroyPermanentsClass(LackPowerPermanents)),
    // every Delete this sink emits stamps it. Otherwise a fresh id is allocated LAZILY on the first Delete and reused
    // for the rest of the sink's flush — mirroring "one effect resolution's sink flush == one Destroy() == one batch".
    private readonly long? _explicitDeletionBatchId;
    private long? _cachedDeletionBatchId;

    // (F1-Tier1 OnDiscard*) the DISCARD batch id shared by every hand/library trash this sink flush stages — one
    // id per sink == one AS-IS logical discard operation (DiscardHands / TrashDeckCards each fire OnDiscard* ONCE
    // for the whole list), so an effect discarding N cards fires the reactor once. Lazily allocated on the first
    // discard; a context-less sink (bare unit test) leaves it 0 (sentinel — collapse-all-together).
    private long? _cachedDiscardBatchId;

    // (F1-Tier1 OnAddHand) the ADD-HAND batch id shared by every ->Hand move (draw / return-to-hand) this sink
    // flush stages — one id per sink == one AS-IS AddHandCards call (fires OnAddHand ONCE for the whole added list).
    // Lazily allocated on the first hand add; a context-less sink (bare unit test) leaves it 0 (collapse-all-together).
    private long? _cachedAddHandBatchId;

    // (R2-P1-1) the CURRENT delete batch: every Delete staged before the next flush belongs to ONE batch (one
    // AS-IS DestroyPermanentsClass.Destroy() over the whole target list). The defer decision is BATCH-ATOMIC —
    // resolved once, at the first delete thunk's execution (all targets staged, none moved yet — the AS-IS
    // "willBeRemoveField on ALL, then one cut-in over the LIST" moment). Reset at flush so a reused sink
    // starts a fresh batch.
    private DeleteBatch? _currentDeleteBatch;

    private sealed class DeleteBatch
    {
        public List<StagedDelete> Entries { get; } = new();

        /// <summary>null until the first delete thunk resolves it; then: true = EVERY entry parks as a
        /// pending deletion (some entry has a PRE option), false = every entry moves immediately.</summary>
        public bool? DeferAll;

        /// <summary>(C2 decision-4) set once the immediate (non-deferred) path has opened this batch's collect-
        /// before-removal OnDestroyedAnyone / OnLeaveFieldAnyone window, so a multi-entry batch stacks the dead
        /// cards' reactors ONCE (AS-IS single DestroyPermanentsClass StackSkillInfos pair), not per entry.</summary>
        public bool OnDeletionWindowOpened;

        /// <summary>(C-Del 3b PRE transport) set once the non-deferred path has opened this batch's AS-IS PRE
        /// cut-in "would be deleted" window (WhenPermanentWouldBeDeleted → WhenRemoveField), marking every field-
        /// present member's <c>willBeRemoveField=true</c> ONCE (AS-IS DestroyPermanentsClass :3448) before the
        /// survivor fix. Per-entry survivor read (<c>willBeRemoveField</c>) then spares any card a replacement
        /// cancelled.</summary>
        public bool PreWindowOpened;

        /// <summary>(C-Del 3c-1 promote-to-defer) set once the PRE cut-in drain PAUSED on an INTERACTIVE
        /// would-be-deleted replacement (a "will you use Evade?" agent choice). The batch's field-present members
        /// were all flagged <c>pendingDeletion</c> (leaving the parked cut-in window to carry the choice), so
        /// <see cref="FlushAsync"/> SWALLOWS the pause (the enclosing pick's flush completes — no body re-run) and
        /// the <c>GameFlowProcessor</c> sweep finalizes the batch on <c>willBeRemoveField</c> once the agent
        /// resolves the parked window. Every later thunk of this batch short-circuits (already promoted).</summary>
        public bool PromotedToDefer;
    }

    private sealed record StagedDelete(EffectMutation Mutation, HeadlessEntityId TargetId);

    public MatchStateMutationSink(
        ICardInstanceRepository repository,
        ILogSink? log = null,
        IZoneMover? zoneMover = null,
        IHeadlessMemoryController? memory = null,
        EffectRegistry? effectRegistry = null,
        GameEventQueue? gameEventQueue = null,
        Action<HeadlessEntityId, HeadlessPlayerId>? onCardEnteredPlay = null,
        Func<HeadlessPlayerId?>? currentTurnPlayer = null,
        EngineContext? context = null,
        long? explicitDeletionBatchId = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _log = log;
        _zoneMover = zoneMover;
        _memory = memory;
        _effectRegistry = effectRegistry;
        _gameEventQueue = gameEventQueue;
        // (G8-002 / effect-play registration) Invoked when a sink plays a card onto the field (PlayCardKind /
        // PlayDigivolutionAsDigimonKind), so the played card's ported continuous/trigger effects auto-register.
        // Default to the context's enter-play chokepoint whenever a context is present, so EVERY sink that can
        // play a card registers it — not only the action-layer scheduler sink. This mirrors AS-IS, where every
        // play routes through PlayCardClass.PlayCard(); an explicit hook (if supplied) still wins. A context-less
        // sink (bare unit test) leaves it null. Future ported cards that play via any sink path register for free.
        _onCardEnteredPlay = onCardEnteredPlay
            ?? (context is null ? null : context.RegisterEnteredCardEffects);
        _currentTurnPlayer = currentTurnPlayer;
        _context = context;
        _explicitDeletionBatchId = explicitDeletionBatchId;
    }

    // (D-1 / VR-8) Resolve THIS sink's delete-batch id: an explicit id (DP-zero sweep) always wins; otherwise a
    // fresh context id is allocated ONCE (lazily, on the first Delete) and cached for every later Delete in this
    // flush. A context-less sink (bare unit test) has no counter — batch id 0 (unstamped) collapses all-together,
    // preserving the pre-D1 whole-pass dedup for such paths.
    private long ResolveDeletionBatchId()
    {
        if (_explicitDeletionBatchId is long fixedId)
        {
            return fixedId;
        }

        _cachedDeletionBatchId ??= _context?.NextDeletionBatchId() ?? 0L;
        return _cachedDeletionBatchId.Value;
    }

    // (F1-Tier1 OnDiscard*) the discard batch id for this sink flush — one id shared by all hand/library trashes
    // it stages (== one AS-IS StackSkillInfos(OnDiscard*) over the whole discarded list).
    private long ResolveDiscardBatchId()
    {
        _cachedDiscardBatchId ??= _context?.NextDiscardBatchId() ?? 0L;
        return _cachedDiscardBatchId.Value;
    }

    // (F1-Tier1 OnAddHand) the add-hand batch id for this sink flush — one id shared by all ->Hand moves it
    // stages (== one AS-IS StackSkillInfos(OnAddHand) over the whole added list).
    //
    // design item F1-ADDHAND-FLUSHGRAIN (deferred, adversarial review P2-2): the cache grain is the sink FLUSH,
    // NOT the AS-IS AddHandCards(list, isDraw, cardEffect) CALL. AS-IS fires ONE StackSkillInfos(OnAddHand) per
    // AddHandCards call, so a single effect that BOTH draws (isDraw:true) AND returns cards to hand (isDraw:false)
    // is TWO AddHandCards calls == TWO OnAddHand broadcasts; here both stage under one flush and share this one id,
    // so the activated bridge collapses them to a SINGLE OnAddHand fire (a 1-id under-fire vs AS-IS 2). This is
    // LATENT: no ported card both draws and returns-to-hand in one effect (grain divergence unobservable today).
    // Correct remediation: partition the id by AddHandCards boundary — a separate cached id for the DRAW list vs
    // the RETURN-TO-HAND list within a flush (isDraw is the discriminator), each its own NextAddHandBatchId, so
    // the bridge fires OnAddHand once per boundary == the AS-IS two broadcasts. (Deferred until a witness exists.)
    private long ResolveAddHandBatchId()
    {
        _cachedAddHandBatchId ??= _context?.NextAddHandBatchId() ?? 0L;
        return _cachedAddHandBatchId.Value;
    }

    public int AppliedCount => _applied.Count;

    public int UnsupportedCount => _unsupported.Count;

    public int SkippedCount => _skipped.Count;

    public IReadOnlyList<AppliedMutation> Applied => _applied.ToArray();

    public IReadOnlyList<EffectMutation> Unsupported => _unsupported.ToArray();

    public IReadOnlyList<EffectMutation> Skipped => _skipped.ToArray();

    public void Apply(EffectMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        // (B-1 rework, mutation replay journal) During a uniform-cycle REPLAY (a suspended resolution re-invoked
        // after an agent answer), an Apply call the original run performed PURELY IMMEDIATELY (no pending thunk)
        // already mutated game state and must NOT re-apply (double memory / double DP / double timing events); a
        // call that staged pending work must re-execute so this FRESH sink re-stages the thunks the suspend
        // discarded. Fresh calls beyond the journal execute and record their classification.
        OnceFlagController.MutationReplay replay =
            _context?.OnceFlags.BeginMutationApply() ?? OnceFlagController.MutationReplay.None;
        if (replay == OnceFlagController.MutationReplay.Skip)
        {
            _applied.Add(new AppliedMutation(mutation.Kind, ResolveTargetId(mutation), "replayed"));
            return;
        }

        int pendingBefore = _pendingAsync.Count;
        ApplyCore(mutation);
        if (replay == OnceFlagController.MutationReplay.Fresh)
        {
            _context!.OnceFlags.RecordFreshMutation(purelyImmediate: _pendingAsync.Count == pendingBefore);
        }
    }

    private void ApplyCore(EffectMutation mutation)
    {
        // Unknown kinds are reported as unsupported BEFORE the target is checked, so an effect that
        // emits a mutation this sink does not understand is surfaced regardless of its target.
        if (!IsKnownKind(mutation.Kind))
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Unsupported effect mutation kind '{mutation.Kind}'; no MatchState mapping.");
            return;
        }

        // Player-scoped / global mutations (no specific card target).
        switch (mutation.Kind)
        {
            case AddMemoryKind:
                ApplyMemory(mutation, isSet: false);
                return;
            case SetMemoryKind:
                ApplyMemory(mutation, isSet: true);
                return;
            case DrawCardsKind:
                ApplyDraw(mutation);
                return;
            case RecoverKind:
                ApplyRecover(mutation);
                return;
            case TrashSecurityKind:
                ApplyTrashSecurity(mutation);
                return;
            case ShuffleSecurityKind:
                ApplyShuffleSecurity(mutation);
                return;
            case CreateTokenKind:
                ApplyCreateToken(mutation);
                return;
        }

        HeadlessEntityId targetId = ResolveTargetId(mutation);
        if (targetId.IsEmpty
            || !_repository.TryGetInstance(targetId, out CardInstanceRecord? record)
            || record is null)
        {
            _skipped.Add(mutation);
            _log?.Warn(
                $"Effect mutation '{mutation.Kind}' targeted card '{targetId.Value}' which is not in the instance repository.");
            return;
        }

        // S2 (C-15 Progress): an active opponent-only immunity on the target prevents an opponent-sourced
        // effect mutation. No-op unless an immunity is active (source-relativity skips own/ally effects).
        // (B군 P0-1) Rehomed from the now-dead ContinuousImmunityGate.BlocksOpponentEffect registry scan (0
        // producers after the W3c-1/2 CanNotAffectedStaticEffect→CanNotAffectedClass + ProgressImmunity→bucket
        // flips) to the AS-IS-literal live getter TopCard.CanNotBeAffected — the same per-target gate the AS-IS
        // mutation carriers apply (DestroyPermanentsClass CardController.cs:3679, HandBounce :2632,
        // ReturnToDeckTop :2471, ReturnToDeckBottom :2305, PutSecurity :3525). The causing effect is collapsed to
        // its source card (BareCauseEffect). Needs the EngineContext; a context-less unit sink never produced a
        // live immunity, so it reports none — matching AS-IS, which always runs with full CardSource views.
        if (_context is { } immunityCtx
            && new Assets.Scripts.Script.CardEffectCommons.Permanent(immunityCtx, targetId, record.OwnerId)
                .TopCard.CanNotBeAffected(Assets.Scripts.Script.CardEffectCommons.BareCauseEffect.For(immunityCtx, mutation.SourceEntityId)))
        {
            _skipped.Add(mutation);
            _log?.Warn($"Effect mutation '{mutation.Kind}' on '{targetId.Value}' was prevented by immunity (opponent effect).");
            return;
        }

        if (KindToFlag.TryGetValue(mutation.Kind, out string? flagKey))
        {
            WriteMetadata(record, targetId, mutation.Kind, flagKey, true);
            return;
        }

        switch (mutation.Kind)
        {
            case AddDpModifierKind:
                ApplyDpModifier(mutation, record, targetId);
                break;
            case SuspendKind:
                // (PRIM-W4 CantSuspendStaticEffect) a continuous "cannot suspend" restriction blocks it.
                // (R2-D) the new-model interface scan is now read through its AS-IS home — the mirror
                // Permanent.CanSuspend getter (AS-IS Permanent.CanSuspend, the ICanNotSuspendEffect scan over all
                // players' field permanents + players). This replaces the former NewModelContinuousScan.CanNotSuspend
                // helper call (byte-identical scan) with the getter, matching the established R1-d consumers
                // (CardController / CardEffectCommons read `permanent.CanSuspend` directly). The legacy registry arm
                // (HasSelfRestriction, via ScopedResult) stays UNIONed unchanged — a ported CantSuspendStaticEffect
                // registers no legacy binding, so the getter catches what the registry misses (and vice-versa),
                // symmetric with the deletion path below (BattleDeletionGate keeps the same two-arm union).
                if (HasSelfRestriction(targetId, CannotRestrictionKind.Suspend)
                    || (_context is not null && !new Assets.Scripts.Script.CardEffectCommons.Permanent(_context, targetId).CanSuspend))
                {
                    _skipped.Add(mutation);
                    _applied.Add(new AppliedMutation(mutation.Kind, targetId, "restricted"));
                    break;
                }

                WriteMetadata(record, targetId, mutation.Kind, SuspendedFlagKey, true);
                EmitTiming(TriggerTimings.OnTapped, record.OwnerId, subject: targetId);
                break;
            case UnsuspendKind:
                WriteMetadata(record, targetId, mutation.Kind, SuspendedFlagKey, false);
                EmitTiming(TriggerTimings.OnUntapped, record.OwnerId, subject: targetId);
                break;
            case SetFlagKind:
                ApplyNamedFlag(mutation, record, targetId, value: true);
                break;
            case ClearFlagKind:
                ApplyNamedFlag(mutation, record, targetId, value: false);
                break;
            case DeleteKind:
                ApplyDelete(mutation, record, targetId);
                break;
            case TrashCardKind:
                ApplyTrashCard(mutation, record, targetId);
                break;
            case ReturnToHandKind:
                // (PRIM-W4/FR2 CannotReturnToHandStaticEffect) a continuous "cannot return to hand" restriction
                // blocks it — honouring any cardEffectCondition against the causing effect's source. (d-remediation)
                // the broader "cannot be removed from the field except by deletion" (CanNotBeRemoved) also blocks it.
                if (IsRestrictedFromCause(targetId, Assets.Scripts.Script.CardEffectCommons.RestrictionHelpers.CannotReturnToHandKey, mutation.SourceEntityId) || IsRemovalBlockedByScan(targetId))
                {
                    _skipped.Add(mutation);
                    _applied.Add(new AppliedMutation(mutation.Kind, targetId, "restricted"));
                    break;
                }

                ApplyAceOverflowOnLeave(record, targetId, mutation);
                // (F1-Tier1 OnAddHand) an effect-driven return-to-hand is one AS-IS AddHandCards(list, isDraw:false,
                // cardEffect): stamp the shared add-hand batch id (N cards returned by one effect fire OnAddHand ONCE)
                // and the CAUSE effect source (AS-IS cardEffect != null). N ReturnToHand mutations of one flush share
                // ONE cached id, mirroring the single AddHandCards call.
                long returnHandBatchId = ResolveAddHandBatchId();
                HeadlessEntityId returnHandCause = mutation.SourceEntityId;
                ApplyZoneMove(mutation, record, targetId, (zm, owner, id, ct) => zm.AddToHandAsync(
                    owner, id, returnHandBatchId, returnHandCause.IsEmpty ? null : returnHandCause, ct));
                break;
            case ReturnToDeckTopKind:
                if (IsRestrictedFromCause(targetId, Assets.Scripts.Script.CardEffectCommons.RestrictionHelpers.CannotReturnToDeckKey, mutation.SourceEntityId) || IsRemovalBlockedByScan(targetId))
                {
                    _skipped.Add(mutation);
                    _applied.Add(new AppliedMutation(mutation.Kind, targetId, "restricted"));
                    break;
                }

                ApplyAceOverflowOnLeave(record, targetId, mutation);
                ApplyZoneMove(mutation, record, targetId, (zm, owner, id, ct) => zm.MoveToDeckTopAsync(owner, id, ct));
                break;
            case ReturnToDeckBottomKind:
                // (PRIM-W4 CannotReturnToDeckStaticEffect) a continuous "cannot return to deck" restriction blocks it;
                // (d-remediation) as does the broader CanNotBeRemoved ("can't leave the field except by deletion").
                if (IsRestrictedFromCause(targetId, Assets.Scripts.Script.CardEffectCommons.RestrictionHelpers.CannotReturnToDeckKey, mutation.SourceEntityId) || IsRemovalBlockedByScan(targetId))
                {
                    _skipped.Add(mutation);
                    _applied.Add(new AppliedMutation(mutation.Kind, targetId, "restricted"));
                    break;
                }

                ApplyAceOverflowOnLeave(record, targetId, mutation);
                ApplyZoneMove(mutation, record, targetId, (zm, owner, id, ct) => zm.MoveToDeckBottomAsync(owner, id, ct));
                break;
            case AddToSecurityKind:
                // (PRIM-P0 B.O.6) a player-scope "cannot add security" restriction blocks the add (AS-IS
                // Player.CanAddSecurity gate consulted before every AddSecurityCard).
                if (IsPlayerRestricted(record.OwnerId, Assets.Scripts.Script.CardEffectCommons.RestrictionHelpers.CannotAddSecurityKey, mutation.SourceEntityId))
                {
                    _skipped.Add(mutation);
                    _applied.Add(new AppliedMutation(mutation.Kind, targetId, "restricted"));
                    break;
                }

                bool faceUp = ReadBool(mutation.Values, FaceUpKey);
                // N-3: default to the security TOP (original AddSecurityCard toTop:true). An effect that
                // needs a bottom insert sets the "toBottom" flag on the mutation.
                bool toTop = !ReadBool(mutation.Values, ToBottomKey);
                // (F1-Tier1 OnAddSecurity P2-1) one added card == one AS-IS IAddSecurity == one per-card
                // OnAddSecurity: allocate a distinct shared-counter id (a context-less bare sink leaves it null).
                long? addSecurityBatchId = _context?.NextSecurityAddBatchId();
                ApplyZoneMove(mutation, record, targetId, (zm, owner, id, ct) => zm.AddToSecurityAsync(owner, id, faceUp, toTop, addSecurityBatchId, ct));
                // (faceup security) persist the AS-IS SetFace/SetReverse face state so the continuous-source
                // scan can find a face-up security card (Runtime.SecurityFaceState).
                Runtime.SecurityFaceState.Stamp(_repository, targetId, faceUp);
                // F-6.4: a face-up add raises the face-up security count — open that timing window.
                if (faceUp)
                {
                    EmitTiming(TriggerTimings.OnFaceUpSecurityIncreased, record.OwnerId);
                }
                break;
            case PlayCardKind:
                ApplyPlayCard(mutation, record, targetId);
                break;
            case PlayDigivolutionAsDigimonKind:
                ApplyPlayDigivolutionAsDigimon(mutation, record, targetId);
                break;
            case TrashDigivolutionCardsKind:
                ApplyDigivolutionSourceRemoval(mutation, targetId, returnToZone: null);
                break;
            case ReturnDigivolutionCardsKind:
                ApplyDigivolutionSourceRemoval(mutation, targetId,
                    returnToZone: ReadBool(mutation.Values, ToDeckKey) ? ChoiceZone.Library : ChoiceZone.Hand);
                break;
            case TrashLinkCardsKind:
                ApplyTrashLinkCards(mutation, targetId);
                break;
            case DeDigivolveKind:
                // (PRIM-W5 DigivolveIntoHandOrTrashCard) remove N top digivolution cards from the target.
                // (b-remediation; R3-W3c-4c B5 flip) AS-IS checks Permanent.ImmuneFromDeDigivolve() first — a
                // CONTINUOUS "cannot be de-digivolved" restriction (ImmuneFromDeDigivolveClass) is honoured here
                // before scheduling the removal (DeDigivolveAsync itself still handles a metadata-stamped flag on
                // the live top). NEW-MODEL: routes through the AS-IS-literal LIVE getter
                // (DeDigivolveHelpers.IsDeDigivolveImmune → Permanent.ImmuneFromDeDigivolve, a subject-only scan
                // with NO causing-effect gate — matching the AS-IS signature) instead of the registry cause-scan.
                if (_context is not null && Runtime.DeDigivolveHelpers.IsDeDigivolveImmune(_context, targetId))
                {
                    _skipped.Add(mutation);
                    _applied.Add(new AppliedMutation(mutation.Kind, targetId, "restricted"));
                    break;
                }

                if (_zoneMover is { } ddMover)
                {
                    int ddCount = ReadInt(mutation.Values, CountKey) ?? 1;
                    _pendingAsync.Add(ct => DeDigivolveHelpers.DeDigivolveAsync(_repository, ddMover, targetId, ddCount, _gameEventQueue, ct));
                    _applied.Add(new AppliedMutation(mutation.Kind, targetId, DeDigivolveKind));
                }
                else
                {
                    _unsupported.Add(mutation);
                }

                break;
            case MaterialSaveKind:
                // (C-23 Material Save) re-parent `count` of the source-host's digivolution cards to another
                // Digimon's stack (toEntityId). Source = the mutation's source (from-host).
                if (mutation.Values.TryGetValue(ToEntityIdKey, out object? toRaw) && toRaw is string toValue && !string.IsNullOrWhiteSpace(toValue))
                {
                    int saveCount = ReadInt(mutation.Values, CountKey) ?? 1;
                    // (F1-Tier2 OnAddDigivolutionCards) Material Save's MoveSourcesBottom IS the real add (cause = the
                    // Material Save effect's source = the mutation source card), so it fires.
                    DigivolutionStackHelpers.MoveSourcesBottom(_repository, mutation.SourceEntityId, new HeadlessEntityId(toValue), saveCount,
                        gameEventQueue: _gameEventQueue, causeSourceId: mutation.SourceEntityId);
                    _applied.Add(new AppliedMutation(mutation.Kind, targetId, MaterialSaveKind));
                }
                else
                {
                    _unsupported.Add(mutation);
                }

                break;
            default:
                _unsupported.Add(mutation);
                _log?.Warn($"Unsupported effect mutation kind '{mutation.Kind}'; no MatchState mapping.");
                break;
        }
    }

    /// <summary>Applies pending asynchronous zone moves / draws deferred by <see cref="Apply"/>.</summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingAsync.Count == 0)
        {
            return;
        }

        Func<CancellationToken, Task>[] operations = _pendingAsync.ToArray();
        _pendingAsync.Clear();
        // (R2-P1-1) the staged delete thunks hold their batch by reference; detach it so a post-flush Apply
        // opens a NEW batch (a reused sink = a new resolution step) instead of extending the executed one.
        DeleteBatch? flushedDeleteBatch = _currentDeleteBatch;
        _currentDeleteBatch = null;
        // (F1-Tier1) the staged discard thunks captured their batch id by value; clear the cache so a reused sink
        // (a new resolution step) opens a FRESH discard batch rather than collapsing into the flushed one.
        _cachedDiscardBatchId = null;
        _cachedAddHandBatchId = null;
        for (int opIndex = 0; opIndex < operations.Length; opIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await operations[opIndex](cancellationToken).ConfigureAwait(false);
            }
            catch (Exception opEx) when ((opEx is WindowChoicePendingException or Runtime.DeferredChoicePendingException)
                && flushedDeleteBatch is { PromotedToDefer: true })
            {
                // (C-Del 3c-1 promote-to-defer) an INTERACTIVE PRE cut-in replacement paused this batch's inline
                // drain; PromoteBatchToPendingDeletion already flagged every member pendingDeletion. SWALLOW the
                // pause — the flush is logically complete for this caller (the enclosing pick does NOT re-run its
                // body), and the parked ForCutIn window keeps the pending choice: RunToStable pauses on it, the
                // agent resolves it (resuming the ForCutIn pool), and the GameFlowProcessor sweep finalizes the
                // batch on willBeRemoveField.
                //
                // (RD-3C1-01) The remaining staged operations of THIS flush are the effect's TRAILING statements
                // that AS-IS runs when its `yield return Destroy()` coroutine RESUMES (e.g. the DRAW of "delete an
                // enemy Digimon; then draw 1"). The former code ABANDONED them (silent loss of the trailing draw).
                // Instead, stash them (in original flush order) under this batch's id: StateBasedDeletionSweepAsync
                // REPLAYS them once the batch fully finalizes (cut-in resolved, survivors spared / casualties
                // trashed), mirroring the AS-IS coroutine executing its trailing statements after Destroy() returns.
                // Any same-batch delete thunk left in the slice re-invokes ExecuteStagedDeleteAsync, which
                // short-circuits (batch.PromotedToDefer) — a harmless no-op that keeps the ORIGINAL order intact.
                if (_context is not null && opIndex + 1 < operations.Length)
                {
                    var trailing = new Func<CancellationToken, Task>[operations.Length - (opIndex + 1)];
                    Array.Copy(operations, opIndex + 1, trailing, 0, trailing.Length);
                    _context.StashPromotedBatchTrailingOps(ResolveDeletionBatchId(), trailing);
                }

                break;
            }
        }

        // (C2 decision-4 batch sequencing — the SequenceByMinimumBatch analogue at THIS feeder) AS-IS resolves
        // each delete-PROCESS's OnDeletion window before the next process runs (each pick's RuleProcess →
        // TriggeredSkillProcess; stage 4's per-Destroy inline resolution). Two sink flushes with no drain between
        // them (a state-based sweep pass; a direct-sink resolution step) would otherwise CO-STACK two batches into
        // ONE drained window — surfacing a cross-batch order choice AS-IS never offers. So when THIS flush opened
        // a deletion window and NO enclosing window drain will run it (executingMultipleSkills == null — a nested
        // flush inside a window pick defers to that pick's PassTail, the AS-IS position), drain it here, once,
        // before returning to the caller. A choice suspension propagates as the normal pause contract.
        if (flushedDeleteBatch is { OnDeletionWindowOpened: true } && _context is not null)
        {
            using AmbientMatchContext.Scope _drainScope = AmbientMatchContext.Enter(_context);
            var drainAutoProcessing = Assets.Scripts.Script.AutoProcessing.For(_context);
            if (drainAutoProcessing.executingMultipleSkills is null && drainAutoProcessing.StackedSkillInfos.Count > 0)
            {
                try
                {
                    await drainAutoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception drainEx) when (drainEx is WindowChoicePendingException or Runtime.DeferredChoicePendingException)
                {
                    // The window suspended for an agent choice — the FLUSH itself is complete (every move ran);
                    // the parked window resumes via ResumeSuspendedWindowsAsync when the pending choice resolves.
                }
            }
        }
    }

    private void ApplyMemory(EffectMutation mutation, bool isSet)
    {
        if (_memory is null)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a memory controller; none is wired.");
            return;
        }

        int amount = ReadInt(mutation.Values, AmountKey) ?? 0;

        // (PRIM-P0 B.O.6) a "cannot add memory" restriction blocks a memory GAIN (AS-IS Player.CanAddMemory —
        // gated on the GAINING player, `this` = gainer, Player.cs:1030). SetMemory (isSet) is not routed here.
        // (B군 P1-1) The mirror gauge is TURN-PLAYER-RELATIVE (Player.MemoryForPlayer :504-511 / AceOverflowGate
        // .MemoryDelta): a POSITIVE delta raises the TURN player's memory (the turn player gains), a NEGATIVE delta
        // raises the OPPONENT's (the opponent gains). So the real gainer is derived from the delta SIGN — NOT the
        // causing effect's owner (`src.OwnerId`), which the earlier port keyed on. That mis-key gated the wrong
        // player whenever a NON-turn-player effect drove the gain, and — because it only fired for `amount > 0` —
        // never gated an opponent-gain expressed as a negative delta. AS-IS routes each gain through the gaining
        // Player's own CanAddMemory; we reproduce that by resolving the gainer from the sign and scanning it.
        // (A null cause is not gated, mirroring AS-IS `if (cardEffect != null)` — the empty-source guard.)
        if (!isSet && amount != 0 && !mutation.SourceEntityId.IsEmpty && _context is { } memoryCtx
            && memoryCtx.TurnController.Current.TurnPlayerId is { } turnPlayer)
        {
            HeadlessPlayerId gainer;
            if (amount > 0)
            {
                gainer = turnPlayer;   // +delta raises the turn player's memory
            }
            else
            {
                // -delta raises the OPPONENT's memory (turn-player-relative gauge) — the opponent is the gainer.
                Assets.Scripts.Script.CardEffectCommons.Player? opponent =
                    new Assets.Scripts.Script.CardEffectCommons.Player(memoryCtx, turnPlayer).Enemy;
                gainer = opponent is null ? default : opponent.PlayerId;
            }

            if (!gainer.IsEmpty
                && IsPlayerRestricted(gainer, Assets.Scripts.Script.CardEffectCommons.RestrictionHelpers.CannotAddMemoryKey, mutation.SourceEntityId))
            {
                _skipped.Add(mutation);
                _applied.Add(new AppliedMutation(mutation.Kind, mutation.SourceEntityId, "restricted"));
                return;
            }
        }

        if (isSet)
        {
            _memory.Set(amount);
        }
        else
        {
            _memory.Add(amount);
        }

        _applied.Add(new AppliedMutation(mutation.Kind, mutation.SourceEntityId, "memory"));
    }

    private void ApplyDraw(EffectMutation mutation)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        HeadlessPlayerId player = ReadPlayer(mutation.Values, PlayerIdKey);
        if (player.IsEmpty)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' is missing a '{PlayerIdKey}' value.");
            return;
        }

        int count = ReadInt(mutation.Values, CountKey) ?? 1;
        // (F1-Tier1 OnAddHand) an effect-driven draw is one AS-IS AddHandCards(DrawCards, isDraw:true, cardEffect):
        // stamp the shared add-hand batch id (so N drawn cards fire an OnAddHand reactor ONCE) and the CAUSE effect
        // source (mirrors AS-IS cardEffect != null — so the reactor's CanTriggerOnHandAdded gate accepts this
        // effect-driven add). A context-less sink leaves the id 0 (unstamped). Turn/mulligan/setup draws do NOT
        // route through this sink path, so they carry neither id and never fire OnAddHand (AS-IS cardEffect=null).
        HeadlessEntityId drawCause = mutation.SourceEntityId;
        if (_context is { } drawContext)
        {
            // (MIG3-3a) the mirror DrawClass is the AS-IS carrier (CardController.cs:1903-1965): DrawAsync +
            // the OnDraw window emit the legacy staging below never fired (an effect-driven draw silently
            // skipped OnDraw reactors — latent gap, now closed).
            _pendingAsync.Add(ct => new Assets.Scripts.Script.DrawClass(
                drawContext, player, count, drawCause.IsEmpty ? null : drawCause).Draw(ct));
        }
        else
        {
            long addHandBatchId = ResolveAddHandBatchId();
            _pendingAsync.Add(ct => zoneMover.DrawAsync(
                player, count, addHandBatchId, drawCause.IsEmpty ? null : drawCause, ct));
        }

        _applied.Add(new AppliedMutation(mutation.Kind, mutation.SourceEntityId, "draw"));
    }

    // B-6 Recovery: move the top N library cards into the player's security stack (AS-IS IRecovery →
    // IAddSecurityFromLibrary; face down by default). Player-scoped batch over the zone mover.
    private void ApplyRecover(EffectMutation mutation)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        HeadlessPlayerId player = ReadPlayer(mutation.Values, PlayerIdKey);
        if (player.IsEmpty)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' is missing a '{PlayerIdKey}' value.");
            return;
        }

        // (PRIM-P0 B.O.6) recovery adds to security — blocked by a "cannot add security" restriction.
        if (IsPlayerRestricted(player, Assets.Scripts.Script.CardEffectCommons.RestrictionHelpers.CannotAddSecurityKey, mutation.SourceEntityId))
        {
            _skipped.Add(mutation);
            _applied.Add(new AppliedMutation(mutation.Kind, mutation.SourceEntityId, "restricted"));
            return;
        }

        int count = ReadInt(mutation.Values, CountKey) ?? 1;
        bool faceUp = ReadBool(mutation.Values, FaceUpKey);
        if (_context is { } recoverContext)
        {
            // (MIG3-3a) the mirror IAddSecurityFromLibrary is the AS-IS carrier (CardController.cs:2041-2079 +
            // CardObjectController.AddSecurityCard:976-1007): per-card add-security batch ids + face stamps +
            // per-card mirror IAddSecurity — whose face-up branch fires OnFaceUpSecurityIncreased PER CARD
            // (AS-IS :5494), replacing the legacy single player-level emit below (batch-vs-per-card divergence).
            _pendingAsync.Add(ct => new Assets.Scripts.Script.IAddSecurityFromLibrary(
                recoverContext, player, count, faceUp).AddSecurity(ct));
            _applied.Add(new AppliedMutation(mutation.Kind, mutation.SourceEntityId, "recover"));
            return;
        }

        _pendingAsync.Add(async ct =>
        {
            // (F1-Tier1 OnAddSecurity P2-1) each recovered card gets its OWN shared-counter add-security id
            // (OnAddSecurity is per-card, not collapsed — AS-IS fires one StackSkillInfos per IAddSecurity),
            // allocated at move time so the N per-card triggers sequence in ascending add order. A context-less
            // bare sink leaves the factory null-yielding (unstamped).
            IReadOnlyList<HeadlessEntityId> moved = await zoneMover.AddSecurityFromLibraryAsync(
                player, count, faceUp, () => _context?.NextSecurityAddBatchId(), ct).ConfigureAwait(false);
            // (faceup security) persist the face state on each recovered card (AS-IS SetFace/SetReverse).
            foreach (HeadlessEntityId movedId in moved)
            {
                Runtime.SecurityFaceState.Stamp(_repository, movedId, faceUp);
            }
        });
        _applied.Add(new AppliedMutation(mutation.Kind, mutation.SourceEntityId, "recover"));
        if (faceUp)
        {
            EmitTiming(TriggerTimings.OnFaceUpSecurityIncreased, player);
        }
    }

    // B-6 Trash Security: trash N security cards from the top (or bottom) (AS-IS IDestroySecurity), then emit
    // OnDiscardSecurity so security-discard triggers fire. Player-scoped batch over the zone mover.
    // (MIG3-3a) the mirror Assets.Scripts.Script.IDestroySecurity carries the same F1-M1 semantics (one
    // security-loss batch id, zone-derived OnLoseSecurity/OnDiscardSecurity) plus the SelectedCard mode and the
    // CanReduceSecurity guard — unifying this handler onto it is slice 3c work (design item MIG3-TRASHSEC-UNIFY).
    private void ApplyTrashSecurity(EffectMutation mutation)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        HeadlessPlayerId player = ReadPlayer(mutation.Values, PlayerIdKey);
        if (player.IsEmpty)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' is missing a '{PlayerIdKey}' value.");
            return;
        }

        int count = ReadInt(mutation.Values, CountKey) ?? 1;
        bool fromTop = !mutation.Values.ContainsKey(FromTopKey) || ReadBool(mutation.Values, FromTopKey);
        // (F1-M1 P1-1) one IDestroySecurity == one IReduceSecurity == one OnLoseSecurity batch: allocate a single
        // security-loss id shared across the N trashed cards so the activated bridge fires an OnLoseSecurity reactor
        // ONCE for this effect (not per card). A context-less sink (bare unit test) leaves it null (sentinel 0).
        long? securityLossBatchId = _context?.NextSecurityLossBatchId();
        // (F1-Tier1) thread the CAUSE effect source so the Security->Trash CardMoved passes the OnDiscardSecurity
        // CardEffect!=null gate (this IS an effect-driven IDestroySecurity). The redundant explicit
        // EmitTiming(OnDiscardSecurity) was REMOVED: every trashed security card already emits a Security->Trash
        // CardMoved (carrying the subject + batch id + cause) that derives OnDiscardSecurity via TriggerTimingMap,
        // exactly as OnLoseSecurity does — the old subject-less explicit emit only produced a phantom broadcast the
        // gate rejected (no discarded card). Mirrors AS-IS's single StackSkillInfos(OnDiscardSecurity) per trash.
        HeadlessEntityId cause = mutation.SourceEntityId;
        _pendingAsync.Add(ct => zoneMover.TrashSecurityAsync(
            player, count, fromTop, securityLossBatchId, cause.IsEmpty ? null : cause, ct));
        _applied.Add(new AppliedMutation(mutation.Kind, mutation.SourceEntityId, "trashSecurity"));
    }

    // (BT1_087) Shuffle the player's security stack — a deferred zone shuffle so it flushes after any
    // preceding security moves (add-to-hand / recovery) staged on the same sink.
    private void ApplyShuffleSecurity(EffectMutation mutation)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        HeadlessPlayerId player = ReadPlayer(mutation.Values, PlayerIdKey);
        if (player.IsEmpty)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' is missing a '{PlayerIdKey}' value.");
            return;
        }

        _pendingAsync.Add(ct => zoneMover.ShuffleSecurityAsync(player, ct));
        _applied.Add(new AppliedMutation(mutation.Kind, mutation.SourceEntityId, "shuffleSecurity"));
    }

    // B-9 PlayToken: create N token Digimon (IsToken instances of the given token definition) on the
    // controller's battle area, summoning-sick (AS-IS CardEffectCommons.PlayToken). The token definition is
    // supplied by the effect (the porting layer registers token card data); ids are derived from the base
    // instance id (suffixed for quantity > 1).
    private void ApplyCreateToken(EffectMutation mutation)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        HeadlessPlayerId player = ReadPlayer(mutation.Values, PlayerIdKey);
        string? definitionId = ReadString(mutation.Values, TokenDefinitionIdKey);
        string? baseInstanceId = ReadString(mutation.Values, TokenInstanceIdKey);
        if (player.IsEmpty || string.IsNullOrWhiteSpace(definitionId) || string.IsNullOrWhiteSpace(baseInstanceId))
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires '{PlayerIdKey}', '{TokenDefinitionIdKey}' and '{TokenInstanceIdKey}' values.");
            return;
        }

        int count = Math.Max(1, ReadInt(mutation.Values, CountKey) ?? 1);
        bool tapped = ReadBool(mutation.Values, TokenTappedKey);
        for (int index = 1; index <= count; index++)
        {
            var tokenId = new HeadlessEntityId(index == 1 ? baseInstanceId : $"{baseInstanceId}#{index}");
            _repository.Upsert(new CardInstanceRecord(
                tokenId,
                new HeadlessEntityId(definitionId),
                player,
                IsToken: true,
                Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["enteredThisTurn"] = true,
                    [SuspendedFlagKey] = tapped,
                }));
            _pendingAsync.Add(ct => zoneMover.MoveAsync(
                new ZoneMoveRequest(player, tokenId, ChoiceZone.None, ChoiceZone.BattleArea, FaceUp: true), ct));
            _applied.Add(new AppliedMutation(mutation.Kind, tokenId, "createToken"));
        }
    }

    private void ApplyZoneMove(
        EffectMutation mutation,
        CardInstanceRecord record,
        HeadlessEntityId targetId,
        Func<IZoneMover, HeadlessPlayerId, HeadlessEntityId, CancellationToken, Task> move)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        HeadlessPlayerId owner = record.OwnerId;
        _pendingAsync.Add(ct => move(zoneMover, owner, targetId, ct));
        // G7-001: the card is leaving its current zone (bounce / return-to-deck / security / trash) — drop
        // the continuous/trigger bindings it auto-registered while in play. Critical for player-scope
        // effects (e.g. a Tamer's "your Digimon +1000 DP"), which CollectApplicable matches by owner only
        // and would otherwise keep applying after the source has left. No-op for cards that had none.
        // (design item R2-P2-3) this drop runs at STAGE time while the move is a flush thunk — a later
        // mutation staged in the SAME batch evaluates restrictions with this card's protections already gone.
        _effectRegistry?.RemoveWhere(binding => binding.Request.Context.SourceEntityId == targetId);
        _applied.Add(new AppliedMutation(mutation.Kind, targetId, "pendingMove"));
    }

    // (F1-Tier1 OnDiscard*) Trash a card to the trash while PRESERVING its source zone (so Hand->Trash /
    // Library->Trash derives OnDiscardHand / OnDiscardLibrary), stamping this flush's shared DISCARD batch id (so
    // an effect discarding N cards collapses to one reactor fire) and the CAUSE effect's source card id (the AS-IS
    // {"CardEffect", cardEffect} — a Hand/Security discard reactor requires an effect cause). Distinct from the
    // generic ApplyZoneMove/AddToTrashAsync (From=None) which cannot derive a source-zone discard timing.
    private void ApplyTrashCard(EffectMutation mutation, CardInstanceRecord record, HeadlessEntityId targetId)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        HeadlessPlayerId owner = record.OwnerId;
        long discardBatchId = ResolveDiscardBatchId();
        HeadlessEntityId cause = mutation.SourceEntityId;
        // (F1 reveal-remainder) a reveal-remainder trash carries the IsBeingRevealed mirror so the
        // OnDiscardLibrary gate rejects it (AS-IS !IsBeingRevealed, WhenDiscardLibrary.cs:23-26).
        bool isRevealTrash = ReadBool(mutation.Values, RevealTrashFlagKey);
        _pendingAsync.Add(ct => zoneMover.TrashCardAsync(owner, targetId, discardBatchId, cause.IsEmpty ? null : cause, isRevealTrash, ct));
        // (G7-001, same as ApplyZoneMove) drop the leaving card's auto-registered continuous/trigger bindings.
        _effectRegistry?.RemoveWhere(binding => binding.Request.Context.SourceEntityId == targetId);
        _applied.Add(new AppliedMutation(mutation.Kind, targetId, "pendingMove"));
    }

    private void ApplyDelete(EffectMutation mutation, CardInstanceRecord record, HeadlessEntityId targetId)
    {
        // Deletion-prevention: the static cannotBeDeleted flag (card/instance) OR a continuous
        // Delete/Prevent replacement (the same source BattleDeletionGate consults). When prevented, the
        // card stays on the field and the mutation is recorded as skipped with a deletionPrevented marker.
        if (ReadFlag(record.Metadata, CannotBeDeletedFlagKey) || IsDeletionPreventedByContinuous(targetId, mutation.SourceEntityId))
        {
            _skipped.Add(mutation);
            _applied.Add(new AppliedMutation(mutation.Kind, targetId, DeletionPreventedKey));
            return;
        }

        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        // (R2-P1-1) BATCH-ATOMIC defer. AS-IS DestroyPermanentsClass.Destroy() (CardController.cs:3684-3852)
        // processes the WHOLE target list through ONE sequence: willBeRemoveField on ALL → ONE PRE cut-in
        // (WhenPermanentWouldBeDeleted / WhenRemoveField) over the LIST → fix survivors → OnDestroyedAnyone /
        // OnLeaveFieldAnyone stacked ONCE for the fixed list → per-permanent trash. So when card A of a 2-card
        // delete carries a PRE option, card B is NOT trashed until A's cut-in decision resolved — B waits with A.
        // The former per-card defer decision split one Destroy() into two drains: B's [On Deletion] completed
        // BEFORE A's replacement decision, and the split drains double-fired anyone-scoped reactors
        // (call-local firedDeletion/firedLeaveBatch cannot dedup across drains). Mirror the battle path
        // (BattleResolver.ResolveRoundAsync: any loser needing a window parks EVERY loser; a single batch
        // finalize once all are decided): each Delete stages ONE thunk; the FIRST thunk to execute decides the
        // whole batch (all targets staged, none moved — the AS-IS all-marked-then-one-cut-in moment); a batch
        // with ANY PRE option parks EVERY member as pendingDeletion (the sweep's batch-mate gate finalizes them
        // together), else every member moves immediately.
        _currentDeleteBatch ??= new DeleteBatch();
        DeleteBatch batch = _currentDeleteBatch;
        var entry = new StagedDelete(mutation, targetId);
        batch.Entries.Add(entry);
        _pendingAsync.Add(ct => ExecuteStagedDeleteAsync(batch, entry, zoneMover, ct));
    }

    /// <summary>(R2-P1-1) One staged Delete's flush-time execution — the per-permanent half of the AS-IS
    /// <c>Destroy()</c> sequence, running after the batch-level defer decision.</summary>
    private async Task ExecuteStagedDeleteAsync(
        DeleteBatch batch, StagedDelete entry, IZoneMover zoneMover, CancellationToken ct)
    {
        EffectMutation mutation = entry.Mutation;
        HeadlessEntityId targetId = entry.TargetId;

        // (C-Del 3c-1 promote-to-defer) a prior thunk of this batch already PROMOTED it to a deferred deletion
        // (its PRE cut-in paused on an interactive replacement, flagging every member pendingDeletion). This
        // thunk's member is already parked; short-circuit so it does not re-open the PRE / OnDeletion window or
        // trash the (deferred) card — the GameFlowProcessor sweep finalizes the whole batch after the parked
        // window resolves.
        if (batch.PromotedToDefer)
        {
            _skipped.Add(mutation);
            return;
        }

        // Batch decision — once, at the FIRST delete thunk (every Delete of this flush is staged, none has
        // moved yet; earlier non-delete thunks have already run, matching the AS-IS sequencing where Destroy()
        // starts after the effect's prior steps completed).
        if (batch.DeferAll is null)
        {
            bool defer = false;
            if (_zoneMover is IZoneStateReader preZones)
            {
                foreach (StagedDelete staged in batch.Entries)
                {
                    if (!_repository.TryGetInstance(staged.TargetId, out CardInstanceRecord? candidate) || candidate is null)
                    {
                        continue;
                    }

                    // (C-Del 3c-2b) The 8 PRE replacement keywords (Evade/Barrier/ArmorPurge/Decoy/Scapegoat/
                    // Decode/Partition/Fragment) are RETIRED from the gate — they fire through the AS-IS PRE cut-in
                    // window opened below (batch.DeferAll=false path). Only the retained generic bridge
                    // (CustomWouldBeDeletedOption: a card-registered WhenPermanentWouldBeDeleted effect in the
                    // invented EffectRegistry, disjoint from the window's EffectList collection) still defers here.
                    if (DeletionReplacementTiming.HasPreOption(_repository, preZones, candidate, byBattle: false, _effectRegistry))
                    {
                        defer = true;
                        break;
                    }
                }
            }

            batch.DeferAll = defer;
        }

        // (C-Del 3b PRE cut-in transport) Open the AS-IS "would be deleted" PRE cut-in window
        // (WhenPermanentWouldBeDeleted → WhenRemoveField) on the universal effect-delete path — the same pair the
        // mirror faithful DestroyPermanentsClass.Destroy() opens (CardController.cs:3448-3485) but which only 2
        // cards reach directly. Opened ONLY on the NON-gate-deferred path (batch.DeferAll is false): every ported
        // PRE keyword (Evade/Barrier/ArmorPurge/Decoy/Scapegoat/Decode/Partition/Fragment) is gate-detected via
        // ContinuousKeywordGate → NewModelContinuousScan, which scans the SAME EffectList(WhenPermanentWouldBeDeleted)
        // this window collects, so it always forces DeferAll=true and never lands here → NO double-fire with the live
        // PRE replacement gate (whose firing half is NOT retired this batch). This branch is therefore a STRUCTURAL
        // NO-OP for every real card today (they are all gate-deferred, or carry no PRE effect); it activates only for
        // a gate-invisible window-form WhenPermanentWouldBeDeleted/WhenRemoveField ActivateClass (a Tfx witness, and —
        // once 3c retires the gate — the ported PRE keywords, which then fall into DeferAll=false and are collected
        // here). Reconciles the AS-IS willBeRemoveField model to the sink: all field-present members are marked
        // willBeRemoveField=true (batch-once), the cut-in drains, a replacement body cancels a deletion by clearing
        // its willBeRemoveField, and the per-entry survivor read below spares any such card (never trashing it) —
        // the sink's equivalent of AS-IS destroyTargetPermanents_Fixed. An INTERACTIVE cut-in effect (the drain
        // pauses on a ChoiceController request) is design item RD-3B-INTERACTIVE: the sink's inline-drain model
        // cannot resume mid-flush to finish the surrounding deletion, so the pause is re-thrown rather than swallowed
        // into a half-deleted batch; the promote-to-defer (pendingDeletion + GameFlowProcessor cut-in drain +
        // willBeRemoveField finalize) integration is deferred to 3c, which also owns the gate retirement.
        if (batch.DeferAll is false && !batch.PreWindowOpened && _context is not null)
        {
            using AmbientMatchContext.Scope _preScope = AmbientMatchContext.Enter(_context);
            batch.PreWindowOpened = true;
            var toDelete = new List<Permanent>();
            if (_zoneMover is IZoneStateReader preFieldZones)
            {
                foreach (StagedDelete staged in batch.Entries)
                {
                    if (_repository.TryGetInstance(staged.TargetId, out CardInstanceRecord? live) && live is not null
                        && (preFieldZones.GetCards(live.OwnerId, ChoiceZone.BattleArea).Contains(staged.TargetId)
                            || preFieldZones.GetCards(live.OwnerId, ChoiceZone.BreedingArea).Contains(staged.TargetId)))
                    {
                        var perm = new Permanent(_context, staged.TargetId, live.OwnerId)
                        {
                            willBeRemoveField = true,   // AS-IS Destroy() :3448 — mark ALL targets before the cut-in
                        };
                        toDelete.Add(perm);
                    }
                }
            }

            if (toDelete.Count > 0)
            {
                var cutIn = Assets.Scripts.Script.AutoProcessing.ForCutIn(_context);
                // AS-IS builds a FRESH WhenPermanentWouldRemoveFieldCheckHashtable per StackSkillInfos
                // (CardController.cs:3454-3469) — two builder calls. cardEffect=null (the sink threads only the
                // causing source id, RD-C1-CARDEFFECT-IDTHREAD); battle=null (the effect-delete path is never a
                // battle deletion — that is BattleResolver's IBattle path).
                // (design item RD-3C2B-02) IsByEffect-gated PRE keywords: AS-IS Destroy() threads the LIVE causing
                // cardEffect onto this pair (CardController.cs:3691-3705); with cardEffect=null here, a POSITIVE
                // IsByEffect gate (Decoy — "would be deleted by an opponent's effect") reads false and does NOT
                // collect via this sink window (it still fires via the faithful mirror DestroyPermanentsClass
                // path, which threads the live cardEffect). A boolean ByEffectCauseKey marker canNOT stand in:
                // IsByEffect's marker fallback ignores the per-card condition, which would break the NEGATED
                // owner-conditioned gate (Partition: !IsByEffect(IsOwnerEffect) — live-witnessed) — and payload
                // synthesis (a stand-in ICardEffect) is forbidden (the C-Btl IBattle precedent). Blocked on the
                // live-cardEffect thread (RD-C1-CARDEFFECT-IDTHREAD).
                await cutIn.StackSkillInfos(
                    CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(toDelete, cardEffect: null, battle: null),
                    EffectTiming.WhenPermanentWouldBeDeleted).ConfigureAwait(false);
                await cutIn.StackSkillInfos(
                    CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(toDelete, cardEffect: null, battle: null),
                    EffectTiming.WhenRemoveField).ConfigureAwait(false);

                if (cutIn.HasAwaitingActivateEffects())   // AS-IS Destroy() :3471 gate
                {
                    // AS-IS ShowDeleteEffect / ShrinkSecurityDigimonDisplay / HideDeleteEffect = UI (stripped).
                    try
                    {
                        await cutIn.TriggeredSkillProcess(false, null).ConfigureAwait(false);
                    }
                    catch (Exception preEx) when (preEx is WindowChoicePendingException or Runtime.DeferredChoicePendingException)
                    {
                        // (C-Del 3c-1 promote-to-defer — RD-3B-INTERACTIVE resolved) an INTERACTIVE would-be-deleted
                        // replacement (a "will you use Evade?" agent choice) paused the sink's inline cut-in drain.
                        // The sink cannot resume mid-flush to finish the surrounding deletion, so PROMOTE the batch
                        // to a deferred deletion: flag every field-present member pendingDeletion (leaving the parked
                        // ForCutIn window to carry the choice), then re-throw. FlushAsync SWALLOWS the pause (the
                        // enclosing pick's flush completes — no body re-run), so RunToStable pauses on the pending
                        // choice; once the agent resolves the parked window (MetadataActionProcessor resumes the
                        // ForCutIn pool), the resumed replacement body sets each survivor's willBeRemoveField=false
                        // and the GameFlowProcessor sweep finalizes the batch on willBeRemoveField (survivor spared /
                        // casualty trashed in ONE batch-unit OnDeletion window). Mirrors the AS-IS Destroy() coroutine
                        // suspending at TriggeredSkillProcess (:3471) then finalizing destroyTargetPermanents_Fixed.
                        PromoteBatchToPendingDeletion(batch);
                        throw;
                    }
                }
            }
        }

        // (C2 decision-4 deletion transport) The universal effect-deletion path is this sink (the mirror
        // DestroyPermanentsClass.Destroy() inline OnDeletion pair is reached by only 2 cards). Reproduce that pair
        // here: BEFORE any field->Trash move, COLLECT-BEFORE-REMOVAL the dead cards' OnDestroyedAnyone /
        // OnLeaveFieldAnyone reactors onto the mirror trigger stack, ONCE per delete-batch (AS-IS
        // CardController.cs:3736-3756 / mirror :3490/:3502). Immediate (non-deferred) batch = no member has a PRE
        // replacement option, so EVERY still-present entry dies -> the survivor set is the whole batch. The main
        // loop's AutoProcessCheck drains the stack afterwards (the SkillInfo heap holds the pre-removal card data,
        // so the reactors resolve even though the cards are then in the trash). Member derivation (decision #4):
        // cardEffect = null (the sink threads only the causing source id, RD-C1-CARDEFFECT-IDTHREAD); battle = null
        // (the effect-delete path never sets DeletedByBattleKey — that is BattleResolver's); isDPZero rides
        // mutation.Values (the DP-zero sweep stamps IsDpZeroKey on its cause). The sink SELF-ENTERS the ambient
        // match scope with its own context (nest-safe save/restore) so every flush path — RunToStable, an effect
        // body, or a direct-sink caller — opens the window against THIS match.
        if (batch.DeferAll is false && !batch.OnDeletionWindowOpened && _context is not null)
        {
            using AmbientMatchContext.Scope _deletionScope = AmbientMatchContext.Enter(_context);
            batch.OnDeletionWindowOpened = true;
            var deadPermanents = new List<Permanent>();
            bool anyDpZero = false;
            if (_zoneMover is IZoneStateReader preRemovalZones)
            {
                foreach (StagedDelete staged in batch.Entries)
                {
                    if (_repository.TryGetInstance(staged.TargetId, out CardInstanceRecord? dead) && dead is not null
                        && (preRemovalZones.GetCards(dead.OwnerId, ChoiceZone.BattleArea).Contains(staged.TargetId)
                            || preRemovalZones.GetCards(dead.OwnerId, ChoiceZone.BreedingArea).Contains(staged.TargetId)))
                    {
                        // (C-Del 3b) AS-IS builds the OnDeletion window over destroyTargetPermanents_Fixed —
                        // the PRE-cut-in SURVIVORS (willBeRemoveField cleared) are EXCLUDED (CardController.cs:3482-3496).
                        // When a PRE window opened, skip any member a replacement spared so its OnDestroyedAnyone /
                        // OnLeaveFieldAnyone reactor does NOT fire (it never left play).
                        if (batch.PreWindowOpened && !new Permanent(_context, staged.TargetId, dead.OwnerId).willBeRemoveField)
                        {
                            continue;
                        }

                        deadPermanents.Add(new Permanent(_context, staged.TargetId, dead.OwnerId));
                        anyDpZero |= ReadBool(staged.Mutation.Values, IsDpZeroKey);
                    }
                }
            }

            if (deadPermanents.Count > 0)
            {
                var deletionAutoProcessing = Assets.Scripts.Script.AutoProcessing.For(_context);
                // AS-IS builds a FRESH OnDeletionHashtable per StackSkillInfos (CardController.cs:3489-3509) —
                // two builder calls, not one shared instance.
                // (P1-1 C2r) Cause derivation for IsByBattle/IsByEffect: this sink is the effect-delete path, so
                // byBattle = false; byEffect = the batch is NOT the DP-zero sweep (a DP-zero delete carries no
                // CardEffect in AS-IS, so IsByEffect must stay false while DPZero=true). A non-DPZero sink batch
                // always carries a causing effect id, so byEffect = !anyDpZero exactly matches AS-IS.
                await deletionAutoProcessing.StackSkillInfos(
                    CardEffectCommons.OnDeletionHashtable(deadPermanents, byEffectCause: !anyDpZero, byBattleCause: false, anyDpZero),
                    EffectTiming.OnDestroyedAnyone).ConfigureAwait(false);
                await deletionAutoProcessing.StackSkillInfos(
                    CardEffectCommons.OnDeletionHashtable(deadPermanents, byEffectCause: !anyDpZero, byBattleCause: false, anyDpZero),
                    EffectTiming.OnLeaveFieldAnyone).ConfigureAwait(false);
            }
        }

        if (!_repository.TryGetInstance(targetId, out CardInstanceRecord? record) || record is null)
        {
            _skipped.Add(mutation);
            return;
        }

        if (batch.DeferAll is true)
        {
            // DEFER the whole batch member (flag pendingDeletion) so the common loop surfaces the replacement
            // window for the option holders; the state-based sweep finishes the batch together once every
            // member's decision settled (GameFlowProcessor batch-mate gate). (C-Del 3c-2b) Only the retained
            // CustomWouldBeDeleted bridge reaches this branch now — the Decoy eligibility marker is retired.
            var deferMetadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
            {
                [GameFlowProcessor.PendingDeletionKey] = true,
                [DeletedByEffectKey] = true,
                [Runtime.DeletionReplacementGate.DeletedByOwnEffectKey] = IsOwnEffect(mutation.SourceEntityId, record.OwnerId),
                [DeletedBySourceEntityIdKey] = mutation.SourceEntityId.Value,
                // (D-1 / VR-8) remember THIS effect resolution's batch id so the deferred-finalize move
                // (GameFlowProcessor) re-stamps it — a decline finishes the ORIGINATING Destroy()'s batch,
                // and the sweep's batch-mate gate groups co-deferred members by this id.
                [DeletionBatchIdKey] = ResolveDeletionBatchId(),
            };
            if (ReadBool(mutation.Values, IsDpZeroKey))
            {
                deferMetadata[IsDpZeroKey] = true;   // (B3) AS-IS DPZero flag travels with the deletion
            }

            _repository.Upsert(record with { Metadata = deferMetadata });
            _skipped.Add(mutation);
            _applied.Add(new AppliedMutation(mutation.Kind, targetId, GameFlowProcessor.PendingDeletionKey));
            return;
        }

        // (Fragment / Scapegoat / Decoy auto-resolve removed — all are F-6.8 agent choices via the window.)

        // (C-Del 3b PRE transport) AS-IS Destroy() fixes survivors AFTER the PRE cut-in — a permanent whose
        // willBeRemoveField a replacement CLEARED is filtered OUT of destroyTargetPermanents_Fixed
        // (CardController.cs:3482-3485) and never trashed. Reconciled to the sink: read this member's
        // willBeRemoveField (set true for the whole batch above; cleared only by a would-be-deleted replacement
        // body during the drain) and SPARE it if a replacement cancelled its deletion. The flag is a transient of
        // this synchronous flush (AS-IS resets it at :3585-3593); reset it here for a spared survivor so no stale
        // marker persists. Reached only on the non-deferred path (PreWindowOpened ⇒ batch.DeferAll is false).
        if (batch.PreWindowOpened && _context is not null)
        {
            var fixedPerm = new Permanent(_context, targetId, record.OwnerId);
            if (!fixedPerm.willBeRemoveField)
            {
                _skipped.Add(mutation);
                _applied.Add(new AppliedMutation(mutation.Kind, targetId, "survivedWouldBeDeletedWindow"));
                return;
            }

            fixedPerm.willBeRemoveField = false;   // AS-IS Destroy() :3585-3593 reset (card is about to be trashed)
        }

        // Stamp the deletion marker before the move so OnDeletion-scoped triggers can read it.
        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
        {
            [DeletedByEffectKey] = true,
            [Runtime.DeletionReplacementGate.DeletedByOwnEffectKey] = IsOwnEffect(mutation.SourceEntityId, record.OwnerId),
            [DeletedBySourceEntityIdKey] = mutation.SourceEntityId.Value,
        };
        if (ReadBool(mutation.Values, IsDpZeroKey))
        {
            metadata[IsDpZeroKey] = true;   // (B3) AS-IS DPZero flag on the on-deletion hashtable
        }

        // (A4/P1) the card's own keyword grants are dropped below (it left play), but its POST deletion
        // responses are judged AT deletion time (AS-IS reads the dead card's effects during its own
        // deletion processing). Snapshot the live keyword state — and Partition's stored condition list —
        // into the per-instance flags the POST window / Fortitude replay read. (R2-P1-3) the snapshot now
        // also records the AS-IS "record parameters just before deletion" block (CardController.cs:3762-3783).
        if (_effectRegistry is not null)
        {
            Runtime.CardLeavePlayCleanup.SnapshotPostReplacementKeywords(_effectRegistry, _context, targetId, metadata, _repository);
        }

        _repository.Upsert(record with { Metadata = metadata });

        HeadlessPlayerId owner = record.OwnerId;
        // G6-001: the card left play — drop the continuous/trigger bindings it had auto-registered. (B.O.5-tail)
        // EXCEPT a self-[On Deletion] grant marked SurviveOwnLeave, which must fire ON this deletion; it is
        // removed after it resolves (DelayedOneShot) or at its duration boundary.
        _effectRegistry?.RemoveWhere(binding => binding.Request.Context.SourceEntityId == targetId
            && !ReadBool(binding.Request.Context.Values, AutoProcessingTriggerCollector.SurviveOwnLeaveKey));

        // (RD-4) AS-IS Permanent.DiscardEvoRoots (CardController.cs:3846) trashes the deleted permanent's
        // digivolution sources BEFORE the top card (:3852) — a direct trash-add, so NO OnDigivolutionCardDiscarded
        // trigger fires (gameEventQueue omitted). Unconditional like AS-IS. (This IMMEDIATE path runs only when
        // the deletion was NOT deferred; Decode/Partition (design item C-1) now defer into the PRE window above and
        // play/detach their source(s) before the deferred finalize trashes the remainder.) See DeletionSourceTrash.
        // (design item C-2) the sources/link cards ride the same DiscardEvoRoots overflow pass as the top card: an
        // un-flipped ACE source leaving costs its owner memory. Same memory sink + turn player as the top-card
        // ApplyAceOverflowOnLeave below; ignoreOverflow follows the top card's move (AS-IS DiscardEvoRoots takes
        // one ignoreOverflow governing both).
        bool ignoreSourceOverflow = ReadBool(mutation.Values, AceOverflowGate.IgnoreOverflowKey);
        await Runtime.DeletionSourceTrash.TrashEvoSourcesAsync(
            _repository, zoneMover, targetId, gameEventQueue: null, ct, _memory, _currentTurnPlayer?.Invoke(), ignoreSourceOverflow)
            .ConfigureAwait(false);

        // (PRIM-W4 AceOverflow + R2-P2-1) an un-flipped ACE leaving the field (here: deleted to trash) costs its
        // owner the printed Overflow memory — AFTER the sources' overflow, mirroring the AS-IS order
        // DiscardEvoRoots (source+link overflow, Permanent.cs:113-114) → RemoveField (top overflow,
        // CardObjectController.cs:528). Formerly charged at staging (top-first), inverting the AS-IS
        // source→top observation order; the sums matched but mid-sequence memory reads did not.
        ApplyAceOverflowOnLeave(record, targetId, mutation);

        // (C5-witness) move with the REAL from-zone so the CardMoved event derives OnDeletion/OnLeaveField —
        // AS-IS DestroyPermanentsClass.Destroy always opens the OnDestroyedAnyone window for an effect
        // deletion. The previous AddToTrashAsync recorded FromZone=None ("Insert"), so an IMMEDIATE (non-
        // deferred) effect deletion never fired the dead card's [On Deletion]; the deferred-finalize path
        // (GameFlowProcessor.RuleProcessAsync) and the battle path already move field->trash.
        // (D-1 / VR-8) the CardMoved event carries THIS effect resolution's batch id (shared by every card this
        // sink deletes: N-card single delete-process == one batch == one reactor fire) for the window collapse.
        long deletionBatchId = ResolveDeletionBatchId();
        // (RD-S2-LM018-01) BUGFIX: the bare FirstOrDefault(predicate) overload falls back to default(ChoiceZone)
        // when no zone matches — and ChoiceZone.Library is the enum's first (value-0) member, NOT
        // ChoiceZone.None (declared last) — so an already-relocated target (e.g. AS-IS's own double-delete
        // idiom: a SelectPermanentEffect Mode.Destroy batch whose per-target selectPermanentCoroutine ALSO
        // independently deletes the same permanent, as LM_018 does verbatim) silently produced a bogus
        // MoveAsync(from: Library) instead of falling through to the intended AddToTrashAsync no-op path,
        // throwing "Card id '...' is not in player zone 'Library'." Supply the explicit fallback value.
        ChoiceZone from = zoneMover is IZoneStateReader deadZones
            ? new[] { ChoiceZone.BattleArea, ChoiceZone.BreedingArea }
                .FirstOrDefault(zone => deadZones.GetCards(owner, zone).Contains(targetId), ChoiceZone.None)
            : ChoiceZone.None;
        if (from == ChoiceZone.None)
        {
            await zoneMover.AddToTrashAsync(owner, targetId, ct).ConfigureAwait(false);
        }
        else
        {
            await zoneMover.MoveAsync(new ZoneMoveRequest(owner, targetId, from, ChoiceZone.Trash,
                Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionBatchIdKey] = deletionBatchId }), ct)
                .ConfigureAwait(false);
        }

        // (C-Del 3a RETIRED) C-6 Fortitude no longer replays via the invented gate here — it fires through the
        // AS-IS OnDestroyedAnyone cut-in window opened above (StackSkillInfos, collect-before-removal), resolved
        // by the post-deletion AutoProcessCheck, from the card's printed FortitudeEffect / granted GainFortitude
        // bucket. Auto-replaying here as well would double-fire.
        // C-21 Armor Purge is now an OPTIONAL post-deletion agent choice (F-6.8 DeletionReplacementTiming),
        // opened by the common loop once the top is in the trash — no longer auto-applied here.
        // C-17 Ascension / C-22 Save are now OPTIONAL post-deletion agent choices (F-6.8
        // DeletionReplacementTiming), opened by the common loop once the card is in the trash — no longer
        // auto-applied here.
        _applied.Add(new AppliedMutation(mutation.Kind, targetId, DeletedByEffectKey));
    }

    /// <summary>(C-Del 3c-1 promote-to-defer) An INTERACTIVE PRE cut-in replacement paused the sink's inline
    /// drain. Flag EVERY field-present member of the batch <c>pendingDeletion</c> — the SAME deferral the
    /// <c>batch.DeferAll is true</c> branch writes, plus <see cref="PreWindowPromotedKey"/> so the sweep finalizes
    /// on <c>willBeRemoveField</c> rather than a gate decline. All members currently carry
    /// <c>willBeRemoveField=true</c> (the PRE window marked them; the interactive replacement has not resolved yet),
    /// so none is spared here — the resumed window body clears the survivors' flag and the sweep spares them.
    /// Per-entry cause metadata (source/DPZero/Decoy) mirrors the DeferAll branch. Sets <see cref="DeleteBatch.PromotedToDefer"/>
    /// so later thunks of the flush short-circuit and <see cref="FlushAsync"/> swallows the pause.</summary>
    private void PromoteBatchToPendingDeletion(DeleteBatch batch)
    {
        batch.PromotedToDefer = true;
        if (_zoneMover is not IZoneStateReader zones)
        {
            return;
        }

        foreach (StagedDelete staged in batch.Entries)
        {
            if (!_repository.TryGetInstance(staged.TargetId, out CardInstanceRecord? record) || record is null ||
                !(zones.GetCards(record.OwnerId, ChoiceZone.BattleArea).Contains(staged.TargetId)
                    || zones.GetCards(record.OwnerId, ChoiceZone.BreedingArea).Contains(staged.TargetId)))
            {
                continue;   // already left the field (a co-batch member trashed earlier) — nothing to defer
            }

            // (C-Del 3c-2b) The Decoy eligibility marker is retired — Scapegoat/Decoy fire through the AS-IS window.
            var deferMetadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
            {
                [GameFlowProcessor.PendingDeletionKey] = true,
                [PreWindowPromotedKey] = true,
                [DeletedByEffectKey] = true,
                [Runtime.DeletionReplacementGate.DeletedByOwnEffectKey] = IsOwnEffect(staged.Mutation.SourceEntityId, record.OwnerId),
                [DeletedBySourceEntityIdKey] = staged.Mutation.SourceEntityId.Value,
                // (D-1 / VR-8) stash THIS Destroy()'s batch id so the deferred-finalize move re-stamps it and the
                // sweep's batch-mate gate groups the co-promoted members into ONE batch-unit OnDeletion window.
                [DeletionBatchIdKey] = ResolveDeletionBatchId(),
            };
            if (ReadBool(staged.Mutation.Values, IsDpZeroKey))
            {
                deferMetadata[IsDpZeroKey] = true;
            }

            _repository.Upsert(record with { Metadata = deferMetadata });
        }
    }

    // (S6) Whether the causing effect belongs to the deleted card's own controller (source owner == card owner).
    private bool IsOwnEffect(HeadlessEntityId sourceId, HeadlessPlayerId cardOwner) =>
        !sourceId.IsEmpty && _repository.TryGetInstance(sourceId, out CardInstanceRecord? src) && src is not null && src.OwnerId == cardOwner;

    // (A4/P1) the deletion-time keyword snapshot lives in Runtime.CardLeavePlayCleanup — shared with the
    // battle-deletion and pending-sweep departure paths.

    // (FR-P3) The continuous effects applicable to a card: player-scope + arbitrary predicate aware when an
    // EngineContext is wired (so "your <X> Digimon cannot be ..." reaches the matching set), else registry-only
    // card-targeted (self) fallback.
    private IReadOnlyList<EffectRequest> ScopedEffects(HeadlessEntityId cardId)
    {
        if (_context is not null)
        {
            return ContinuousScopeEvaluation.ApplicableEffects(_context, ContinuousRestrictionGate.Scope, cardId);
        }

        return _effectRegistry is null
            ? Array.Empty<EffectRequest>()
            : _effectRegistry.GetContinuousEffects(new EffectQueryContext(ContinuousRestrictionGate.Scope, targetEntityId: cardId)).ToArray();
    }

    private bool HasValueFlag(HeadlessEntityId cardId, string flagKey)
    {
        foreach (EffectRequest effect in ScopedEffects(cardId))
        {
            if (effect.Context.Values.TryGetValue(flagKey, out object? raw) && raw is bool flag && flag)
            {
                return true;
            }
        }

        return false;
    }

    // (FR2/M-2) Is <paramref name="cardId"/> blocked (by a restriction carrying <paramref name="restrictionKey"/>)
    // FROM the effect currently causing this mutation? An unconditional restriction always blocks; one carrying a
    // causing-effect predicate (AS-IS cardEffectCondition, e.g. IsOpponentEffect) blocks only when the causing
    // effect's SOURCE card matches — so "cannot be returned/trashed by the OPPONENT's effects" still allows your
    // own. When the causing source is unknown, a narrowed restriction does not block (it cannot be confirmed).
    /// <summary>(PRIM-P0 B.O.6) Whether <paramref name="player"/> has an active player-scope restriction under
    /// <paramref name="restrictionKey"/> (AS-IS Player.Can... gate). Unlike <see cref="IsRestrictedFromCause"/>
    /// this is not tied to a card — it consults every player-scope continuous restriction binding.</summary>
    private bool IsPlayerRestricted(HeadlessPlayerId player, string restrictionKey, HeadlessEntityId causingSourceId)
    {
        // (R3-W3c-4c B3 flip) AS-IS-literal LIVE scan for the CannotAddSecurity/CannotAddMemory restriction — the
        // gaining <paramref name="player"/>'s add is blocked when a usable ICannotAdd{Security,Memory}Effect on any
        // Players_ForTurnPlayer field permanent's or player's EffectList(None) says so (AS-IS Player.CanAddSecurity
        // :1477-1513 / CanAddMemory :1037-1071 — the RESTRICTION portion only; the AddMemory gauge cap and the
        // IsSecurityLooking guard belong to their own gates, not this per-mutation restriction check). Was the
        // registry player-scope binding scan; the factory now produces the kind-classes (no ToBinding), so the
        // live scan is the sole reader. The causing effect is reconstructed from <paramref name="causingSourceId"/>
        // (the CardEffectCondition reads only its source card).
        if (_context is null || player.IsEmpty)
        {
            return false;
        }

        var gainingPlayer = new Assets.Scripts.Script.CardEffectCommons.Player(_context, player);
        Assets.Scripts.Script.CardEffectCommons.ICardEffect cause =
            Assets.Scripts.Script.CardEffectCommons.BareCauseEffect.For(_context, causingSourceId);

        bool IsSecurity = restrictionKey == Assets.Scripts.Script.CardEffectCommons.RestrictionHelpers.CannotAddSecurityKey;
        foreach (Assets.Scripts.Script.CardEffectCommons.Player scanPlayer in new Assets.Scripts.Script.CardEffectCommons.GameContext(_context).Players_ForTurnPlayer)
        {
            foreach (Assets.Scripts.Script.CardEffectCommons.Permanent permanent in scanPlayer.GetFieldPermanents())
            {
                if (ScanEffects(permanent.EffectList(Assets.Scripts.Script.CardEffectCommons.EffectTiming.None)))
                {
                    return true;
                }
            }

            if (ScanEffects(scanPlayer.EffectList(Assets.Scripts.Script.CardEffectCommons.EffectTiming.None)))
            {
                return true;
            }
        }

        return false;

        bool ScanEffects(List<Assets.Scripts.Script.CardEffectCommons.ICardEffect> effects)
        {
            // AS-IS order (Player.CanAddSecurity/CanAddMemory): interface check FIRST, then CanUse(null), then the
            // predicate — so CanUse (which touches GManager) is only reached for a matching restriction effect.
            foreach (Assets.Scripts.Script.CardEffectCommons.ICardEffect cardEffect in effects)
            {
                if (IsSecurity)
                {
                    if (cardEffect is Assets.Scripts.Script.CardEffectCommons.ICannotAddSecurityEffect s
                        && cardEffect.CanUse(null) && s.cannotAddSecurity(gainingPlayer, cause))
                    {
                        return true;
                    }
                }
                else if (cardEffect is Assets.Scripts.Script.CardEffectCommons.ICannotAddMemoryEffect m
                    && cardEffect.CanUse(null) && m.cannotAddMemory(gainingPlayer, cause))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>(d-remediation, true-scan) AS-IS <c>Permanent.CanNotBeRemoved</c>: SCAN every field permanent's
    /// effects and, for each usable <c>CanNotBeRemoved</c> effect, evaluate its predicate against the candidate.
    /// Any match ⇒ the candidate cannot leave the battle area (bounce / deck-bounce) — deletion is exempt (the
    /// caller only consults this on the return chokepoints).</summary>
    private bool IsRemovalBlockedByScan(HeadlessEntityId candidateId)
    {
        // (joint-migration) canonical scan (AS-IS Permanent.CanNotBeRemoved): single-participant restriction, no cause.
        return _context is not null
            && Runtime.RestrictionScan.IsRestricted(
                _context, Assets.Scripts.Script.CardEffectCommons.RestrictionHelpers.CannotBeRemovedKey, candidateId, default);
    }

    private bool IsRestrictedFromCause(HeadlessEntityId cardId, string restrictionKey, HeadlessEntityId causingSourceId)
    {
        // (joint-migration) canonical scan: mirror AS-IS Permanent.Can<Return/Delete> — SCAN every field effect and
        // evaluate the joint restriction predicate f(subject = this card, counterpart = the CAUSING effect's source).
        // The producers embed the AS-IS cardEffectCondition as the counterpart gate, so a causing predicate that the
        // source fails (or an empty source when the restriction is conditional) does not restrict — matching the old
        // per-key CausingEffectPredicate branch below.
        if (_context is not null)
        {
            // (RD-P6B-12 resolved, P7 FAILa-04/G9-053 fix) UNION the new-model cause-conditional interface scans
            // (AS-IS Permanent.CanBeDestroyedBySkill:3309 / CannotReturnToHand:744 / CannotReturnToLibrary:785) — a
            // ported CanNotBeDestroyedBySkillStaticEffect / CannotReturnToHandStaticEffect / CannotReturnToDeckStaticEffect
            // registers no legacy binding, so RestrictionScan.IsRestricted (registry-backed) alone cannot see it.
            return Runtime.RestrictionScan.IsRestricted(_context, restrictionKey, cardId, causingSourceId)
                || Assets.Scripts.Script.CardEffectCommons.NewModelContinuousScan.IsRestrictedByCauseNewModel(
                    _context, restrictionKey, cardId, causingSourceId);
        }

        // Registry-only fallback (no EngineContext): only UNCONDITIONAL restrictions can be evaluated — a conditional
        // (causing-predicate) restriction needs a CardSource for the source, which requires the context.
        foreach (EffectRequest effect in ScopedEffects(cardId))
        {
            IReadOnlyDictionary<string, object?> values = effect.Context.Values;
            if (!(values.TryGetValue(restrictionKey, out object? raw) && raw is bool flag && flag))
            {
                continue;
            }

            if (!values.ContainsKey(Assets.Scripts.Script.CardEffectCommons.RestrictionHelpers.CausingEffectPredicateKey))
            {
                return true;
            }
        }

        return false;
    }

    // (FR-P3) The PARSED continuous restriction/replacement result for a card — player-scope + arbitrary
    // predicate aware when an EngineContext is wired, else the exact registry-only (self) reading. Preserves
    // the full replacement parsing (all Delete/Prevent sources, not just one flag) the sink relied on before.
    private ContinuousEvaluationResult ScopedResult(HeadlessEntityId cardId)
    {
        if (_context is not null)
        {
            return ContinuousScopeEvaluation.EvaluateForCard(_context, ContinuousRestrictionGate.Scope, cardId);
        }

        var queryContext = new EffectQueryContext(ContinuousRestrictionGate.Scope, targetEntityId: cardId);
        return _effectRegistry is null
            ? new ContinuousEvaluationResult(queryContext, Array.Empty<EffectRequest>(), Array.Empty<NumericModifier>(), Array.Empty<CannotRestriction>(), Array.Empty<ReplacementEffect>(), new Dictionary<string, object?>(StringComparer.Ordinal))
            : ContinuousEffectEvaluator.Evaluate(_effectRegistry, queryContext);
    }

    // (PRIM-W4/FR-P3) restriction probe honouring self AND player-scope-with-predicate. Used by the suspend /
    // return sink paths (CantSuspend / CannotReturnToHand / CannotReturnToDeck).
    private bool HasSelfRestriction(HeadlessEntityId cardId, CannotRestrictionKind kind)
    {
        foreach (CannotRestriction restriction in ScopedResult(cardId).Restrictions)
        {
            if (restriction.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    // (PRIM-W4 AceOverflow) when an un-flipped ACE Digimon leaves the field (battle / breeding area), its
    // owner loses memory equal to its printed Overflow value. Called at the field-leave mutations; the
    // on-field check makes a card moved from deck/hand (never on field) a no-op.
    private void ApplyAceOverflowOnLeave(CardInstanceRecord record, HeadlessEntityId cardId, EffectMutation mutation)
    {
        if (_memory is null || ReadBool(mutation.Values, AceOverflowGate.IgnoreOverflowKey))
        {
            return;
        }

        int overflow = AceOverflowGate.OverflowFor(record);
        if (overflow <= 0 || _zoneMover is not IZoneStateReader reader)
        {
            return;
        }

        bool onField = reader.GetCards(record.OwnerId, ChoiceZone.BattleArea).Contains(cardId)
            || reader.GetCards(record.OwnerId, ChoiceZone.BreedingArea).Contains(cardId);
        if (!onField)
        {
            return;
        }

        int delta = AceOverflowGate.MemoryDelta(overflow, record.OwnerId, _currentTurnPlayer?.Invoke());
        _memory.Add(delta);
        _applied.Add(new AppliedMutation(mutation.Kind, cardId, "aceOverflow"));
    }

    // (PRIM-W4/FR-P3) "does an applicable (self OR player-scope-with-predicate) continuous effect carry
    // <paramref name="flagKey"/>=true".
    private bool HasSelfFlag(HeadlessEntityId cardId, string flagKey) => HasValueFlag(cardId, flagKey);

    private bool IsDeletionPreventedByContinuous(HeadlessEntityId cardId, HeadlessEntityId causingSourceId)
    {
        // (FR-P3) honour self AND player-scope-with-predicate, via the fully PARSED result (every Delete/Prevent
        // replacement source, not just one flag). ApplyDelete is the effect-sourced delete path (battle deletion
        // runs through BattleDeletionGate).
        ContinuousEvaluationResult result = ScopedResult(cardId);
        foreach (ReplacementEffect replacement in result.Replacements)
        {
            // General CanNotBeDestroyed (Delete/Prevent replacement) = unconditional immunity, no causing predicate.
            if (replacement.EventKind == ReplacementEventKind.Delete && replacement.ActionKind == ReplacementActionKind.Prevent)
            {
                return true;
            }
        }

        // (R2-D) the new-model general "cannot be destroyed" scan is now read through its AS-IS home — the mirror
        // Permanent.CanBeDestroyed() getter (AS-IS Permanent.CanBeDestroyed, the ICanNotBeDestroyedEffect scan over
        // all players' field permanents + players), replacing the former NewModelContinuousScan.HasCanNotBeDestroyed
        // helper (byte-identical scan). Same two-arm UNION BattleDeletionGate.PreventsBattleDeletion performs — a
        // ported CanNotBeDestroyedStaticEffect registers no legacy replacement so the ScopedResult arm above alone
        // cannot see it. NOTE: battle-only immunity (ICanNotBeDestroyedByBattleEffect) is battle-path-only
        // (BattleDeletionGate) and intentionally NOT consulted here (this is the effect-delete path). The
        // cause-conditional CanBeDestroyedBySkill(effect) is NOT rewireable here — the sink holds only the causing
        // source's entity id, not the live ICardEffect the getter needs (STOP RD-R2-02); it stays on the shared
        // RestrictionScan || IsRestrictedByCauseNewModel seam below.
        if (_context is not null && !new Assets.Scripts.Script.CardEffectCommons.Permanent(_context, cardId).CanBeDestroyed())
        {
            return true;
        }

        // (fidelity) CanNotBeDestroyedBySkill (effect-delete-only) honours its CAUSING-effect predicate — a card
        // immune to "opponent's effects" is NOT immune to its own controller's effect (AS-IS
        // CanNotBeDestroyedBySkill(permanent, cardEffect)). Mirror the CannotReturnToHand/Deck path exactly:
        // no predicate → unconditional; predicate present → immune only when the deleting effect's source matches.
        return IsRestrictedFromCause(
            cardId, Assets.Scripts.Script.CardEffectCommons.RestrictionHelpers.CannotBeDeletedBySkillKey, causingSourceId);
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        return metadata.TryGetValue(key, out object? raw) && raw is bool value && value;
    }

    private void ApplyDpModifier(EffectMutation mutation, CardInstanceRecord record, HeadlessEntityId targetId)
    {
        int value = ReadInt(mutation.Values, DpValueKey) ?? 0;
        bool absolute = ReadBool(mutation.Values, DpAbsoluteKey);
        long order = ReadLong(mutation.Values, DpActivatedOrderKey) ?? 0;
        string source = mutation.SourceEntityId.Value;

        DpModifier modifier = absolute
            ? DpModifier.Absolute(value, order, source)
            : DpModifier.Relative(value, order, source);

        DpModifier[] existing = record.Metadata.TryGetValue(DpModifiersKey, out object? raw) &&
            raw is IEnumerable<DpModifier> mods
            ? mods.ToArray()
            : Array.Empty<DpModifier>();

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
        {
            [DpModifiersKey] = existing.Append(modifier).ToArray(),
        };
        _repository.Upsert(record with { Metadata = metadata });
        _applied.Add(new AppliedMutation(mutation.Kind, targetId, DpModifiersKey));
    }

    private void ApplyNamedFlag(EffectMutation mutation, CardInstanceRecord record, HeadlessEntityId targetId, bool value)
    {
        string? key = ReadString(mutation.Values, FlagKeyKey);
        if (string.IsNullOrWhiteSpace(key))
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' is missing a '{FlagKeyKey}' value.");
            return;
        }

        WriteMetadata(record, targetId, mutation.Kind, key.Trim(), value);
    }

    private void WriteMetadata(CardInstanceRecord record, HeadlessEntityId targetId, string kind, string key, object? value)
    {
        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
        {
            [key] = value,
        };
        _repository.Upsert(record with { Metadata = metadata });
        _applied.Add(new AppliedMutation(kind, targetId, key));
    }

    /// <summary>(F-3.7) Effect-driven play: move the card from its source zone (default Hand) onto the
    /// battle area face up and mark it entered-this-turn (summoning sickness). The actual move is
    /// deferred to the flush, like the other zone-move kinds.</summary>
    private void ApplyPlayCard(EffectMutation mutation, CardInstanceRecord record, HeadlessEntityId targetId)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        ChoiceZone fromZone = ReadZone(mutation.Values, FromZoneKey, ChoiceZone.Hand);
        bool faceUp = !mutation.Values.ContainsKey(FaceUpKey) || ReadBool(mutation.Values, FaceUpKey);
        HeadlessPlayerId owner = record.OwnerId;

        // D-8: pay the (already cost-pipeline-resolved) memory cost for a "play for cost" effect. 0 / no
        // memory controller = play for free.
        int memoryCost = ReadInt(mutation.Values, MemoryCostKey) ?? 0;
        if (memoryCost > 0 && _memory is not null)
        {
            _memory.Pay(memoryCost);
        }

        // Mark summoning sickness synchronously (same metadata flag PlayCardAction sets).
        WriteMetadata(record, targetId, mutation.Kind, EnteredThisTurnKey, true);
        // (G3) an ETB-suppressed play threads a one-shot suppressOnPlay marker onto the CardMoved event so the
        // moved card's OWN OnPlay/OnEnterField triggers are dropped (other cards' reactions unaffected).
        IReadOnlyDictionary<string, object?>? moveMetadata = ReadBool(mutation.Values, SuppressOnPlayKey)
            ? new Dictionary<string, object?>(StringComparer.Ordinal) { [SuppressOnPlayKey] = true }
            : null;
        // (C-Del 3c-2b-pre) AS-IS PlayPermanentClass.PlayPermanent (CardController.cs:1361-1363) runs
        // CardObjectController.RemoveFromAllArea BEFORE placing the played card on the field: it detaches the
        // card from any host permanent's digivolution stack (RemoveFromAllArea:392-402 -> Permanent.RemoveCardSource)
        // AND withdraws it from whatever physical zone it sits in, then CreateNewPermanent places it in the battle
        // area. A digivolution source physically sits OFF-FIELD (ChoiceZone.None), tracked ONLY by the host's
        // sourceIds metadata — it is NOT in a DigivolutionCards zone-list — so its physical transport is
        // None -> BattleArea (a DigivolutionCards from-zone would throw on the empty zone) and the "removal from
        // DigivolutionCards" is the sourceIds detach. Without the detach a following deletion finalize
        // (DeletionSourceTrash.TrashEvoSourcesAsync) reads the dead host's stale sourceIds and re-trashes the
        // just-played source (the retiring DeletionReplacementGate.PlaySourceForFreeAsync did exactly this
        // None->BattleArea move + detach). A source is embedded ⇔ the play root is DigivolutionCards.
        bool fromDigivolutionStack = fromZone == ChoiceZone.DigivolutionCards;
        ChoiceZone physicalFromZone = fromDigivolutionStack ? ChoiceZone.None : fromZone;
        _pendingAsync.Add(ct => zoneMover.MoveAsync(
            new ZoneMoveRequest(owner, targetId, physicalFromZone, ChoiceZone.BattleArea, faceUp, moveMetadata), ct));
        if (fromDigivolutionStack)
        {
            _pendingAsync.Add(ct => Runtime.DigivolutionStackHelpers.DetachSourceFromHostAsync(_repository, zoneMover, targetId, ct));
        }
        // (G8-002) The effect played a card onto the field — auto-register its ported effects (no-op for
        // un-ported cards). Binding registration is zone-independent, so it is safe before the deferred move.
        _onCardEnteredPlay?.Invoke(targetId, owner);
    }

    /// <summary>(G10-007) Play a specific digivolution source out from under its host as a new battle-area
    /// Digimon (cost-free) and auto-register its effects.</summary>
    private void ApplyPlayDigivolutionAsDigimon(EffectMutation mutation, CardInstanceRecord record, HeadlessEntityId underCardId)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        if (!(mutation.Values.TryGetValue(HostEntityIdKey, out object? raw) && raw is string hostValue) || string.IsNullOrWhiteSpace(hostValue))
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' is missing a '{HostEntityIdKey}' value.");
            return;
        }

        var hostId = new HeadlessEntityId(hostValue);
        HeadlessPlayerId owner = record.OwnerId;
        _pendingAsync.Add(ct => DigivolutionStackHelpers.PlaySpecificSourceAsync(_repository, zoneMover, hostId, underCardId, ChoiceZone.BattleArea, ct));
        // The under-card is now a fresh battle-area Digimon — auto-register its effects (G6-001 / G8-002).
        _onCardEnteredPlay?.Invoke(underCardId, owner);
        _applied.Add(new AppliedMutation(mutation.Kind, underCardId, "playFromUnder"));
    }

    /// <summary>(B-10) Trash (returnToZone null) or return the host's digivolution sources. Deferred to
    /// flush like the other zone moves.</summary>
    private void ApplyDigivolutionSourceRemoval(EffectMutation mutation, HeadlessEntityId hostId, ChoiceZone? returnToZone)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        int count = ReadInt(mutation.Values, CountKey) ?? 1;
        bool fromBottom = !mutation.Values.ContainsKey(FromBottomKey) || ReadBool(mutation.Values, FromBottomKey);
        if (returnToZone is ChoiceZone destination)
        {
            // (c-remediation) AS-IS ReturnToLibraryBottomDigivolutionCards checks `TopCard.CanNotBeAffected(CardEffect)`
            // (CardController.cs:5268) before returning — a host immune to the causing effect keeps its sources.
            // Mirror it (general effect immunity), distinct from the trash-only ImmuneStackTrashing gate below.
            // (B군 P0-1) Rehomed from the now-dead ContinuousImmunityGate.BlocksOpponentEffect registry scan to the
            // AS-IS-literal live getter TopCard.CanNotBeAffected (cause = the causing effect collapsed to its source
            // card). Context-less sink => no live immunity (nothing produced the flag after the flip).
            if (_context is { } returnImmuneCtx
                && new Assets.Scripts.Script.CardEffectCommons.Permanent(returnImmuneCtx, hostId)
                    .TopCard.CanNotBeAffected(Assets.Scripts.Script.CardEffectCommons.BareCauseEffect.For(returnImmuneCtx, mutation.SourceEntityId)))
            {
                _skipped.Add(mutation);
                _applied.Add(new AppliedMutation(mutation.Kind, hostId, "restricted"));
                return;
            }

            _pendingAsync.Add(ct => DigivolutionStackHelpers.ReturnSourcesAsync(_repository, zoneMover, hostId, count, destination, fromBottom, ct, _gameEventQueue));
        }
        else
        {
            // (PRIM-W4/FR2 ImmuneStackTrashingClass / CanNotBeTrashedBySkill) a continuous "immune from
            // digivolution-stack trashing" flag on the host prevents effect-driven source trashing — honouring
            // any cardEffectCondition against the causing effect's source.
            // (C-5 adversarial review P1-2) AS-IS ITrashDigivolutionCards ALSO yield-breaks on the host top
            // card's GENERAL effect immunity (`TopCard.CanNotBeAffected(cardEffect)`, CardController.cs:5154-5155)
            // — the same gate the return-to-hand/deck branch above already mirrors.
            // (R3-W3c B6) ImmuneFromStackTrashing rehomed from the ImmuneStackTrashingKey registry scan to the
            // AS-IS-literal live getter Permanent.ImmuneFromStackTrashing(cardEffect) — the host permanent, cause
            // = the causing effect collapsed to its source card (BareCauseEffect). Needs the EngineContext; a
            // context-less sink can carry no live effect scan (nothing produced the flag anyway after the flip).
            // (B군 P0-1) The general-immunity OR-arm (AS-IS :5155 TopCard.CanNotBeAffected) is likewise rehomed
            // from the now-dead BlocksOpponentEffect registry scan to the live TopCard.CanNotBeAffected getter.
            if (_context is { } stackTrashCtx
                    && (new Assets.Scripts.Script.CardEffectCommons.Permanent(stackTrashCtx, hostId)
                            .ImmuneFromStackTrashing(Assets.Scripts.Script.CardEffectCommons.BareCauseEffect.For(stackTrashCtx, mutation.SourceEntityId))
                        || new Assets.Scripts.Script.CardEffectCommons.Permanent(stackTrashCtx, hostId)
                            .TopCard.CanNotBeAffected(Assets.Scripts.Script.CardEffectCommons.BareCauseEffect.For(stackTrashCtx, mutation.SourceEntityId))))
            {
                _skipped.Add(mutation);
                _applied.Add(new AppliedMutation(mutation.Kind, hostId, "restricted"));
                return;
            }

            // (B-2 DigiBurst rework) an explicit selected-card list (the AS-IS ITrashDigivolutionCards(permanent,
            // selectedCards, …) shape) trashes exactly those sources; otherwise the positional count/fromBottom form.
            if (mutation.Values.TryGetValue(SelectedCardIdsKey, out object? rawSelected) && rawSelected is string selectedCsv
                && !string.IsNullOrWhiteSpace(selectedCsv))
            {
                HeadlessEntityId[] selectedIds = selectedCsv
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(value => new HeadlessEntityId(value))
                    .ToArray();
                _pendingAsync.Add(ct => DigivolutionStackHelpers.TrashSpecificSourcesAsync(
                    _repository, zoneMover, hostId, selectedIds, ct, _gameEventQueue,
                    // (C-3) effect-trash: honour CanNotTrashFromDigivolutionCards (BT9_109) via TrashProtectionScan.
                    _effectRegistry, _context, mutation.SourceEntityId));
            }
            else
            {
                _pendingAsync.Add(ct => DigivolutionStackHelpers.TrashSourcesAsync(
                    _repository, zoneMover, hostId, count, fromBottom, ct, _gameEventQueue,
                    // (C-3) effect-trash path honours trash protection (deletion bypasses it — DeletionSourceTrash).
                    honorProtection: true, _effectRegistry, _context, mutation.SourceEntityId));
            }
        }

        _applied.Add(new AppliedMutation(mutation.Kind, hostId, "sourceRemoval"));
    }

    /// <summary>(B-10) Trash up to <c>count</c> (default all) of the host's link cards via LinkHelpers.</summary>
    private void ApplyTrashLinkCards(EffectMutation mutation, HeadlessEntityId hostId)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        int count = ReadInt(mutation.Values, CountKey) ?? int.MaxValue;
        GameEventQueue? queue = _gameEventQueue;
        _pendingAsync.Add(async ct =>
        {
            if (!_repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
            {
                return;
            }

            // Newest-first list; trash from the front up to count.
            HeadlessEntityId[] trashTargets = LinkHelpers.ReadLinkedCardIds(host.Metadata).Take(count).ToArray();

            // (R3-P1-5) AS-IS ITrashLinkCards (CardController.cs:5331): `new AceOverflowClass(_trashTargetCards)
            // .Overflow()` runs IMMEDIATELY BEFORE the link cards move — an un-flipped ACE link card leaving the
            // field costs its owner the printed Overflow memory. Same shared pass as DiscardEvoRoots' link-root
            // overflow (host still on the field, so the AS-IS on-field existence test holds).
            if (_memory is not null && _zoneMover is IZoneStateReader linkZones
                && (linkZones.GetCards(host.OwnerId, ChoiceZone.BattleArea).Contains(hostId)
                    || linkZones.GetCards(host.OwnerId, ChoiceZone.BreedingArea).Contains(hostId)))
            {
                Runtime.DeletionSourceTrash.ApplyAceOverflow(_repository, trashTargets, _memory, _currentTurnPlayer?.Invoke());
            }

            foreach (HeadlessEntityId linkCardId in trashTargets)
            {
                await LinkHelpers.RemoveLinkCardAsync(_repository, zoneMover, hostId, linkCardId, trash: true, queue, ct).ConfigureAwait(false);
            }
        });
        _applied.Add(new AppliedMutation(mutation.Kind, hostId, "trashLinkCards"));
    }

    private static ChoiceZone ReadZone(IReadOnlyDictionary<string, object?> values, string key, ChoiceZone fallback)
    {
        if (values.TryGetValue(key, out object? raw) && raw is not null)
        {
            switch (raw)
            {
                case ChoiceZone zone:
                    return zone;
                case string text when Enum.TryParse(text, ignoreCase: true, out ChoiceZone parsed) && Enum.IsDefined(parsed):
                    return parsed;
            }
        }

        return fallback;
    }

    /// <summary>(CV-A4) Open a global timing window for a state change that is not a zone move (so it is
    /// not derived from a CardMoved event). No-op when the sink was built without a game-event queue.</summary>
    private void EmitTiming(string timing, HeadlessPlayerId actor, HeadlessEntityId? subject = null)
    {
        if (_gameEventQueue is not null)
        {
            TriggerEventEmitter.Emit(_gameEventQueue, timing, actor: actor, subject: subject);
        }
    }

    private static bool IsKnownKind(string kind)
    {
        return KindToFlag.ContainsKey(kind)
            || kind is AddDpModifierKind or SuspendKind or UnsuspendKind or SetFlagKind or ClearFlagKind
            || kind is TrashCardKind or ReturnToHandKind or ReturnToDeckTopKind or ReturnToDeckBottomKind
                or AddToSecurityKind or DrawCardsKind or AddMemoryKind or SetMemoryKind
                or DeleteKind or PlayCardKind or RecoverKind or TrashSecurityKind or ShuffleSecurityKind or CreateTokenKind
                or TrashDigivolutionCardsKind or ReturnDigivolutionCardsKind or TrashLinkCardsKind
                or MaterialSaveKind or DeDigivolveKind
                or PlayDigivolutionAsDigimonKind;
    }

    private static HeadlessPlayerId ReadPlayer(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return default;
        }

        return raw switch
        {
            HeadlessPlayerId p => p,
            int i => new HeadlessPlayerId(i),
            long l when l >= int.MinValue && l <= int.MaxValue => new HeadlessPlayerId((int)l),
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) => new HeadlessPlayerId(p),
            _ => default,
        };
    }

    private static HeadlessEntityId ResolveTargetId(EffectMutation mutation)
    {
        if (mutation.Values.TryGetValue(TargetEntityIdKey, out object? raw))
        {
            switch (raw)
            {
                case HeadlessEntityId typed when !typed.IsEmpty:
                    return typed;
                case string text when !string.IsNullOrWhiteSpace(text):
                    return new HeadlessEntityId(text.Trim());
            }
        }

        return mutation.SourceEntityId;
    }

    private static int? ReadInt(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            int i => i,
            long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
            double d when d % 1 == 0 && d is >= int.MinValue and <= int.MaxValue => (int)d,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) => p,
            _ => null,
        };
    }

    private static long? ReadLong(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            long l => l,
            int i => i,
            string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long p) => p,
            _ => null,
        };
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> values, string key)
    {
        return values.TryGetValue(key, out object? raw) && raw is bool b && b;
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> values, string key)
    {
        return values.TryGetValue(key, out object? raw) && raw is string s ? s : null;
    }
}

public sealed record AppliedMutation(string Kind, HeadlessEntityId TargetId, string FlagKey);
