namespace HeadlessDCGO.Engine.Headless.Services;

using HeadlessDCGO.Engine.Headless.Runtime;

public interface IZoneMover
{
    IReadOnlyList<GameEvent> Events { get; }

    Task<ZoneMoveResult> MoveAsync(ZoneMoveRequest request, CancellationToken cancellationToken = default);

    Task AddToHandAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, CancellationToken cancellationToken = default);

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
    Task AddToSecurityAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, bool faceUp, bool toTop = true, CancellationToken cancellationToken = default);

    Task MoveToDeckTopAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, CancellationToken cancellationToken = default);

    Task MoveToDeckBottomAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HeadlessEntityId>> DrawAsync(HeadlessPlayerId playerId, int count, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HeadlessEntityId>> AddSecurityFromLibraryAsync(
        HeadlessPlayerId playerId,
        int count,
        bool faceUp = false,
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
}
