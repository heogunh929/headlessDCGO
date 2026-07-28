// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/ChangeSAttack.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overloads (the AS-IS file defines two
// `ChangeDigimonSAttack` overloads at lines 10 and 62; the substrate already collapsed both into one method
// with `activateAnimation`/`hashstring` optional — CardEffectCommons.cs:1812).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.ChangeDigimonSAttack(...)</c> (GiveEffect/GiveEffectToPermanent/ChangeSAttack.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task ChangeDigimonSAttack(Permanent targetPermanent, int changeValue, EffectDuration effectDuration, ICardEffect activateClass)
    {
        ChangeDigimonSAttack(targetPermanent, changeValue, effectDuration, activateClass?.EffectSourceCard, activateClass: activateClass);
        await Task.CompletedTask;
    }

    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.ChangeDigimonSAttack(...)</c> (GiveEffect/GiveEffectToPermanent/ChangeSAttack.cs:62, the <c>activateAnimation</c>/<c>hashstring</c> overload) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task ChangeDigimonSAttack(Permanent targetPermanent, int changeValue, EffectDuration effectDuration, ICardEffect activateClass, bool activateAnimation, string hashstring = null)
    {
        ChangeDigimonSAttack(targetPermanent, changeValue, effectDuration, activateClass?.EffectSourceCard, activateAnimation, hashstring, activateClass);
        await Task.CompletedTask;
    }
}
