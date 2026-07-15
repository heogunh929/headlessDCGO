// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Decoy.cs
// (P6 cluster2) 1:1 port. (R2-B) CanActivateDecoy STOP RESOLVED — R1-d ported
// `Permanent.CanBeDestroyedBySkill` (Permanent.cs:3035, AS-IS Permanent.cs:3309-3365), so the AS-IS body is
// now portable verbatim (design item RD-P6C2-3 closed). DecoyProcess was already fully ported.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>CanActivateDecoy</c> (KeyWordEffects/Decoy.cs:10, verbatim — R2-B, RD-P6C2-3 resolved
    /// via R1-d <c>Permanent.CanBeDestroyedBySkill</c>): this Digimon is on the battle area and is not currently
    /// immune to effect-deletion.</summary>
    public static bool CanActivateDecoy(Permanent permanent, ICardEffect activateClass)
    {
        if (IsPermanentExistsOnBattleArea(permanent))
        {
            if (permanent.CanBeDestroyedBySkill(activateClass))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>DecoyProcess</c> (KeyWordEffects/Decoy.cs:25): delete this Digimon, then (if
    /// deleted) the owner may redirect a matching ally to take its place — cancel that ally's pending
    /// deletion (AS-IS <c>SelectPermanentCoroutine</c>: <c>permanent.willBeRemoveField = false;
    /// HideDeleteEffect();</c>). (C-Del 3c-2a) the AS-IS trailing <c>willBeRemoveField = false</c> is now
    /// RESTORED — survival is owned by the AS-IS PRE cut-in window (the sink opens it, 3b), not the retired
    /// <see cref="Headless.Runtime.DeletionReplacementGate"/>: clearing the selected ally's flag is what the
    /// sweep's survivor-fix reads to spare it. <c>HideDeleteEffect()</c> = UI (stripped, established
    /// convention).</summary>
    public static async Task DecoyProcess(ICardEffect activateClass, Permanent permanent, Func<Permanent, bool> canSelectPermanentCondition)
    {
        if (permanent?.TopCard is null)
        {
            return;
        }

        await DeletePeremanentAndProcessAccordingToResult(
            targetPermanents: new List<Permanent> { permanent },
            activateClass: activateClass,
            successProcess: async permanents =>
            {
                if (!HasMatchConditionPermanent(permanent.TopCard!, canSelectPermanentCondition))
                {
                    return;
                }

                int maxCount = Math.Min(1, MatchConditionPermanentCount(permanent.TopCard!, canSelectPermanentCondition));
                var selectPermanentEffect = GManager.instance!.GetComponent<SelectPermanentEffect>();
                selectPermanentEffect.SetUp(
                    selectPlayer: permanent.TopCard!.Owner,
                    canTargetCondition: (Headless.Services.HeadlessEntityId id) => canSelectPermanentCondition(PermanentOf(permanent.TopCard!, id)),
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: maxCount,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: (Permanent selectedAlly) => { selectedAlly.willBeRemoveField = false; return Task.CompletedTask; },
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);
                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to prevent deletion.", "The opponent is selecting 1 Digimon to prevent deletion.");
                await selectPermanentEffect.Activate().ConfigureAwait(false);
            },
            failureProcess: null).ConfigureAwait(false);
    }
}
