// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Collision.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainCollision` (CardEffectCommons.cs:3421).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>(G-clean-2 grant rehousing) AS-IS <c>CardEffectCommons.GainCollision</c>
    /// (KeyWordEffects/Collision.cs:10), 1:1: build <c>PermanentEffectFactory.CollisionEffect</c>
    /// (card = <c>targetPermanent.TopCard</c>, AS-IS) and store it in the target permanent's
    /// <c>OnCounterTiming</c> duration bucket via <see cref="AddEffectToPermanent"/> — read by
    /// <see cref="Permanent.HasCollision"/>'s <c>ICollisionEffect</c> scan. Replaces the invented
    /// <c>GainKeywordToPermanent</c> funnel. ADAPTATION: the AS-IS terminal <c>CreateBuffEffect</c> VFX is
    /// dropped.</summary>
    public static async Task GainCollision(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
    {
        if (targetPermanent == null) return;
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) return;
        if (activateClass == null) return;
        if (activateClass.EffectSourceCard == null) return;

        CardSource card = targetPermanent.TopCard;

        ICardEffect collision = PermanentEffectFactory.CollisionEffect(targetPermanent, activateClass);

        AddEffectToPermanent(
            targetPermanent: targetPermanent, effectDuration: effectDuration, card: card,
            cardEffect: collision, timing: EffectTiming.OnCounterTiming);

        await Task.CompletedTask;
    }
}
