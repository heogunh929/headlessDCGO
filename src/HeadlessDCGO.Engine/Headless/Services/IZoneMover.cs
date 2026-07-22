namespace HeadlessDCGO.Engine.Headless.Services;

using HeadlessDCGO.Engine.Headless.Runtime;

public interface IZoneMover
{
    IReadOnlyList<GameEvent> Events { get; }

    Task<ZoneMoveResult> MoveAsync(ZoneMoveRequest request, CancellationToken cancellationToken = default);

    // (F1-Tier1 OnAddHand) Move a card into a player's hand, optionally stamping the ADD-HAND batch id (so an
    // effect's multi-card hand add collapses to a single OnAddHand reactor fire) and the CAUSE effect source id
    // (the AS-IS {"CardEffect", …} — mirrors CardEffect != null so a non-effect add does not fire the
    // CanTriggerOnHandAdded gate). A bare add (no ids) is a non-effect hand add and derives no live OnAddHand.
    Task AddToHandAsync(
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        long? addHandBatchId = null,
        HeadlessEntityId? causeEffectId = null,
        CancellationToken cancellationToken = default);

    Task AddToTrashAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, CancellationToken cancellationToken = default);

    // (F1-Tier1 OnDiscard*) Trash a card while PRESERVING its source zone on the CardMoved event, so a
    // Hand->Trash / Library->Trash move derives OnDiscardHand / OnDiscardLibrary (TriggerTimingMap). Unlike
    // AddToTrashAsync (which uses From=None / RemoveFromAllZones and therefore derives no source-zone timing),
    // this looks up the card's current zone and moves From that zone. It stamps the DISCARD batch id (so the
    // activated bridge collapses one effect's multi-card discard to a single reactor fire) and the CAUSE effect
    // id (the AS-IS {"CardEffect", …} — mirrors CardEffect != null so a non-effect trash does not fire the
    // OnDiscardHand/Security gate). isRevealTrash stamps the AS-IS IsBeingRevealed mirror (F1 reveal-remainder):
    // a card trashed as reveal remainder is IsBeingRevealed==true at the trash moment, so its OnDiscardLibrary
    // broadcast is filtered out by the !IsBeingRevealed gate (WhenDiscardLibrary.cs:23-26).
    Task TrashCardAsync(
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        long? discardBatchId = null,
        HeadlessEntityId? causeEffectId = null,
        bool isRevealTrash = false,
        CancellationToken cancellationToken = default);

    // N-3: toTop defaults true to match the original AddSecurityCard(toTop: true) — a returned/recovered
    // card goes to the TOP of security (index 0, the next card checked), not the bottom.
    // (F1-Tier1 OnAddSecurity P2-1) addSecurityBatchId stamps the shared-counter per-card id on the ->Security
    // CardMoved so the OnAddSecurity activated bridge sequences per-card triggers in add order; null = unstamped.
    Task AddToSecurityAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, bool faceUp, bool toTop = true, long? addSecurityBatchId = null, CancellationToken cancellationToken = default);

    Task MoveToDeckTopAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, CancellationToken cancellationToken = default);

    Task MoveToDeckBottomAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, CancellationToken cancellationToken = default);

    // (F1-Tier1 OnAddHand) Draw the top `count` library cards into the player's hand. The optional add-hand batch
    // id + cause effect id are stamped on every Library->Hand CardMoved (as AddToHandAsync) so an effect-driven
    // multi-card draw fires the OnAddHand reactor ONCE and passes the CardEffect != null gate; a bare draw
    // (turn / mulligan / setup) carries neither and never fires OnAddHand (AS-IS cardEffect=null).
    Task<IReadOnlyList<HeadlessEntityId>> DrawAsync(
        HeadlessPlayerId playerId,
        int count,
        long? addHandBatchId = null,
        HeadlessEntityId? causeEffectId = null,
        CancellationToken cancellationToken = default);

    // (F1-Tier1 OnAddSecurity P2-1) batchIdFactory is invoked ONCE PER moved card to allocate a fresh
    // shared-counter add-security id (OnAddSecurity is per-card, not collapsed), stamped on each ->Security
    // CardMoved so the activated bridge sequences the N recovered cards in ascending add order. Null = unstamped.
    Task<IReadOnlyList<HeadlessEntityId>> AddSecurityFromLibraryAsync(
        HeadlessPlayerId playerId,
        int count,
        bool faceUp = false,
        Func<long?>? batchIdFactory = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HeadlessEntityId>> TrashSecurityAsync(
        HeadlessPlayerId playerId,
        int count,
        bool fromTop = true,
        long? securityLossBatchId = null,
        HeadlessEntityId? causeEffectId = null,
        CancellationToken cancellationToken = default);

    Task<HeadlessEntityId?> HatchDigitamaAsync(
        HeadlessPlayerId playerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HeadlessEntityId>> MoveBreedingToBattleAsync(
        HeadlessPlayerId playerId,
        int count = 1,
        CancellationToken cancellationToken = default);

    Task ShuffleAsync(HeadlessPlayerId playerId, CancellationToken cancellationToken = default);

    // (BT1_087) Shuffle the player's SECURITY stack (AS-IS RandomUtility.ShuffledDeckCards on SecurityCards).
    // Distinct from ShuffleAsync (Library-only) — the deterministic RNG shuffles the Security zone in place.
    Task ShuffleSecurityAsync(HeadlessPlayerId playerId, CancellationToken cancellationToken = default);

    // (BT13_033) Shuffle the player's HAND in place (AS-IS `HandCards = RandomUtility.ShuffledDeckCards(HandCards)`).
    // Sibling of ShuffleSecurityAsync/ShuffleAsync scoped to ChoiceZone.Hand — the deterministic RNG reorders the
    // hand zone list in place, which IS the AS-IS in-place reassignment (Player.HandCards is a live zone view).
    Task ShuffleHandAsync(HeadlessPlayerId playerId, CancellationToken cancellationToken = default);
}
