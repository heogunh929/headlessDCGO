namespace HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace with real legal-action generation once turn/rule flow is ported.
public interface IHeadlessLegalActionSeeder
{
    void SetLegalActions(IEnumerable<LegalAction> legalActions);
}
