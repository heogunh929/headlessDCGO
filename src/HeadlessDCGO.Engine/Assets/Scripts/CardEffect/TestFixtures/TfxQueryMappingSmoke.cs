// SMOKE. Verifies the §9 cheatsheet query mappings actually compile (real headless names/forms).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public static class TfxQueryMappingSmoke
{
    public static bool VerifyMappings(CardSource card)
    {
        // 키워드 보유
        bool hasReboot = ContinuousKeywordGate.HasKeyword(card.Context, card.InstanceId, "Reboot");
        // 트래시 수 (자기 / 상대)
        var zones = (IZoneStateReader)card.Context.ZoneMover;
        int trashCount = zones.GetCards(card.Owner, ChoiceZone.Trash).Count;
        // 색 보유
        bool isRed = card.HasCardColor("Red");
        // 소유 + 타입
        bool ownedDigimon = card.Owner == card.Owner && card.IsDigimon;
        // permanent id
        var pid = card.InstanceId;
        return hasReboot || trashCount >= 0 || isRed || ownedDigimon || !pid.IsEmpty;
    }
}
