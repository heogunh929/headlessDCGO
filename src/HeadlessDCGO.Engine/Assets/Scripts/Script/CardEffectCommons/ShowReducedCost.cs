// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/ShowReducedCost.cs
// (EFFECT-MODEL REBUILD / bridge W1, rule 5) UI-ONLY classification (see
// docs/audit/mutation_helper_bridge_map.md "UI-ONLY (1)"): the AS-IS body only calls
// `GManager.instance.memoryObject.ShowMemoryPredictionLine(...)` (a cost-preview overlay) then
// `WaitForSeconds(0.2f)` — no game-state mutation. AS-IS has no `activateClass`/`cardEffect` param at all
// (only `Hashtable hashtable`, used purely to fetch UI context), so this is a genuine no-op mirror: the
// symbol must exist because ported card verbatim bodies `await` it, but its behavior is correctly
// "does nothing."
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE, UI-ONLY) AS-IS <c>CardEffectCommons.ShowReducedCost(Hashtable)</c> (ShowReducedCost.cs:9) — no-op mirror; the AS-IS body is pure presentation (cost-preview overlay), no game-state mutation.</summary>
    public static async Task ShowReducedCost(Hashtable hashtable)
    {
        await Task.CompletedTask;
    }
}
