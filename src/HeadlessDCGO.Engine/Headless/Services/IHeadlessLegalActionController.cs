namespace HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace with rule-generated legal action updates after AS-IS rule flow is ported.
public interface IHeadlessLegalActionController : IHeadlessLegalActionSeeder
{
    void AddLegalActions(IEnumerable<LegalAction> legalActions);

    bool RemoveLegalAction(HeadlessEntityId actionId);

    void ClearLegalActions();
}
