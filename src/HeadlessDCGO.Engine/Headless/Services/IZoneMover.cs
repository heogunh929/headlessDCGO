namespace HeadlessDCGO.Engine.Headless.Services;

using HeadlessDCGO.Engine.Headless.Runtime;

public interface IZoneMover
{
    IReadOnlyList<GameEvent> Events { get; }

    Task<ZoneMoveResult> MoveAsync(ZoneMoveRequest request, CancellationToken cancellationToken = default);

    Task AddToHandAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, CancellationToken cancellationToken = default);

    Task AddToTrashAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, CancellationToken cancellationToken = default);

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
        CancellationToken cancellationToken = default);

    Task<HeadlessEntityId?> HatchDigitamaAsync(
        HeadlessPlayerId playerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HeadlessEntityId>> MoveBreedingToBattleAsync(
        HeadlessPlayerId playerId,
        int count = 1,
        CancellationToken cancellationToken = default);

    Task ShuffleAsync(HeadlessPlayerId playerId, CancellationToken cancellationToken = default);
}
