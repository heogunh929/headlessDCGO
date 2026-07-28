// Mirrored from DCGO/Assets/Scripts/Script/PlayLog.cs (189 lines).
// NO-OP PRESENTATION SEAM for substrate root S1 (phase A1). The original is the on-screen scrolling battle-log
// panel (a MonoBehaviour driving TMP_Text/ScrollRect). Mirror logic had its `PlayLog.OnAddLog?.Invoke(...)`
// call sites deleted because no such type existed headless; this file re-creates the type so those statements
// can later be restored verbatim. Every body here is empty — nothing renders, nothing is stored.
//
// SIGNATURE CHANGES (rule 4 — no Unity types): none. The public surface of PlayLog uses only string/Action.
//
// OMITTED MEMBERS (all private in the original, all Unity/UI-internal):
//   - fields `_logText` (TMP_Text), `_scroll` (ScrollRect), `_logList`, `_maxLogCharacterLength`, `_first`
//   - `OnDestroy()`, `GetLogString()`, `SetUpPlayLogCoroutine()`, `AddLogStringCoroutine(string)`,
//     `AddLink(string)`, `ShowCard(string)`, `AllIndexesOf(string,string)`
//   AddLink/AllIndexesOf are pure string logic, but they exist only to build TMP `<link>`/`<color>` rich-text
//   markup for the panel, so they have no headless consumer.
//
// BEHAVIOUR NOTE (verified in the original): `Init()` (PlayLog.cs:107-117) subscribes `OnAddLog += AddLogString`
// and `OnLinkPressed += ShowCard`. This seam deliberately does NOT subscribe — with no text panel there is
// nothing for a handler to write to, so the static Actions stay null and `OnAddLog?.Invoke(...)` is a no-op.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

/// <summary>Headless no-op stand-in for the AS-IS <c>PlayLog</c> battle-log panel. Member names, order and
/// parameter lists mirror DCGO/Assets/Scripts/Script/PlayLog.cs; all behaviour is stripped.</summary>
public class PlayLog
{
    /// <summary>AS-IS <c>PlayLog.OnAddLog</c> (PlayLog.cs:17) — the static broadcast every log-emitting rule
    /// site fires. Never subscribed headless (see file header), so invoking it does nothing.</summary>
    public static Action<string>? OnAddLog;

    /// <summary>AS-IS <c>PlayLog.OnLinkPressed</c> (PlayLog.cs:18) — fired by the UI when a card link inside the
    /// log text is clicked. Never subscribed headless.</summary>
    public static Action<string>? OnLinkPressed;

    /// <summary>AS-IS <c>OnClickLiogButton()</c> (PlayLog.cs:44, sic — the original spelling) — toggles the
    /// panel. No panel headless.</summary>
    public void OnClickLiogButton()
    {
    }

    /// <summary>AS-IS <c>SetUpPlayLog()</c> (PlayLog.cs:57) — starts the show-panel coroutine.</summary>
    public void SetUpPlayLog()
    {
    }

    /// <summary>AS-IS <c>OffPlayLog()</c> (PlayLog.cs:87) — hides the panel (and plays the cancel SE).</summary>
    public void OffPlayLog()
    {
    }

    /// <summary>AS-IS <c>Init()</c> (PlayLog.cs:107) — clears the log text/list and wires the static Actions.
    /// </summary>
    public void Init()
    {
    }

    /// <summary>AS-IS <c>AddLogString(string)</c> (PlayLog.cs:119) — the <c>OnAddLog</c> handler; appends the
    /// ASCII-normalised line to the panel.</summary>
    public void AddLogString(string logText)
    {
    }
}
