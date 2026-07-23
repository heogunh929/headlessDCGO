// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Scapegoat.cs
// (P6 cluster2) 1:1 port; the AS-IS single-arg `HasMatchConditionPermanent(predicate)`/
// `MatchConditionPermanentCount(predicate)` (global scan) map onto the mirror's (card, predicate) scoped
// overload — `permanent.TopCard` supplies the scope context (any live card works: the scan itself iterates
// both players' battle areas off the engine context, not off the card's ownership).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>CanActivateScapegoat</c> (KeyWordEffects/Scapegoat.cs:10, verbatim).</summary>
    public static bool CanActivateScapegoat(Permanent permanent, Func<Permanent, bool> permanentCondition) =>
        IsPermanentExistsOnBattleArea(permanent) && permanent.TopCard is not null &&
        HasMatchConditionPermanent(permanent.TopCard, permanentCondition);

    /// <summary>AS-IS <c>ScapegoatProcess</c> (KeyWordEffects/Scapegoat.cs:25): owner selects 1 matching
    /// Digimon to delete instead of this one; when that substitute is ACTUALLY deleted, cancel THIS Digimon's
    /// pending deletion (AS-IS <c>SelectPermanentCoroutine</c>'s <c>SuccessProcess</c>:
    /// <c>permanent.willBeRemoveField = false; HideDeleteEffect();</c>). (C-Del 3c-2a) restructured 1:1 with
    /// AS-IS — the substitute's delete runs INSIDE the per-selected coroutine (awaited by Activate in selection
    /// order), and the AS-IS trailing <c>willBeRemoveField = false</c> is RESTORED — survival is owned by the
    /// AS-IS PRE cut-in window (the sink opens it, 3b), not the retired
    /// <see cref="Headless.Runtime.DeletionReplacementGate"/>: the sweep's survivor-fix reads this Digimon's
    /// cleared flag to spare it. <c>HideDeleteEffect()</c> = UI (stripped, established convention).</summary>
    public static async Task ScapegoatProcess(ICardEffect activateClass, Permanent permanent, Func<Permanent, bool> canSelectPermanentCondition)
    {
        if (permanent?.TopCard is null || !HasMatchConditionPermanent(permanent.TopCard, canSelectPermanentCondition))
        {
            return;
        }

        var selectPermanentEffect = GManager.instance!.GetComponent<SelectPermanentEffect>();
        selectPermanentEffect.SetUp(
            selectPlayer: permanent.TopCard.Owner,
            canTargetCondition: canSelectPermanentCondition,
            canTargetCondition_ByPreSelecetedList: null,
            canEndSelectCondition: null,
            maxCount: 1,
            canNoSelect: false,
            canEndNotMax: false,
            selectPermanentCoroutine: async (Permanent selectedSubstitute) =>
            {
                await DeletePeremanentAndProcessAccordingToResult(
                    targetPermanents: new System.Collections.Generic.List<Permanent> { selectedSubstitute },
                    activateClass: activateClass,
                    successProcess: _ => { permanent.willBeRemoveField = false; return Task.CompletedTask; },
                    failureProcess: null).ConfigureAwait(false);
            },
            afterSelectPermanentCoroutine: null,
            mode: SelectPermanentEffect.Mode.Custom,
            cardEffect: activateClass);
        selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to delete.", "The opponent is selecting 1 Digimon to delete.");
        await selectPermanentEffect.Activate().ConfigureAwait(false);
    }
}
