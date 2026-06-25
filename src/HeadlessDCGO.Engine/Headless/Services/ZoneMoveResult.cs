namespace HeadlessDCGO.Engine.Headless.Services;

using HeadlessDCGO.Engine.Headless.Runtime;

public sealed record ZoneMoveResult(
    ZoneMoveRequest Request,
    GameEvent Event,
    IReadOnlyList<HeadlessEntityId> SourceZoneCards,
    IReadOnlyList<HeadlessEntityId> DestinationZoneCards);
