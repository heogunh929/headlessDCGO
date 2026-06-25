namespace HeadlessDCGO.Engine.Headless.Services;

using HeadlessDCGO.Engine.Headless.Choices;

// TODO: Replace with read-only access to final Player/Card zone state.
public interface IZoneStateReader
{
    IReadOnlyList<HeadlessEntityId> GetCards(HeadlessPlayerId playerId, ChoiceZone zone);

    IReadOnlyDictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>> Snapshot(HeadlessPlayerId playerId);
}
