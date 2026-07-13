// Source: DCGO/Assets/Scripts/Script/CEntity_Base.cs
// (EFFECT-MODEL REBUILD) The AS-IS CEntity_Base.cs is the card-definition ScriptableObject; only the members
// ported effect code needs so far are mirrored here (currently the CardColor enum), grown as ports require.
// File lives at the AS-IS path Script/CEntity_Base.cs; namespace kept ...CardEffectCommons so existing
// references (the IChangeCardColorEffect / IChangeBaseCardColorEffect interfaces in CardEffectInterfaces.cs)
// are unaffected — namespace normalisation is a later, separate pass.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

/// <summary>1:1 mirror of AS-IS <c>CardColor</c> (CEntity_Base.cs:381) — the printed-colour enum the
/// <c>IChangeCardColorEffect</c>/<c>IChangeBaseCardColorEffect</c> marker interfaces transform. DESIGN ITEM
/// COLOR-MODEL-DUALITY: the mirror also carries a STRING colour model (<see cref="CardSource.CardColors"/>/
/// <see cref="CardSource.BaseCardColors"/> return <c>IReadOnlyList&lt;string&gt;</c>). The per-kind
/// <c>ChangeCardColorClass</c>/<c>ChangeBaseCardColorClass</c> port must reconcile this enum with those string
/// accessors — until then the two colour representations coexist.</summary>
public enum CardColor
{
    Red,
    Blue,
    Yellow,
    Green,
    White,
    Black,
    Purple,
    None,
}
