namespace HeadlessDCGO.Engine.Headless.Effects;

using EffectTiming = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.EffectTiming;

/// <summary>
/// (PRIM triggered-activated bridge) The single source of truth for WHICH trigger timings resolve a card's
/// ACTIVATED effects (draw/trash/delete/select via <c>ActivatedEffectResolver</c>) rather than its bound
/// mutation <c>IHeadlessCardEffect</c> (memory/DP). Shared by the batch pipeline's
/// <c>GameFlowProcessor.BridgeActivatedTriggersAsync</c> and the Stage-5 window loop's
/// <see cref="WindowResolverWiring"/> so both classify a timing identically (Stage-5 unifies them into one
/// seed at cut-over — see design RD 3b-ii/iii).
///
/// The four sets differ only in HOW the reacting card is found and how it is capped:
///  - <see cref="SubjectScoped"/>: the event's subject (the attacker / deleted card) resolves — fires once per
///    event, so no per-turn cap (except the multi-fire timings in <see cref="OncePerTurn"/>).
///  - <see cref="Boundary"/>: a turn boundary carries no subject; EVERY battle-area card resolves and gates
///    itself — one such event per turn, so the per-turn cap is natural.
///  - <see cref="EventBroadcast"/>: every battle-area card resolves PER driving event, with the event threaded
///    through so each card's own gate decides scope (self / ally / opponent host); a "[Once Per Turn]" reactor
///    caps itself via <c>MaxCountPerTurn</c> inside the resolver.
/// </summary>
public static class ActivatedBridgeTimings
{
    /// <summary>SUBJECT-scoped timings — the ACTIVATED effects of the card the event is about (the attacker /
    /// the deleted card) resolve. These fire once per event (per attack / per deletion), so no per-turn cap is
    /// needed. ALLOW-LIST excludes action-wired timings (OnEnterFieldAnyone → PlayCardAction, WhenDigivolving →
    /// DigivolveAction, OptionSkill/SecuritySkill) so nothing double-fires.</summary>
    public static readonly IReadOnlySet<EffectTiming> SubjectScoped = new HashSet<EffectTiming>
    {
        EffectTiming.OnAllyAttack,
        // (F1-DEAD) OnUseAttack is DELIBERATELY NOT registered: it is emitted on the SAME attack declaration as
        // OnAllyAttack (AttackPermanentAction → AttackDeclarationCommons.Declare fires both OnAttack="OnUseAttack"
        // and OnAllyAttack), so OnAllyAttack is the live subject-scoped attack-declaration reactor and adding
        // OnUseAttack here would DOUBLE-fire the attacker's activated effects on one attack. (AS-IS OnUseAttack is
        // the raw attack timing; "[When Attacking]" reactors are served by OnAllyAttack/OnDeclaration.)
        EffectTiming.OnDestroyedAnyone,
        EffectTiming.OnUnTappedAnyone,
        // OnTappedAnyone lives in EventBroadcast: "[Your Turn] when an OPPONENT'S Digimon is suspended, …" lives
        // on a DIFFERENT card (e.g. a Tamer, ST4_14) than the suspended subject, so it needs the cross-card
        // broadcast, not the subject-scoped path.
        // (B-2 / P1-5) [Main] skill-declaration timing. Resolved directly by MainSkillActivateAction
        // (ActivatedEffectResolver.ResolveAsync) in the Main phase — the attack-time proxy emit was removed. Kept
        // subject-scoped here so a direct TriggerEventEmitter.Emit(OnDeclaration) still bridges (a card gates
        // itself); no production path emits it after proxy removal, but the classification stays for that path.
        EffectTiming.OnDeclaration,
        // (F1-DEAD) OnKnockOut — subject = the battle-knocked-out card (its own "[On Deletion by battle]" reactor).
        // LATENT: production emits OnKnockOut via a SYNC window (BattleResolver.ResolveKnockOutWindowAsync →
        // RunSyncWindowAsync with AutoProcessingTriggerCollector), NOT the GameEventQueue, so CollectActivatedBridge-
        // Triggers (which scans queued events only) never sees it — this registration opens NOTHING in production
        // and does NOT change the C-4 vestigial phase-1 knockout window (that path never consults this set). It is a
        // classification hook so IF a queued OnKnockOut emit is ever added, the subject-scoped bridge is wired.
        // Distinct from OnDestroyedAnyone (the deletion-move reactor), so no double-fire. 0 cards react. design item
        // F1-DEAD-KNOCKOUT (queue emit).
        EffectTiming.OnKnockOut,
        // (F1-DEAD) OnGetDamage — subject = the card that takes damage ("[When this receives damage] …", by AS-IS
        // name semantics; 0 AS-IS cards so scope cannot be re-derived from real usage). NO emit source exists (no
        // headless damage pipeline) → fully LATENT/inert (no event ever derives this name). Placeholder only; the
        // damage pipeline + emit is design item F1-DEAD-DAMAGE — do NOT invent it. 0 cards react.
        EffectTiming.OnGetDamage,
    };

    /// <summary>(v3) Subject-scoped timings that can fire MULTIPLE times per turn (a card can be re-suspended and
    /// unsuspended). Their activated triggers are capped ONCE PER TURN via <c>OnceFlagController</c> — the AS-IS
    /// default for on-unsuspend triggers is "[Once Per Turn]", and under-firing a re-unsuspend is safer than
    /// over-firing. memory/DP on-unsuspend triggers keep their IHeadlessCardEffect forms (scheduler-capped) and
    /// are not bridged here.</summary>
    public static readonly IReadOnlySet<EffectTiming> OncePerTurn = new HashSet<EffectTiming>
    {
        EffectTiming.OnUnTappedAnyone,
    };

    /// <summary>(v2) BOUNDARY timings — carry no card subject (a turn boundary emitted with only an actor). The
    /// ACTIVATED effects of EVERY battle-area card resolve (each card gates "[End of YOUR turn]" itself). These
    /// fire exactly ONCE per turn (one such event per turn), so the per-turn cap is natural — no OnceFlags
    /// needed.</summary>
    public static readonly IReadOnlySet<EffectTiming> Boundary = new HashSet<EffectTiming>
    {
        EffectTiming.OnEndTurn,
        EffectTiming.OnStartTurn,
        EffectTiming.OnStartMainPhase,
        // (F1-DEAD) OnEndAttackPhase / OnEndMainPhase — phase-boundary timings, no card subject, emitted actor-only
        // to the GameEventQueue (PassAction.cs:28-29 TriggerEventEmitter.Emit). Now that the enum members exist,
        // this Boundary registration OPENS the bridge: at each phase-end the boundary scan visits every zone card,
        // then HasActivatedEffectsAt + CanCollectAt filter — behavior-neutral because 0 cards return effects at
        // these timings (verified). The per-turn cap is natural (one such event per turn). A fixture card
        // (F1-DeadTimingInfra) proves the scan→marker path works.
        EffectTiming.OnEndAttackPhase,
        EffectTiming.OnEndMainPhase,
        // (F1-DEAD) OnEndCoinToss — game-start coin-toss boundary (no subject). NO emit source exists (no headless
        // coin-toss pipeline) → LATENT/inert (no event ever derives this name). Classification placeholder; emit is
        // design item F1-DEAD-COINTOSS — do NOT invent the coin-toss pipeline. 0 cards react.
        EffectTiming.OnEndCoinToss,
        // (Other emitted windows use TriggerTimings names with no matching EffectTiming enum member yet — OnDraw —
        // so they need enum + name reconciliation first.)
        // NOTE: OnEndBattle lives in EventBroadcast — it carries battle-result metadata (winnerIds/loserIds)
        // that "[End of Battle] when this deletes an opponent in battle" gates need, which the boundary branch
        // discards.
    };

    /// <summary>(Phase D broadcast) EVENT-BROADCAST timings — the AS-IS fires these via a global
    /// StackSkillInfos(hashtable, timing) that offers the event to EVERY field card; each card's OWN gate
    /// (e.g. <c>CanTriggerOnTrashDigivolutionCard</c>) decides scope. The bridge scans every battle-area card
    /// and passes the DRIVING EVENT through so gates read the event subject and metadata. Fires once per event
    /// (a second trash that turn fires again, as AS-IS), so no per-turn cap here; a card caps itself via
    /// MaxCountPerTurn.
    /// KNOWN BOUNDARY: AS-IS GetSkillInfos also offers the timing to trash / hand / face-up security cards; this
    /// scan covers the battle area only (same model as the boundary scan) — every current listener at these
    /// timings lives on the field. Extend the scan zones when a trash/hand listener is actually ported.</summary>
    public static readonly IReadOnlySet<EffectTiming> EventBroadcast = new HashSet<EffectTiming>
    {
        EffectTiming.OnDigivolutionCardDiscarded,
        // (c-remediation) digivolution cards returned to deck bottom — cross-card reactors thread the driving event.
        EffectTiming.OnDigivolutionCardReturnToDeckBottom,
        // (Phase D) [End of Battle] — emitted actor-only with battle-result metadata (winnerIds/loserIds/…).
        // Broadcast to every field card, threading the driving event so gates like
        // CanTriggerWhenDeleteOpponentDigimonByBattle read "event.winnerIds"/"event.loserIds"; each card
        // self-gates. One resolve per battle event (a turn with N battles opens N windows, as AS-IS).
        EffectTiming.OnEndBattle,
        // (Phase D) [Your Turn] "when an opponent's Digimon is suspended, …" — the reacting effect lives on a
        // different card (Tamer, ST4_14) than the suspended subject, so it broadcasts. The event carries
        // subject = the suspended card; each listener's CanTriggerWhenPermanentSuspends self-gates on it.
        EffectTiming.OnTappedAnyone,
        // (G2) [Your Turn]/[All Turns] "when an Option is used, …" — the reacting effect lives on a DIFFERENT
        // card than the option card being used. Broadcast to every battle-area card, threading the driving event
        // so each listener's CanTriggerWhenUseOption / CanTriggerWhenOwnerUseOption self-gates on the used option
        // (subject) + its cost. Fires once per option use; a "[Once Per Turn]" reactor caps itself via
        // MaxCountPerTurn.
        EffectTiming.OnUseOption,
        // (D-2 / VR-9) [All Turns] "when any of your opponent's Digimon LEAVE the battle area, …" (AD1_025) — the
        // reacting effect lives on a DIFFERENT card than the leaving one. AS-IS stacks the WHOLE leave batch as
        // ONE StackSkillInfos(OnLeaveFieldAnyone) payload whose gate any-matches the list (CardController.cs:3748),
        // so N simultaneous leaves fire the reactor ONCE. Headless emits ONE CardMoved (leave) per card, so the
        // batch is collapsed at collect: WindowResolverWiring.CollectActivatedBridgeTriggers dedups this timing to
        // the FIRST gate-passing leave subject per reactor (mirrors the AS-IS any-match). Each listener's
        // CanTriggerOnPermanentLeave self-gates on the leave subject (its pre-removal zone — Permanent.SnapshotZone).
        // Self-scoped WhenRemoveField/OnRemovedField are NOT here (they stay per-card via the scheduler half).
        EffectTiming.OnLeaveFieldAnyone,
        // (F1-M1) OnLoseSecurity — "[…] when a card is removed from your (opponent's) security stack, …" (73 AS-IS
        // cards, e.g. BT24_018 delete / BT15_037 gain-memory). AS-IS fires it via a global StackSkillInfos(hashtable,
        // EffectTiming.OnLoseSecurity) whose payload is {Player = the security-losing player} (CardController.cs:5444,
        // hashtable {"Player", _player} at :5435). It is a PLAYER-SCOPE broadcast: the reacting effect lives on a
        // DIFFERENT card (a field Digimon/Tamer) than the removed security card, and its gate
        // CanTriggerWhenLoseSecurity(hashtable, playerCondition) self-scopes on WHICH player lost security
        // (player == card.Owner vs card.Owner.Enemy). Headless derives the timing from the CardMoved zone transition
        // (from == Security && to != Security, TriggerTimingMap.cs:164), whose Subject = the moved security card and
        // whose owner = the losing player (= AS-IS _player for the standard same-owner security card; a foreign-owned
        // security card is a documented boundary — no such reactor is ported). The bridge threads that CardMoved as
        // the driving event so CanTriggerWhenLoseSecurity (CardPortingFramework.cs:8641) reads the subject's owner and
        // self-gates. Fires once per removed-security CardMoved (a second removal that turn fires again, as AS-IS); a
        // "[Once Per Turn]" reactor caps itself via MaxCountPerTurn.
        //   SCAN-ZONE (P2-C sweep, all 73 AS-IS OnLoseSecurity cards audited -a): every OnLoseSecurity CanUse gates on
        //   IsExistOnBattleArea / IsExistOnBattleAreaDigimon (true-scan verified — the IsExistOnTrash/IsExistOnHand hits
        //   in EX4_050/BT25_088/BT25_040/BT15_037/BT15_084/BT21_093/BT24_016 all belong to OTHER effects/timings in
        //   those files, not the OnLoseSecurity block), so there are ZERO hand/trash/security-resident listeners and
        //   ScanZones (battle+trash+hand) covers them — no zone extension needed.
        //   INHERITED (F1-M1-INHERITSCAN — RESOLVED 2026-07-12 for uniform ActivatedEffect reactors): ScanZones now
        //   also yields each battle-area top's active digivolution-SOURCE ids (InheritedEffectHelpers.ActiveInheritedSources,
        //   the C-3 membership: inherited=non-top only · flipped skipped · non-top requires Digimon host), and the
        //   resolver's MembershipKeeps splits inherited-source scan (ActivatedEffect.IsInheritedEffect=true only) from
        //   the top scan (non-inherited only). So an inherited `ActivatedEffect` reactor on a source now fires.
        //   SCOPE (not over-claim): this covers ONLY uniform `ActivatedEffect` bridge reactors. Two adjacencies stay
        //   uncovered and are SEPARATE pre-existing items, NOT this scan: (a) inherited TRIGGER reactors that are
        //   `IHeadlessCardEffect` not `IActivatedCardEffect` (TriggeredMemoryEffect/UnsuspendSelfTriggerEffect — e.g.
        //   OnDestroyedAnyone memory on a source) ride the scheduler half, which has no inherited-source iteration; and
        //   (b) LINKED-effect sources (AS-IS EffectList_ForCard IsLinkedEffect at Permanent.cs:1532) — ScanZones yields
        //   inherited sources only, not linked; 0 linked-activated reactors ported (latent). Witness = BT9_021 (inherited
        //   OnAddHand). Remaining ~28 inherited OnLoseSecurity cards are skeleton stubs → picked up unchanged when ported.
        //   SCHEDULER-HALF SYMMETRY: TriggerTimings.BroadcastTimings (the scheduler/memory-DP half) is intentionally
        //   NOT updated — all 73 AS-IS OnLoseSecurity effects are uniform `new ActivateClass()` (activated-half), so
        //   there are ZERO memory/DP (IHeadlessCardEffect) OnLoseSecurity reactors that the scheduler half would
        //   broadcast. A gain-memory OnLoseSecurity reactor (BT15_037/BT6_034) is ported as a uniform ActivatedEffect
        //   with a MemoryBody and rides THIS activated bridge only (HasActivatedEffectsAt true) — it is NOT also a
        //   registered scheduler binding, so it single-fires (no double-collect). Per the design §4 asymmetry note,
        //   both halves are updated only when a real scheduler-half reactor arrives; none does at OnLoseSecurity.
        EffectTiming.OnLoseSecurity,
        // (F1 Tier1 / OnMove) "When a Digimon MOVES from the breeding area to the battle area, …" (30 AS-IS cards).
        // AS-IS emit: CardObjectController.cs:1111 StackSkillInfos(hashtable {Permanent = movingPermanent}, OnMove)
        // fired once per PROMOTED permanent (a SINGLE subject, not a list). Gate CanTriggerOnMove(hashtable,
        // permanentCondition) reads the moved permanent and requires it to be on the battle area (CanUseEffects/
        // OnMove.cs). Headless derives OnMove from the CardMoved BreedingArea->BattleArea transition
        // (TriggerTimingMap.cs:108), whose Subject = the promoted card; the bridge threads that CardMoved as the
        // driving event so each reactor's CanTriggerOnMove reads the subject and self-gates.
        //   CLASSIFICATION = EventBroadcast (NOT SubjectScoped, and NOT BOTH). Of the 30 cards, 18 are SELF reactors
        //   (PermanentCondition => `permanent == card.PermanentOfThisCard()`, e.g. EX11_007/ST22_03/ST24_04) and 12
        //   are ANYONE/cross-card reactors whose PermanentCondition matches a DIFFERENT card by property
        //   (BT8_092 = "your Digimon with [X Antibody]"; BT6_088 = name [Gabumon]/[Garurumon]; BT5_044 = an
        //   OPPONENT'S Digimon). Because cross-card anyone reactors exist, SubjectScoped alone (which resolves only
        //   the subject's OWN effects) would DROP them — so it must be EventBroadcast (scan every field card + thread
        //   the event). And unlike OnDestroyedAnyone (whose subject leaves to the trash, so it ALSO needs SubjectScoped
        //   to reach the departed card), the OnMove subject ENDS UP on the battle area (the gate even requires it), so
        //   the EventBroadcast field scan already visits the subject — the self reactors are covered by the same scan
        //   (each self-gates `permanent == self`). Registering OnMove in SubjectScoped TOO would be actively wrong:
        //   CollectActivatedBridgeTriggers is an else-if chain (SubjectScoped wins first), so it would take the
        //   subject-only branch and DROP every anyone reactor. Hence EventBroadcast EXCLUSIVELY.
        //   BATCH (#9): a promotion is a SINGLE permanent per StackSkillInfos (one CardMoved), and multiple promotions
        //   are separate events (AS-IS stacks each separately) — so there is NO effect-batch multi-subject collapse
        //   here (unlike OnLoseSecurity/OnLeaveFieldAnyone). No batch-id substrate needed; each move fires once.
        //   INHERITED (design item F1-M1-INHERITSCAN, latent): exactly 1 of 30 registers OnMove as an INHERITED
        //   (진화원) effect — EX10_004 (SetIsInheritedEffect on the OnMove ActivateClass itself, EX10_004.cs:14-19).
        //   (The other files carrying SetIsInheritedEffect — BT24_052/BT24_069/EX11_008/013/026/033/048 — flag a
        //   DIFFERENT timing's effect, e.g. WhenRemoveField/OnEndBattle/OnLoseSecurity, NOT their OnMove effect,
        //   verified per-block.) Same common gap as OnLoseSecurity: the bridge dispatches only the top instance's
        //   own effect class and never iterates a permanent's digivolution-source inherited effects, so EX10_004's
        //   inherited OnMove reactor sitting UNDER another Digimon would be missed. LATENT — all 30 headless OnMove
        //   cards are skeleton stubs today (0 ported inherited reactors), and the ported witnesses are NON-inherited
        //   top-permanent reactors (BT8_092 / TfxOnMoveSelfDraw); close with the shared inherited-source scan.
        //   SCHEDULER-HALF SYMMETRY: TriggerTimings.BroadcastTimings is intentionally NOT updated — all 30 OnMove
        //   reactors are uniform `new ActivateClass()` (activated-half); there are 0 memory/DP (IHeadlessCardEffect)
        //   OnMove reactors, so no scheduler-half broadcast exists to add (same rationale as OnLoseSecurity).
        EffectTiming.OnMove,
        // (F1-Tier1) OnDiscardHand / OnDiscardSecurity / OnDiscardLibrary — the three discard timings are structurally
        // identical (a card sent to the trash from a NON-field zone, fired via a global StackSkillInfos to every field
        // card). AS-IS emits each ONCE per logical discard with a payload {DiscardedCards = the whole LIST, CardEffect}:
        //   * OnDiscardHand  — DiscardHands, CardController.cs:48-56 (a card trashed from hand by an effect).
        //   * OnDiscardSecurity — IDestroySecurity, CardController.cs:4369-4377 (security cards trashed by an effect).
        //   * OnDiscardLibrary — TrashDeckCards, CardController.cs:5807-5816 (deck/library cards trashed by an effect).
        // Gates (CanUseEffects/OnTrashHand.cs, WhenDiscardSecurity.cs, WhenDiscardLibrary.cs, all ported into
        // CardPortingFramework): DiscardedCards.Any(cardCondition) — an ANY-MATCH over the discarded list. Hand/Security
        // ALSO require CardEffect != null (the discard must be effect-driven) and may gate on the causing effect's
        // EffectSourceCard; Library does not (only !IsBeingRevealed). The !IsBeingRevealed exclusion IS wired: the
        // reveal path (SimplifiedRevealAndSelectEffect / RevealMultiSelectEffect) routes its unselected remainder to
        // the trash, deriving OnDiscardLibrary; that reveal-remainder trash is stamped with RevealTrashFlagKey (the
        // IsBeingRevealed mirror — AS-IS resets the flag only AFTER TrashRevealedCards) so CanTriggerWhenDiscardLibrary
        // rejects it. A DIRECT effect-driven library trash (plain TrashDeckCards) carries no flag and fires normally.
        //   HEADLESS DERIVATION: TriggerTimingMap derives each from a Hand/Security/Library->Trash CardMoved
        //   (TriggerTimingMap.cs:143-157) whose Subject = the discarded card. The bridge threads that CardMoved so the
        //   ported gate reads the subject (via EventCards) and any-matches; the CAUSE effect id + discard batch id are
        //   stamped on the move (MatchStateMutationSink.ApplyTrashCard / TrashSecurityAsync). Because the discarded card
        //   is now IN the trash, the trash zone scan (ScanZones) reaches self-scoped reactors like "when THIS card is
        //   trashed from security" (BT18_098 / BT15_092 — CanActivate = IsExistOnTrash), collected from the trash.
        //   BATCH (#9, batch-semantics substrate REUSED from OnLoseSecurity/OnLeaveFieldAnyone): AS-IS fires ONE
        //   StackSkillInfos for the whole N-card discard, so N simultaneous discards fire a reactor ONCE. Headless emits
        //   one CardMoved per discarded card; ApplyTrashCard stamps all cards of one sink flush with ONE shared discard
        //   batch id (Hand/Library) and TrashSecurityAsync stamps its security-loss id (Security), so
        //   CollectActivatedBridgeTriggers collapses to the FIRST gate-passing discard per reactor per batch id (mirrors
        //   the AS-IS any-match) while an INDEPENDENT discard (a distinct id) fires again. NON-effect trashes carry no
        //   batch id (sentinel 0) AND no cause id — the attack security-CHECK reveal (a bare zone move) therefore
        //   fails the OnDiscardSecurity CardEffect!=null gate, so it does NOT fire OnDiscardSecurity (it still fires
        //   OnLoseSecurity, which has no cause gate) — matching AS-IS (OnDiscardSecurity emits ONLY from IDestroySecurity).
        //   CLASSIFICATION = EventBroadcast (cross-card: the DiscardedCards list is the driving payload and a reactor
        //   often lives on a DIFFERENT card than the discarded one — e.g. BT19_071 deletes an OPPONENT'S Digimon when
        //   YOUR deck is trashed). Self-scoped reactors ("when THIS card is trashed") self-gate via cardCondition ==
        //   this card and are reached by the same field+trash scan; no SubjectScoped registration is needed (unlike
        //   OnDestroyedAnyone, the discarded card is not a field departure, so there is no subject-scoped leave hook).
        //   INHERITED (design item F1-M1-INHERITSCAN, latent, shared with OnLoseSecurity/OnMove): the bridge dispatches
        //   only the TOP instance's own effect class and never iterates a permanent's digivolution-source inherited
        //   effects, so an INHERITED discard reactor (e.g. BT8_006's inherited <Draw 1> OnDiscardLibrary) sitting under
        //   another Digimon is missed. The ported witnesses (ST16_14 / BT19_071) are NON-inherited top-permanent
        //   reactors, so 0 ported reactors need it today; close with the shared inherited-source scan.
        //   SCHEDULER-HALF SYMMETRY: TriggerTimings.BroadcastTimings is intentionally NOT updated — every AS-IS discard
        //   reactor is a uniform ActivateClass (activated-half); there are 0 memory/DP (IHeadlessCardEffect) discard
        //   reactors, so no scheduler-half broadcast exists to add (same rationale as OnLoseSecurity/OnMove).
        EffectTiming.OnDiscardHand,
        EffectTiming.OnDiscardSecurity,
        EffectTiming.OnDiscardLibrary,
        // (F1-Tier1 OnAddHand / OnAddSecurity) the two "a card was ADDED to a zone" broadcast timings.
        //   * OnAddHand (21 AS-IS cards) — AS-IS CardObjectController.AddHandCards (:559) fires ONE
        //     StackSkillInfos(OnAddHand) per hand-add with payload {Players = distinct owners of the added cards,
        //     CardEffect = the causing effect, CardSources = the WHOLE added LIST} (:616). Gate
        //     CanTriggerOnHandAdded (OnCardsAddedToHand.cs:12) is PLAYER-SCOPE (Players.Contains(player)) AND
        //     REQUIRES CardEffect != null (:19) — the add must be EFFECT-driven — and may gate on the causing
        //     effect's EffectSourceCard. A NON-effect add (turn draw / mulligan / initial deal, cardEffect=null)
        //     never fires it (guard also: DoneStartGame, :605). Headless derives OnAddHand from a ->Hand CardMoved
        //     (TriggerTimingMap.cs:113); the effect-driven draw / return-to-hand sink paths stamp the CAUSE effect
        //     id + a shared ADD-HAND batch id (MatchStateMutationSink.ApplyDraw / ReturnToHandKind →
        //     IZoneMover.Draw/AddToHandAsync), so the ported gate reads the subject's owner (the gaining player) +
        //     the cause and self-gates. A bare (non-effect) draw carries neither, so it fails the CardEffect!=null
        //     gate — matching AS-IS. Cross-card: the reactor lives on a DIFFERENT field card than the added card.
        //     BATCH (#9, substrate REUSED from OnDiscard*): AS-IS fires ONE StackSkillInfos for the whole N-card
        //     add, so N simultaneous hand-adds fire a reactor ONCE. Headless emits one ->Hand CardMoved per added
        //     card sharing ONE add-hand batch id per sink flush, so CollectActivatedBridgeTriggers collapses to the
        //     FIRST gate-passing add per reactor per batch (an INDEPENDENT hand-add, a distinct id, fires again).
        //   * OnAddSecurity (14 AS-IS cards) — AS-IS IAddSecurity.AddSecurity (CardController.cs:5478) fires ONE
        //     StackSkillInfos(OnAddSecurity) per SINGLE added security card with payload {Player = source.Owner,
        //     CardSources = [that one card]}: it is PER-CARD (AddSecurityCard runs IAddSecurity UNCONDITIONALLY per
        //     card — recovery of N cards fires it N times), NOT a batch. Gate CanTriggerWhenAddSecurity
        //     (WhendAddSecurity.cs) delegates to the lose-security shape (player-scope over the gaining player, NO
        //     cause). Headless derives OnAddSecurity from a ->Security CardMoved (TriggerTimingMap.cs:159), one per
        //     added card, so it naturally fires PER CARD — NO batch collapse (matching AS-IS's per-IAddSecurity fire).
        //   INHERITED (design item F1-M1-INHERITSCAN, RESOLVED): the shared digivolution-source scan is now wired
        //   (WindowResolverWiring.ScanZones yields each battle-area permanent's active inherited sources; the
        //   resolver's HasActivatedEffectsAt/CanCollectAt/CanActivateAt/ResolveAsync apply the AS-IS
        //   EffectList_ForCard membership split via ActivatedEffect.IsInheritedEffect). BT9_021 (OnAddHand,
        //   SetIsInheritedEffect(true)) is the ported witness — it fires as a non-flipped source under a Digimon
        //   host and NOT as a top permanent. OnAddSecurity's inherited card (BT9_003) is still a skeleton stub, so
        //   its inherited reactor stays latent until ported (the scan will pick it up unchanged).
        //   REVEAL-TO-HAND (design item F1-ADDHAND-REVEAL, latent): AS-IS RevealLibrary routes its selected/remainder
        //   to hand via AddHandCards(list, false, activateClass) — a NON-null cause, so it DOES fire OnAddHand. The
        //   headless reveal state machine (RevealAndSelect.MoveAsync → AddToHandAsync) does not thread the reveal's
        //   source effect id, so a reveal-to-hand carries no cause and fails the CardEffect!=null gate (UNDER-fires,
        //   the safe direction — it does NOT over-fire). No ported OnAddHand reactor reacts to a reveal-to-hand
        //   today; close by threading the reveal source id into RevealAndSelect.MoveAsync when such a card is ported.
        //   SCHEDULER-HALF SYMMETRY: TriggerTimings.BroadcastTimings is intentionally NOT updated — every AS-IS
        //   OnAddHand/OnAddSecurity reactor is a uniform ActivateClass (activated-half); 0 memory/DP reactors exist.
        EffectTiming.OnAddHand,
        EffectTiming.OnAddSecurity,
        // (F1-Tier2 OnEndAttack) "[End of Attack] …" — a single attack has just finished (77-80 AS-IS cards). This is
        // the FIRST F-1 Tier2 (self-attacker) timing.
        //   AS-IS emit: AttackProcess.EndAttack (AttackProcess.cs:472-484) fires a GLOBAL StackSkillInfos(EffectHashtable,
        //   OnEndAttack) at the End state (AFTER the battle / security-check outcome, BEFORE CleanUp), guarded on the
        //   attacker still being alive (`AttackingPermanent != null && AttackingPermanent.TopCard != null`). The payload
        //   EffectHashtable is the SAME one built at attack DECLARATION ({AttackingPermanent, CardEffect} —
        //   HashtableSetting.cs:310-317) — it carries ONLY the attacker, no defender / battle-result. Distinct enum
        //   member from OnEndAttackPhase (the whole attack-STEP boundary, a F1-DEAD Boundary timing) — do not conflate.
        //   Gate CanTriggerOnEndAttack = CanTriggerOnAttack = CanTriggerOnPermanentAttack(hashtable, p =>
        //   p.cardSources.Contains(card)) (OnEndAttack.cs:10-13 → OnAttack.cs:10-45): the reacting card must be a
        //   cardSource (TOP or digivolution-SOURCE) of the AttackingPermanent, and that permanent must still be alive
        //   (TopCard != null). Fires REGARDLESS of whether a battle happened (unblocked / security-only attacks fire it
        //   too) as long as the attacker survives.
        //   HEADLESS: OnEndAttack is NOT a zone transition, so it cannot be DERIVED from a CardMoved — it is an EXPLICIT
        //   TriggerEventEmitter.Emit(GameEventQueue, TriggerTimings.OnEndAttack, actor: turnPlayer, subject: attackerId)
        //   from AttackPipeline.AdvanceEndAttackAsync, guarded on the attacker still being on the battle area (mirroring
        //   the AS-IS alive guard). TriggerTimingMap returns the explicit timing outright (TriggerTimingMap.cs:18-22), so
        //   no map case is added. The bridge threads that event as the driving event; the ported gate reads its Subject
        //   (= attackerId → BuildUniformResolveContext.TriggerEntityId) and self-gates via SubjectPermanentContains
        //   (TriggerEntityId == card OR the subject's SourceIds contain card — the cardSources.Contains mirror).
        //   CLASSIFICATION = EventBroadcast (NOT SubjectScoped). All 80 AS-IS OnEndAttack reactors are SELF-scoped (0
        //   cross-card / anyone reactors — verified by full scan of CanTriggerOnPermanentAttack usage), so scope alone
        //   would permit SubjectScoped. The DECIDING factor is INHERITED coverage: an INHERITED (진화원) OnEndAttack
        //   reactor (BT9_062 — SetIsInheritedEffect(true), fires when its Alphamon host attacks) sits UNDER the attacker
        //   and is matched by AS-IS cardSources.Contains(card). SubjectScoped adds ONLY the subject top
        //   (WindowResolverWiring.cs:803-806, Inherited=false hardcoded) and would DROP it; EventBroadcast's ScanZones
        //   visits the top's active digivolution sources (Inherited=true) and each self-gates true via the SourceIds
        //   branch. The attacker is still on the battle area at emit (self-delete via Execute happens AFTER, in
        //   DeleteSelfAtEndOfAttackAsync), so the EventBroadcast field scan already visits the attacker itself — the top
        //   self reactors (BT9_043 / BT1_081) are covered by the same scan.
        //   BATCH: a single attack fires ONE StackSkillInfos with ONE subject (the attacker) — NOT a list. Multiple
        //   attacks are separate events. So there is NO effect-batch multi-subject collapse and NO batch-id substrate
        //   (like OnMove, unlike OnLoseSecurity / OnDiscard*). Each attack-end fires its reactors once.
        //   INHERITED (design item F1-M1-INHERITSCAN, RESOLVED): the shared digivolution-source scan (ScanZones) already
        //   covers this timing the moment it is EventBroadcast-registered — BT9_062 is the ported inherited witness
        //   (fires as a non-flipped source under an Alphamon host, NOT as a top permanent), the OnEndAttack analogue of
        //   BT9_021 (OnAddHand).
        //   SCHEDULER-HALF SYMMETRY: TriggerTimings.BroadcastTimings is intentionally NOT updated — all 80 OnEndAttack
        //   reactors are uniform ActivateClass (activated-half); there are 0 memory/DP (IHeadlessCardEffect) OnEndAttack
        //   reactors, so no scheduler-half broadcast exists to add (same rationale as OnMove / OnLoseSecurity).
        //   HOOK COEXISTENCE + DOUBLE-FIRE (design item F1-ENDATTACK-HOOK): EndAttackTriggerHook still runs inline in
        //   AdvanceEndAttackAsync and collects the SCHEDULER half (registered IHeadlessCardEffect bindings) via a
        //   bespoke synthetic AttackResolved event — off-queue. Activated effects are NEVER registered
        //   (CardPortingFramework.cs:13262 skips IActivatedCardEffect), so the hook and the activated bridge are DISJOINT
        //   by interface (HasActivatedEffectsAt excludes IHeadlessCardEffect) — no effect is collected by both today
        //   (0 scheduler-half OnEndAttack reactors). If a real memory/DP OnEndAttack reactor is ever added it would
        //   double-collect (hook + unified-seed scheduler collector over the queued event); the faithful fix then is to
        //   retire the hook's scheduler collection into the unified seed. LATENT until such a card is ported.
        EffectTiming.OnEndAttack,
        // (F1-Tier2 WhenLinked) "[When Linked] …" — a card was just linked onto a Digimon (64 AS-IS cards). Second
        // F-1 Tier2 unit.
        //   AS-IS emit: Permanent.AddLinkCard (Permanent.cs:1237-1291) fires StackSkillInfos(hashtable, WhenLinked)
        //   ONCE per successful attach (`addedCard == true`), with payload {Permanent = the HOST that received the
        //   link, Card = the attached link card, CardEffect, isFromDigimon}. PER-CARD: a card that links N cards calls
        //   AddLinkCard N times (caller foreach — e.g. AD1_005) → N separate WhenLinked emits (NOT a list payload).
        //   Gates (CanUseEffects/WhenLinked.cs) come in TWO shapes (of the 64 WhenLinked files, 42 use the host gate,
        //   35 the link-card gate; some cards use both):
        //     * CanTriggerWhenLinked(ht, permanentCondition, sourceCondition) — the HOST-perspective gate: applies
        //       permanentCondition to the HOST permanent and sourceCondition to the attached link card. Self-scope is
        //       `permanent == card.PermanentOfThisCard()`; owner-anyone is IsPermanentExistsOnOwnerBattleAreaDigimon
        //       with no self-equality. NO cross-player reactor exists (all owner-side).
        //     * CanTriggerWhenLinking(ht, permanentCondition, card) — the LINK-CARD-perspective gate (`Card == card`,
        //       the reacting card IS the attached link card). See LINK-CARD GAP below.
        //   (The AS-IS payload also carries CardEffect (cause) + isFromDigimon; the headless emit omits both. Safe:
        //   0 WhenLinked reactors read isFromDigimon (only BT22_006/EX5_065, both OnAddDigivolutionCard), and the
        //   AS-IS CardEffect!=null guard is unreachable — every AddLinkCard caller passes a non-null cause. Latent.)
        //   HEADLESS: emit ALREADY EXISTS (LinkHelpers.cs:139, wired at D-1) — TriggerEventEmitter.Emit(WhenLinked,
        //   actor: hostOwner, subject: HOST id, extraMetadata {linkCardId}). WhenLinked is NOT a zone transition (the
        //   link card moves to None/off-field), so it is an EXPLICIT emit and TriggerTimingMap returns it outright — no
        //   map case. The gate CanTriggerWhenLinked ALREADY EXISTS (CardPortingFramework.cs:8764): it reads
        //   TriggerEntityId (= subject = HOST) for permanentCondition and event Values["linkCardId"] for
        //   sourceCondition. enum + const + gate + emit are all pre-existing — the ONLY missing wire is this
        //   EventBroadcast registration.
        //   CLASSIFICATION = EventBroadcast (NOT SubjectScoped): the 8 owner-anyone reactors live on a DIFFERENT card
        //   than the linked host, so SubjectScoped (which resolves only the subject's own effects) would DROP them. The
        //   field scan visits every card and each self-gates on the host subject.
        //   BATCH: one AddLinkCard = one emit with one subject (the host) — no list, no effect-batch collapse, no
        //   batch-id (like OnMove/OnEndAttack). EnforceLinkedMax's overflow trash fires OnLinkCardDiscarded (a separate
        //   timing), never a second WhenLinked. Per-card is proven by the TfxWhenLinkedCounter 2-link test (+2).
        //   ORDER (design item F1-WHENLINKED-TRIMORDER, latent, pre-existing D-1): headless emits WhenLinked BEFORE
        //   EnforceLinkedMaxAsync trims the overflow (LinkHelpers.cs:139 then :144), whereas AS-IS trims FIRST then
        //   emits (Permanent.cs:1250-1289). So the WhenLinked/OnLinkCardDiscarded window order is reversed, and a
        //   WhenLinked reactor that reads the host's linked count/DP during resolution would see the PRE-trim state.
        //   Final linked set is count-equivalent; latent until a real WhenLinked card inspects linked count mid-resolve.
        //   GATE LIVENESS (latent, same class as OnMove's requireOnBattleArea): the headless CanTriggerWhenLinked
        //   mirrors neither AS-IS Permanent.TopCard != null nor CardEffect != null. Masked today — all 42 host-gate
        //   cards pass a real permanentCondition that re-checks battle-area membership, and no caller passes a null
        //   cause. Add the explicit re-checks if a card ever passes a null permanentCondition.
        //   INHERITED (F1-M1-INHERITSCAN, RESOLVED): the host is a battle-area top, so ScanZones' inherited-source
        //   visit auto-covers an inherited [When Linked] reactor sitting under the host — BT22_003 is the ported
        //   inherited witness (SetIsInheritedEffect(true), Digimon-self, -2000 DP). BT22_035 (host-self top, already
        //   ported at C-2) goes live the moment this registration lands.
        //   SCHEDULER-HALF SYMMETRY: TriggerTimings.BroadcastTimings intentionally NOT updated — all WhenLinked
        //   reactors are uniform ActivateClass (activated-half); 0 memory/DP reactors. No EndAttackTriggerHook-style
        //   inline scheduler collector exists for WhenLinked, so no CollectUnifiedSeed skip is needed either.
        //   LINK-CARD GAP (design item, latent, = C2-01 / IsLinkedEffect): the CanTriggerWhenLinking gate reacts from
        //   the ATTACHED LINK CARD's position, but a link card lives off-field (ChoiceZone.None), which ScanZones does
        //   NOT visit — so a link-card-self [When Linking] activated reactor (22 AS-IS cards; the ACTIVATED ones, e.g.
        //   BT22_035's deferred -4000 DP) is NOT reached by this EventBroadcast wire. It shares the C2-01 IsLinkedEffect
        //   lifecycle gap (ActivatedEffect has no link-card marker). Host-perspective WhenLinked reactors (this golf's
        //   witnesses) are fully covered; the link-card-self addendum stays latent until C2-01 is remediated.
        EffectTiming.WhenLinked,
        // (F1-Tier2 OnAddDigivolutionCards) "[When ... is placed in this Digimon's digivolution cards by an effect] …"
        // (50 AS-IS cards). Third F-1 Tier2 unit — and the one whose EMIT was INVERTED, requiring a real fix (not a
        // one-line register).
        //   AS-IS emit: ONLY from Permanent.AddDigivolutionCardsTop (Permanent.cs:1119) / AddDigivolutionCardsBottom
        //   (:1223, and only when !skipEffectAndActivateSkill) — i.e. an EFFECT places >=1 card under a permanent
        //   (place-under, DigiXros, Assembly, App-Fusion, Save/MaterialSave/Training). Payload {Permanent=receiving
        //   host, CardEffect=cause, CardSources=the added batch, isFromSameDigimon, isFromDigimon}. The gate hard-
        //   requires CardEffect != null (OnAddDigivolutionCards.cs:24). NATURAL digivolution does NOT emit it (AS-IS
        //   stacks the previous top via a plain AddCardSource, CardController.cs:1365-1375, no StackSkillInfos).
        //   HEADLESS EMIT FIX (design item F1-ADDDIGI-EMIT, remediated here): the headless emit was INVERTED — it fired
        //   on EVERY natural digivolve (DigivolveAction.cs, over-fire) and was SILENT on effect place-under
        //   (DigivolutionStackHelpers, under-fire). Fixed by (1) removing the DigivolveAction emit, (2) emitting from
        //   DigivolutionStackHelpers.AddSourcesBottomAsync/AddSourcesTopAsync/MoveSourcesBottom/TrainAsync (the AS-IS
        //   AddDigivolutionCards* port) with subject=host + causeSourceId, honoring skipEffectAndActivateSkill, and
        //   (3) hardening the gate to require a non-empty causeSourceId (mirrors AS-IS CardEffect != null; also fixes
        //   it reading SourceEntityId=host instead of the cause). Natural digivolve and effect-add are fully separate
        //   headless paths (DigivolveAction never calls DigivolutionStackHelpers), so the fix cannot cross-fire.
        //   CLASSIFICATION = EventBroadcast (NOT SubjectScoped): of the 50, self=38 / owner-anyone=10 / OPPONENT=2
        //   (BT11_088/EX10_056 — a reactor firing when the OPPONENT's permanent receives sources). anyone + cross-
        //   player reactors exist, so SubjectScoped would drop them; the field scan visits every card and self-gates.
        //   BATCH: ONE emit per place-under with a CardSources LIST payload (addedCardIds), gate any-matches — NOT
        //   per-card. N sources added at once = ONE fire. No batch-id collapse needed (single emit).
        //   INHERITED (F1-M1-INHERITSCAN, RESOLVED): 28/50 are inherited; ScanZones auto-covers an inherited reactor
        //   under the receiving host. Ported inherited witness: EX6_001 (memory) — TBD.
        //   ★MEMORY LATENT (the design doc's "memory latent gap" — RESOLVED as no-op): 0 of the 50 are non-activated
        //   (continuous memory/DP) reactors — ALL are ActivateClass, incl. the +memory ones (BT22_044/EX6_001/…),
        //   which call AddMemory INSIDE the ActivateCoroutine. So they port as uniform ActivatedEffect + MemoryBody
        //   (activated-half) and this EventBroadcast wire covers them; no separate scheduler-half broadcast is needed.
        //   SCHEDULER-HALF SYMMETRY: TriggerTimings.BroadcastTimings intentionally NOT updated — the emit stamps
        //   SourceEntityIdKey=host, so a SELF-scoped scheduler-half (IHeadlessCardEffect memory/DP) reactor would
        //   already be collected by the unified seed; only an ANYONE-scope scheduler-half reactor (0 today) would need
        //   BroadcastTimings. No inline scheduler hook exists (unlike OnEndAttack), so no CollectUnifiedSeed skip.
        EffectTiming.OnAddDigivolutionCards,
        // (F1-DEAD) OnEndBlockDesignation — an attack-pipeline event (after blockers are designated); by AS-IS name
        // semantics a board-wide event that a reactor on a DIFFERENT card (attacker/other) responds to, so it is
        // broadcast (0 AS-IS cards, so scope cannot be re-derived from real usage). NO emit source exists (no
        // headless block-designation pipeline) → fully LATENT/inert (no event ever derives this name). Placeholder
        // only; the block-designation pipe + emit is design item F1-DEAD-BLOCKDESIGNATION — do NOT invent it. 0
        // cards react. (Scheduler-half TriggerTimings.BroadcastTimings is intentionally NOT updated: there are 0
        // memory/DP reactors and no emit, so the asymmetry note in the F-1 design §4 — "update both halves" — is
        // moot until a real emit+card arrives at M1+.)
        EffectTiming.OnEndBlockDesignation,
    };
}
