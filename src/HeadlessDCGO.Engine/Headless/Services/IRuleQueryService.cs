namespace HeadlessDCGO.Engine.Headless.Services;

// TODO: Query rules and legal state without UI or scene dependencies.
public interface IRuleQueryService
{
    IReadOnlyList<LegalAction> GetLegalActions(HeadlessPlayerId playerId);

    bool CanPayCost(HeadlessPlayerId playerId, HeadlessEntityId sourceId, int cost);

    bool IsTerminal();
}
