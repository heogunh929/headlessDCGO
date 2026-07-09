using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (DPBoost) AS-IS Permanent.Boosts / AddBoost / RemoveBoost + the DP-getter tail `foreach (DPBoost b in Boosts) DP += b.DP`
// (Permanent.cs:653-699). Per-card named additive boosts, upserted by id, folded AFTER the NotIsUpDown/set group,
// before the final >=0 clamp.

HeadlessPlayerId P1 = new(1);
HeadlessEntityId Host = new("p1:HOST");

EngineContext context = EngineContext.CreateDefault(randomSeed: 3);
context.CardInstanceRepository.Upsert(new CardInstanceRecord(Host, new HeadlessEntityId("def"), P1));

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}
int Dp() => ContinuousDpGate.ResolveDp(context, Host, baseDp: 3000);

Check(Dp() == 3000, "no boost = base DP");

DpBoostHelpers.AddBoost(context.CardInstanceRepository, Host, "b1", 1000);
Check(Dp() == 4000, "a boost folds into DP");

DpBoostHelpers.AddBoost(context.CardInstanceRepository, Host, "b1", 2000);
Check(Dp() == 5000, "same-id AddBoost REPLACES (AS-IS upsert by ID), not accumulates");

DpBoostHelpers.AddBoost(context.CardInstanceRepository, Host, "b2", 500);
Check(Dp() == 5500, "distinct-id boosts sum (3000 + 2000 + 500)");

// AS-IS folds Boosts AFTER the NotIsUpDown/set group, so a "DP becomes 4000" set does NOT overwrite the boosts.
RegisterFixedDp(context, Host, P1, 4000);
Check(Dp() == 6500, "boosts fold AFTER a set-DP (set 4000 + 2000 + 500)");

DpBoostHelpers.RemoveBoost(context.CardInstanceRepository, Host, "b1");
Check(Dp() == 4500, "RemoveBoost removes just that boost (set 4000 + 500)");

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall DPBoost checks passed.");

void RegisterFixedDp(EngineContext ctx, HeadlessEntityId cardId, HeadlessPlayerId owner, int fixedDp)
{
    var effectContext = new EffectContext(
        owner, owner, new HeadlessEntityId($"src:fixeddp:{cardId.Value}"),
        triggerEntityId: null, targetEntityIds: new[] { cardId },
        values: new Dictionary<string, object?>(StringComparer.Ordinal) { ["fixedDp"] = fixedDp });
    ctx.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId($"fixeddp:{cardId.Value}:{fixedDp}"), owner, "Continuous", effectContext),
        keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }));
}
