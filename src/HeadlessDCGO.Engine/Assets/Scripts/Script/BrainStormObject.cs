// Mirrored from DCGO/Assets/Scripts/Script/BrainStormObject.cs (130 lines).
// NO-OP PRESENTATION SEAM for substrate root S1 (phase A1). The original is the per-player "brainstorm" strip
// of face-up HandCard widgets shown while a card is resolving; it holds no rule state (every body only toggles
// GameObjects / sets sprites / rotates transforms). Mirror logic (CardController.cs:3664 anchor "AS-IS
// :803-809") had those call sites deleted; this file re-creates the type so they can be restored.
//
// SIGNATURE CHANGES (rule 4 / missing presentation types):
//   - `IEnumerator Init()`                        -> `Task Init()`                        (coroutine translation)
//   - `IEnumerator BrainStormCoroutine(CardSource)` -> `Task BrainStormCoroutine(CardSource)`
//   - `IEnumerator CloseBrainstrorm(CardSource)`  -> `Task CloseBrainstrorm(CardSource)`  (AS-IS spelling kept)
//
// OMITTED MEMBERS:
//   - `public List<HandCard> BrainStormHandCards` (BrainStormObject.cs:14): the mirror has NO `HandCard` type
//     (src/.../Script/HandCard.cs is a comment-only stub), and HandCard is not in this phase's file list.
//     Restoring the AS-IS `foreach (HandCard handCard in ...BrainStormHandCards)` loop therefore needs a
//     HandCard seam first — reported, not invented here.
//   - `Update()` and `RotationCards()`: private, Unity frame-loop only (they re-rotate the widgets when
//     ContinuousController.reverseOpponentsCards is set).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

/// <summary>Headless no-op stand-in for the AS-IS <c>BrainStormObject</c> hand-display strip. Member names,
/// order and parameter lists mirror DCGO/Assets/Scripts/Script/BrainStormObject.cs; all behaviour is stripped.
/// </summary>
public class BrainStormObject
{
    /// <summary>AS-IS <c>public Player player</c> (BrainStormObject.cs:11) — the seat this strip belongs to.
    /// Kept as plain state (it is only read to name the widgets and to check <c>player.isYou</c>).</summary>
    public Player? player;

    /// <summary>AS-IS <c>IEnumerator Init()</c> (BrainStormObject.cs:16) — activates then re-hides every
    /// HandCard widget for one frame to force layout.</summary>
    public Task Init() => Task.CompletedTask;

    /// <summary>AS-IS <c>IEnumerator BrainStormCoroutine(CardSource)</c> (BrainStormObject.cs:35) — shows the
    /// resolving card in the next free slot (image, orange outline, skill-name strip off).</summary>
    public Task BrainStormCoroutine(CardSource cardSource) => Task.CompletedTask;

    /// <summary>AS-IS <c>void EndBrainStorm()</c> (BrainStormObject.cs:68) — hides every slot.</summary>
    public void EndBrainStorm()
    {
    }

    /// <summary>AS-IS <c>IEnumerator CloseBrainstrorm(CardSource)</c> (BrainStormObject.cs:76, spelling as in
    /// the original) — hides the slot holding that card.</summary>
    public Task CloseBrainstrorm(CardSource cardSource) => Task.CompletedTask;
}
