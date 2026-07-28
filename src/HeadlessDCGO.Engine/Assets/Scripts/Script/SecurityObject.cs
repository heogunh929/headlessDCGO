// Mirrored from DCGO/Assets/Scripts/Script/SecurityObject.cs (181 lines).
// NO-OP PRESENTATION SEAM for substrate root S1 (phase A1). The original is the per-player security-stack
// widget (count text, life-card images, face-up icon, click collider, the "Security Attack / Direct Attack"
// banner). Verified: no body mutates game state — every one only writes UI text/sprites/SetActive, and
// CheckFaceupSecurity merely mirrors `changedPlayer.SecurityCards.Count(c => !c.IsFlipped) > 0` onto an icon.
//
// SIGNATURE CHANGES (rule 4 — no Unity types):
//   - `public Text SecurityText`            -> `public object? SecurityText`
//   - `public GameObject faceupIcon`        -> `public object? faceupIcon`
//   - `public List<Image> LifeCards`        -> `public List<object?> LifeCards`
//   - `public GameObject Collider`          -> `public object? Collider`
//   - `AddClickTarget(UnityAction)`         -> `AddClickTarget(Action)`  (UnityEngine.Events.UnityAction has
//                                              System.Action as its exact neutral equivalent)
//   These four fields are live UI handles in the original; headless they are always null (rule 2).
//
// OMITTED MEMBERS:
//   - `public SecurityBreakGlass securityBreakGlass` (SecurityObject.cs:26): no `SecurityBreakGlass` mirror
//     type exists and it is not in this phase's file list. MultipleSkills.cs:27/237/290/329 anchors
//     (`securityBreakGlass.IsBlueGlass / SetActive / ShowBlueMatarial`) therefore still cannot be restored —
//     reported, not invented here.
//   - `public DropArea securityAttackDropArea` (SecurityObject.cs:29): no `DropArea` mirror type (drag/drop is
//     pure Unity input plumbing).
//   - private serialized fields `player`, `ShowSecurityAttackObject`, `ShowSecurityAttackText`,
//     `ShowSecurityAttackImage`, `_securityIconImage`, `OnClickAction`, and `private async void Start()`
//     (sprite loading + the GManager.OnSecurityStackChanged subscription).
//
// BEHAVIOUR NOTE (verified): AS-IS `Start()` subscribes `GManager.OnSecurityStackChanged += CheckFaceupSecurity`
// and `OnDestroy()` unsubscribes. This seam subscribes to nothing, so `CheckFaceupSecurity` is only reachable
// by a direct restored call.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

/// <summary>Headless no-op stand-in for the AS-IS <c>SecurityObject</c> security-stack widget. Member names,
/// order and parameter lists mirror DCGO/Assets/Scripts/Script/SecurityObject.cs; all behaviour is stripped.
/// </summary>
public class SecurityObject
{
    /// <summary>AS-IS <c>public Text SecurityText</c> (SecurityObject.cs:14) — the stack-count label. Always
    /// null headless.</summary>
    public object? SecurityText;

    /// <summary>AS-IS <c>public GameObject faceupIcon</c> (SecurityObject.cs:18). Always null headless.</summary>
    public object? faceupIcon;

    /// <summary>AS-IS <c>public List&lt;Image&gt; LifeCards</c> (SecurityObject.cs:20) — the card-back images.
    /// Empty headless.</summary>
    public List<object?> LifeCards = new List<object?>();

    /// <summary>AS-IS <c>public GameObject Collider</c> (SecurityObject.cs:23) — the click hit-box.</summary>
    public object? Collider;

    /// <summary>AS-IS <c>OffShowSecurityAttackObject()</c> (SecurityObject.cs:73).</summary>
    public void OffShowSecurityAttackObject()
    {
    }

    /// <summary>AS-IS <c>SetSecurityAttackObject()</c> (SecurityObject.cs:81) — shows the banner reading
    /// "Security Attack" or "Direct Attack" depending on <c>player.SecurityCards.Count</c>.</summary>
    public void SetSecurityAttackObject()
    {
    }

    /// <summary>AS-IS <c>SetSecurityOutline(bool)</c> (SecurityObject.cs:105) — banner outline alpha.</summary>
    public void SetSecurityOutline(bool isSelected)
    {
    }

    /// <summary>AS-IS <c>SetSecurity(Player)</c> (SecurityObject.cs:126) — writes the count text and toggles one
    /// card-back image per security card.</summary>
    public void SetSecurity(Player player)
    {
    }

    /// <summary>AS-IS <c>RemoveClickTarget()</c> (SecurityObject.cs:144) — disables the collider and drops the
    /// stored callback.</summary>
    public void RemoveClickTarget()
    {
    }

    /// <summary>AS-IS <c>AddClickTarget(UnityAction)</c> (SecurityObject.cs:153) — enables the collider and
    /// stores the callback.</summary>
    public void AddClickTarget(Action OnClickAction)
    {
    }

    /// <summary>AS-IS <c>OnClick()</c> (SecurityObject.cs:162) — invokes the stored callback, then
    /// <c>RemoveClickTarget()</c>. Headless there is no stored callback (see <see cref="AddClickTarget"/>), so
    /// nothing fires.</summary>
    public void OnClick()
    {
    }

    /// <summary>AS-IS <c>CheckFaceupSecurity(Player)</c> (SecurityObject.cs:168) — the
    /// <c>GManager.OnSecurityStackChanged</c> handler; lights the face-up icon.</summary>
    public void CheckFaceupSecurity(Player changedPlayer)
    {
    }

    /// <summary>AS-IS <c>OnDestroy()</c> (SecurityObject.cs:176) — public in the original; unsubscribes from
    /// <c>GManager.OnSecurityStackChanged</c>.</summary>
    public void OnDestroy()
    {
    }
}
