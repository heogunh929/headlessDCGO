// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangeCardDP.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `ChangeSecurityDigimonCardDPPlayerEffect` (CardEffectCommons.cs:1456).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.ChangeSecurityDigimonCardDPPlayerEffect(...)</c> (GiveEffect/GiveEffectToPlayer/ChangeCardDP.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task ChangeSecurityDigimonCardDPPlayerEffect(Func<CardSource, bool> cardCondition, int changeValue, EffectDuration effectDuration, ICardEffect activateClass)
    {
        ChangeSecurityDigimonCardDPPlayerEffect(cardCondition, changeValue, effectDuration, activateClass?.EffectSourceCard);
        await Task.CompletedTask;
    }
}
