// Mirrored from DCGO/Assets/Scripts/Script/MemoryObject.cs (213 lines).
// NO-OP PRESENTATION SEAM for substrate root S1 (phase A1). The original is the memory-gauge widget: a row of
// MemoryTab positions (-10..10), the DOTween slide of the current-memory marker, and the "prediction line"
// preview. Verified: it only READS `GManager.instance.turnStateMachine.gameContext.Memory` and
// `GManager.instance.You.PlayerID` — it never writes game state; `oldMemory` is display bookkeeping for the
// tween. Mirror logic (CardController.cs:3662 anchor "AS-IS :801 OffMemoryPredictionLine()") had these call
// sites deleted; this file re-creates the type so they can be restored.
//
// SIGNATURE CHANGES (rule 4 — no Unity types):
//   - `IEnumerator SetMemory()`          -> `Task SetMemory()`                (coroutine translation)
//   - `MemoryTab.public GameObject tabObject` -> `public object? tabObject`
//   - `MemoryTab.public GameObject Light`     -> `public object? Light`, always null: the original returns
//     `tabObject.transform.GetChild(childCount-2).gameObject` and already returns null when `tabObject` is
//     null / has fewer than 2 children (MemoryObject.cs:190-204) — headless `tabObject` is always null, so
//     null is the original's own absent-display-object answer (rule 2).
//
// OMITTED MEMBERS (all private / Unity-serialized in the original):
//   - `CurrentMemoryObject` (GameObject), `memoryPredictionLine` (MemoryPredictionLine — no mirror type), and
//     `oldMemory`.
//   - `AssignMemoryTab()` is PUBLIC in the original and IS mirrored below; it assigns each tab its Memory value
//     from the seat orientation, which is pure display layout.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

/// <summary>Headless no-op stand-in for the AS-IS <c>MemoryObject</c> memory-gauge widget. Member names, order
/// and parameter lists mirror DCGO/Assets/Scripts/Script/MemoryObject.cs; all behaviour is stripped.</summary>
public class MemoryObject
{
    /// <summary>AS-IS <c>public List&lt;MemoryTab&gt; memoryTabs</c> (MemoryObject.cs:11) — the 21 gauge
    /// positions. Empty headless (they are populated from the Unity scene).</summary>
    public List<MemoryTab> memoryTabs = new List<MemoryTab>();

    /// <summary>AS-IS <c>Init()</c> (MemoryObject.cs:16) — assigns tab values and inits every tab/the
    /// prediction line.</summary>
    public void Init()
    {
    }

    /// <summary>AS-IS <c>AssignMemoryTab()</c> (MemoryObject.cs:27) — numbers the tabs -10..10 (or reversed for
    /// the non-master seat).</summary>
    public void AssignMemoryTab()
    {
    }

    /// <summary>AS-IS <c>IEnumerator SetMemory()</c> (MemoryObject.cs:45) — lights the tabs between the old and
    /// new memory value and tweens the marker onto the new tab.</summary>
    public Task SetMemory() => Task.CompletedTask;

    /// <summary>AS-IS <c>ShowMemoryPredictionLine(int)</c> (MemoryObject.cs:139) — draws the preview line from
    /// the current memory tab to the clamped (-10..10) next value.</summary>
    public void ShowMemoryPredictionLine(int nextMemory)
    {
    }

    /// <summary>AS-IS <c>OffMemoryPredictionLine()</c> (MemoryObject.cs:178) — hides the preview line.</summary>
    public void OffMemoryPredictionLine()
    {
    }
}

/// <summary>Headless no-op stand-in for the AS-IS <c>[Serializable] MemoryTab</c> (MemoryObject.cs:185) — one
/// position on the memory gauge.</summary>
public class MemoryTab
{
    /// <summary>AS-IS <c>public int Memory { get; set; }</c> (MemoryObject.cs:187) — the memory value this tab
    /// represents. Real state (a plain auto-property in the original too).</summary>
    public int Memory { get; set; }

    /// <summary>AS-IS <c>public GameObject tabObject</c> (MemoryObject.cs:188). Always null headless.</summary>
    public object? tabObject;

    /// <summary>AS-IS <c>public GameObject Light { get; }</c> (MemoryObject.cs:190) — the highlight child of
    /// <see cref="tabObject"/>. Always null headless (see file header).</summary>
    public object? Light => null;

    /// <summary>AS-IS <c>Init()</c> (MemoryObject.cs:206) — hides the highlight.</summary>
    public void Init()
    {
    }
}
