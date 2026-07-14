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
    /// deletion (AS-IS <c>willBeRemoveField = false; HideDeleteEffect();</c>, no mirror field; the mirror's
    /// live "does Decoy save an ally" behaviour is owned by <see cref="Headless.Runtime.DeletionReplacementGate"/>,
    /// same posture as <see cref="EvadeProcess"/>) — so only the real state action (deleting this Digimon) is
    /// performed here.</summary>
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
                    selectPermanentCoroutine: (Permanent _) => Task.CompletedTask,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);
                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to prevent deletion.", "The opponent is selecting 1 Digimon to prevent deletion.");
                await selectPermanentEffect.Activate().ConfigureAwait(false);
            },
            failureProcess: null).ConfigureAwait(false);
    }
}
