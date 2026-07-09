namespace HeadlessDCGO.Engine.Headless.Effects;

/// <summary>
/// Canonical trigger-timing vocabulary (W1). This is the contract between the engine — which emits
/// game events and derives timings via <see cref="TriggerTimingMap"/> — and ported card effects,
/// which register under these exact strings (<see cref="AutoProcessingTriggerCollector"/> matches by
/// ordinal string equality). Names mirror the original Unity <c>EffectTiming</c> enum so Phase 4
/// card porting is a 1:1 mapping. Add new constants here as more emission points are wired.
/// </summary>
public static class TriggerTimings
{
    // Play / field entry / exit.
    public const string OnPlay = "OnPlay";
    public const string OnEnterField = "OnEnterFieldAnyone";
    public const string OnLeaveField = "OnLeaveFieldAnyone";
    public const string WhenRemoveField = "WhenRemoveField";
    // (PRIM-P0-timing batch 4) The would-be-deleted replacement/prevention window (AS-IS
    // CardController.Destroy willBeRemoveField cut-in). A card registered here surfaces as a PRE option in the
    // existing DeletionReplacementTiming window; activating it prevents/replaces the deletion (ClearDeletion).
    public const string WhenPermanentWouldBeDeleted = "WhenPermanentWouldBeDeleted";

    // Deletion / return.
    public const string OnDeletion = "OnDestroyedAnyone";
    public const string OnReturnToHand = "WhenReturntoHandAnyone";
    public const string OnReturnToLibrary = "WhenReturntoLibraryAnyone";

    // Card-movement timings (F-6.5). Discards (zone→Trash from a non-field zone are distinct from
    // OnDeletion), returns out of the trash, and the field-leave synonyms. All derived from the
    // CardMoved zone transition.
    public const string OnDiscardHand = "OnDiscardHand";
    public const string OnDiscardSecurity = "OnDiscardSecurity";
    public const string OnDiscardLibrary = "OnDiscardLibrary";
    public const string OnReturnCardsToHandFromTrash = "OnReturnCardsToHandFromTrash";
    public const string OnReturnCardsToLibraryFromTrash = "OnReturnCardsToLibraryFromTrash";
    public const string OnPermanentReturnedToHand = "OnPermamemtReturnedToHand"; // (sic) original enum spelling
    public const string OnRemovedField = "OnRemovedField";

    // Hand / security movement.
    public const string OnAddToHand = "OnAddHand";
    public const string OnAddToSecurity = "OnAddSecurity";
    public const string OnLoseSecurity = "OnLoseSecurity";

    // Attack / counter / block / security check.
    public const string OnAttack = "OnUseAttack";
    // (G6-005) Fired alongside OnAttack when a Digimon declares an attack (subject = the attacker), the
    // window the original "[When Attacking] ..." effects (e.g. ST1_06) use.
    public const string OnAllyAttack = "OnAllyAttack";
    // (G7-006) Fired for each participant when a battle begins, before the DP comparison — the window for
    // "[When battling] / [On Start of Battle]" effects. (Other combat-detail timings — OnGetDamage,
    // OnAttackTargetChanged, OnEndBlockDesignation, OnDeclaration, OnUseDigiburst — follow this same
    // one-line emit pattern when a card group requires them.)
    public const string OnStartBattle = "OnStartBattle";
    // (PRIM-P0-timing) Fired when a Digimon declares an attack (subject = the attacker), the window the
    // original "[When Attacking]" attack-declaration effects use. Emitted alongside OnAttack/OnAllyAttack in
    // AttackPermanentAction. Distinct from OnAttack: the original enum separates OnUseAttack from OnDeclaration.
    public const string OnDeclaration = "OnDeclaration";
    // (PRIM-P0-timing batch 3b) The attack's defending target changed after declaration — raid retarget or
    // block redirect (AS-IS AttackProcess.SwitchDefender fires this for both). subject = the attacker.
    public const string OnAttackTargetChanged = "OnAttackTargetChanged";
    public const string OnCounter = "OnCounterTiming";
    public const string OnBlock = "OnBlockAnyone";
    public const string OnSecurityCheck = "OnSecurityCheck";

    // Turn boundaries.
    public const string OnStartTurn = "OnStartTurn";
    public const string OnEndTurn = "OnEndTurn";

    // Action / cost timings (F-6.6 / F-6.7). OnUseOption opens when an Option card is used. The pay-cost
    // windows wrap a card's play/digivolve/option cost payment (subject = the card being paid for).
    public const string OnUseOption = "OnUseOption";
    public const string BeforePayCost = "BeforePayCost";
    public const string AfterPayCost = "AfterPayCost";

    // Phase boundaries (F-6.2). OnStartMainPhase is the original's verified emit point (main-phase
    // entry). OnEndMainPhase / OnEndAttackPhase are declared in the original EffectTiming enum but are
    // not actively fired there; headless opens them when the main phase ends (the turn is handed over),
    // so cards bound to them have a firing site.
    public const string OnStartMainPhase = "OnStartMainPhase";
    public const string OnEndMainPhase = "OnEndMainPhase";
    public const string OnEndAttackPhase = "OnEndAttackPhase";
    // (PRIM-P0-timing batch 3b) End of a single attack (distinct from OnEndAttackPhase = the whole attack
    // step ending). Already collected by EndAttackTriggerHook (which keys on this same "OnEndAttack" string)
    // at AttackPipeline.AdvanceEndAttackAsync — this const just names it in the shared vocabulary.
    public const string OnEndAttack = "OnEndAttack";

    // Digivolution / draw.
    public const string WhenDigivolving = "WhenDigivolving";
    public const string OnDraw = "OnDraw";

    // Battle knock-out (F-6.3). Opens for each Digimon deleted by battle (subject = the knocked-out card).
    public const string OnKnockOut = "OnKnockOut";

    // Stack trashing (D-4 / F-6.5). Opens when a permanent's top card(s) are trashed off the stack
    // (de-digivolve, top-card removal). Mirrors the original EffectTiming.WhenTopCardTrashed.
    public const string WhenTopCardTrashed = "WhenTopCardTrashed";

    // (PRIM-P0-timing batch 3b) A Digimon's digivolution SOURCE (under) card is trashed by an effect
    // (AS-IS ITrashDigivolutionCards.TrashDigivolutionCards). DISTINCT from WhenTopCardTrashed (which is the
    // de-digivolve top-card path). subject = the Digimon losing sources; discardedCardIds metadata carries
    // the trashed source ids.
    public const string OnDigivolutionCardDiscarded = "OnDigivolutionCardDiscarded";

    // (AS-IS ReturnToLibraryBottomDigivolutionCardsClass, EffectTiming.OnDigivolutionCardReturnToDeckBottom).
    // Opens when a Digimon's digivolution (under-)cards are returned to the deck — DISTINCT from the discard
    // (trash) path above. subject = the Digimon losing sources; deckBottomCardIds metadata carries the returned ids.
    public const string OnDigivolutionCardReturnToDeckBottom = "OnDigivolutionCardReturnToDeckBottom";

    // Link (D-1 / F-6.9). WhenWouldLink opens before the link cost is paid (prevent-link window);
    // WhenLinked opens after a link card is attached; OnLinkCardDiscarded opens when a linked card is
    // trashed. Names mirror the original EffectTiming enum members.
    public const string WhenWouldLink = "WhenWouldLink";
    public const string WhenLinked = "WhenLinked";
    public const string OnLinkCardDiscarded = "OnLinkCardDiscarded";

    // Digivolution-source attach (F-6.4). Opens when digivolution cards are placed under a Digimon
    // (the digivolve attach, or an effect that adds sources) — scoped to the receiving card.
    public const string OnAddDigivolutionCards = "OnAddDigivolutionCards";

    // Face-up security increase (F-6.4). Opens when a card is added to a player's security face up,
    // raising the face-up security count. Global — bound effects self-gate.
    public const string OnFaceUpSecurityIncreased = "OnFaceUpSecurityIncreased";

    // Suspend state (CV-A4). Global "anyone tapped/untapped" windows — every bound effect self-gates.
    public const string OnTapped = "OnTappedAnyone";
    public const string OnUntapped = "OnUnTappedAnyone";

    // Promotion move (CV-A4). The original EffectTiming.OnMove fires specifically when a Digimon moves
    // out of the breeding (training) area onto the battle area — derived from that zone transition.
    public const string OnMove = "OnMove";

    // Battle resolution (CV-A4). OnEndBattle opens after the battle is resolved (deletions applied), so
    // "at the end of battle" effects fire. NOTE: the sibling OnStartBattle is NOT emitted yet — it must
    // resolve BEFORE the DP comparison (so "+DP at start of battle" applies), which needs a synchronous
    // mid-battle resolution window; tracked as a CV-A4 follow-up.
    public const string OnEndBattle = "OnEndBattle";

    /// <summary>
    /// (G12-003) Board-wide "Anyone" timings whose listeners must fire cross-card — the event is about
    /// ANY card on the board (own OR opponent), so an effect registered on a DIFFERENT card still
    /// receives the event's subject instead of being dropped by the per-card listener filter (the
    /// trigger gate then self-scopes on the subject). See
    /// <see cref="AutoProcessingTriggerCollector"/>.
    ///
    /// EXTENSION POINT — when a new board-wide "Anyone" timing needs cross-card listeners, add its
    /// constant here. This is an explicit allow-list, NOT the <c>Anyone</c>-suffix convention, on
    /// purpose: not every <c>...Anyone</c> timing is safe to broadcast. Some (e.g.
    /// <see cref="OnBlock"/> = "OnBlockAnyone") have effects that rely on the per-card filter for
    /// correctness rather than self-gating, so blanket-broadcasting them regresses behavior. Add a
    /// timing only after confirming its bound effects self-gate on the subject.
    /// </summary>
    private static readonly HashSet<string> BroadcastTimings = new(StringComparer.Ordinal)
    {
        OnDeletion, // "OnDestroyedAnyone" — ST3_01/04 react to an opponent's 0-DP deletion (G12-003).
        // (PRIM-P0-timing batch 3b) AS-IS fires these via global StackSkillInfos, and some reactors live on a
        // DIFFERENT card than the subject (e.g. an effect on the discarded source card itself; BT21_025 reacts
        // to another Digimon's attack-target change). Broadcast so cross-card listeners are collected; each
        // self-gates on the subject / discardedCardIds metadata.
        OnDigivolutionCardDiscarded,
        OnAttackTargetChanged,
        // (c-remediation) AS-IS fires OnDigivolutionCardReturnToDeckBottom via global StackSkillInfos; reactors
        // may live on other cards. Broadcast so cross-card listeners are collected; each self-gates on subject.
        OnDigivolutionCardReturnToDeckBottom,
    };

    /// <summary>True if <paramref name="timing"/> is a board-wide timing whose listeners fire
    /// cross-card. See <see cref="BroadcastTimings"/> for the allow-list and how to extend it.</summary>
    public static bool IsBroadcast(string? timing) =>
        timing is not null && BroadcastTimings.Contains(timing);
}
