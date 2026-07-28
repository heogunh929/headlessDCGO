// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/TamerBecomesDigimonThatCanNotDigivolve.cs
// (G-clean-2 grant rehousing) AS-IS-signature `Task` overload of the [Tamer becomes a Digimon that can't
// digivolve] grant, AS-IS 1:1.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public static partial class CardEffectCommons
{
    /// <summary>(G-clean-2 grant rehousing) AS-IS <c>CardEffectCommons.BecomeDigimonThatCantDigivolve</c>
    /// (GiveEffect/GiveEffectToPermanent/TamerBecomesDigimonThatCanNotDigivolve.cs:10), 1:1: three timed continuous
    /// grants stored in the target permanent's <c>None</c> duration bucket via <see cref="AddEffectToPermanent"/> —
    /// (1) a <see cref="CardEffectFactory.TreatAsDigimonStaticEffect"/> (read by the central
    /// <c>ContinuousKeywordGate.IsDigimon</c>/<c>HasTreatAsDigimon</c> <c>ITreatAsDigimonEffect</c> scan), (2) a
    /// <see cref="CardEffectFactory.ChangeBaseDPStaticEffect"/> base-DP override, and (3) a
    /// <see cref="CardEffectFactory.CanNotDigivolveStaticEffect"/> (read by the <c>ICanNotDigivolveEffect</c> scan).
    /// Replaces the invented substrate that routed TreatAsDigimon through the <c>GainKeywordToPermanent</c> funnel and
    /// the base-DP/restriction through invented helpers. ADAPTATION: the AS-IS terminal <c>CreateBuffEffect</c> VFX
    /// and <c>SetActivatedTime</c> (a Unity presentation-ordering timestamp with no headless substrate) are dropped.</summary>
    public static async Task BecomeDigimonThatCantDigivolve(Permanent targetPermanent, int DP, EffectDuration effectDuration, ICardEffect activateClass)
    {
        if (targetPermanent == null) return;
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) return;
        if (activateClass == null) return;
        if (activateClass.EffectSourceCard == null) return;
        if (DP < 0) return;

        CardSource card = activateClass.EffectSourceCard;

        bool PermanentCondition(Permanent permanent) => permanent == targetPermanent;

        bool CanUseCondition() => IsPermanentExistsOnBattleArea(targetPermanent);

        // treat as Digimon
        TreatAsDigimonClass treatAsDigimonClass = CardEffectFactory.TreatAsDigimonStaticEffect(
            permanentCondition: PermanentCondition,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition);

        AddEffectToPermanent(
            targetPermanent: targetPermanent,
            effectDuration: effectDuration,
            card: card,
            cardEffect: treatAsDigimonClass,
            timing: EffectTiming.None);

        // change origin DP
        ChangeBaseDPClass changeBaseDPClass = CardEffectFactory.ChangeBaseDPStaticEffect(
            targetPermanent: targetPermanent,
            changeValue: DP,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition);

        AddEffectToPermanent(
            targetPermanent: targetPermanent,
            effectDuration: effectDuration,
            card: card,
            cardEffect: changeBaseDPClass,
            timing: EffectTiming.None);

        // can't Digivolve
        CanNotDigivolveClass canNotEvolveClass = CardEffectFactory.CanNotDigivolveStaticEffect(
            permanentCondition: PermanentCondition,
            cardCondition: (cardSource) => true,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            effectName: "Can't digivolve");

        AddEffectToPermanent(
            targetPermanent: targetPermanent,
            effectDuration: effectDuration,
            card: card,
            cardEffect: canNotEvolveClass,
            timing: EffectTiming.None);

        await Task.CompletedTask;
    }
}
