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
    /// Digimon to delete instead of this one (the AS-IS <c>willBeRemoveField</c>/<c>HideDeleteEffect</c>
    /// cancellation of THIS permanent's pending deletion has no mirror field — same posture as
    /// <see cref="EvadeProcess"/> — the mirror's live "does Scapegoat save this Digimon" answer belongs to
    /// <see cref="Headless.Runtime.DeletionReplacementGate"/>; this performs only the real state action, the
    /// substitute's deletion).</summary>
    public static async Task ScapegoatProcess(ICardEffect activateClass, Permanent permanent, Func<Permanent, bool> canSelectPermanentCondition)
    {
        if (permanent?.TopCard is null || !HasMatchConditionPermanent(permanent.TopCard, canSelectPermanentCondition))
        {
            return;
        }

        var selectPermanentEffect = GManager.instance!.GetComponent<SelectPermanentEffect>();
        Permanent? selected = null;
        selectPermanentEffect.SetUp(
            selectPlayer: permanent.TopCard.Owner,
            canTargetCondition: (Headless.Services.HeadlessEntityId id) => canSelectPermanentCondition(PermanentOf(permanent.TopCard, id)),
            canTargetCondition_ByPreSelecetedList: null,
            canEndSelectCondition: null,
            maxCount: 1,
            canNoSelect: false,
            canEndNotMax: false,
            selectPermanentCoroutine: (Permanent p) => { selected = p; return Task.CompletedTask; },
            afterSelectPermanentCoroutine: null,
            mode: SelectPermanentEffect.Mode.Custom,
            cardEffect: activateClass);
        selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to delete.", "The opponent is selecting 1 Digimon to delete.");
        await selectPermanentEffect.Activate().ConfigureAwait(false);

        if (selected is null)
        {
            return;
        }

        await DeletePeremanentAndProcessAccordingToResult(
            targetPermanents: new System.Collections.Generic.List<Permanent> { selected },
            activateClass: activateClass,
            successProcess: _ => Task.CompletedTask,
            failureProcess: null).ConfigureAwait(false);
    }
}
