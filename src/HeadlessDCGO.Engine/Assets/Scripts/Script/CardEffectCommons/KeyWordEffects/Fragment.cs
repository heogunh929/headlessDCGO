// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Fragment.cs
// (P6 cluster2) 1:1 port. CanActivateFragment is a genuine STOP (Permanent.CanBeDestroyedBySkill immunity
// scan has no mirror — same gap as Decoy, design item RD-P6C2-3). FragmentProcess IS fully portable: its
// own selection (SelectCardEffect) + trash of the resulting list maps directly onto the verified substrate
// primitive DigivolutionStackHelpers.TrashSpecificSourcesAsync, whose own doc header cites
// "AS-IS ITrashDigivolutionCards(permanent, selectedCards, …)" as its exact behavioural model.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Runtime;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>CanActivateFragment</c> (KeyWordEffects/Fragment.cs:7). STOP — see file header,
    /// design item RD-P6C2-3.</summary>
    public static bool CanActivateFragment(Permanent permanent, int trashValue, ICardEffect activateClass)
    {
        throw new NotSupportedException(
            "CanActivateFragment: AS-IS Permanent.CanBeDestroyedBySkill has no mirror immunity-scan primitive " +
            "yet — design item RD-P6C2-3, docs/audit/rebuild_p6_cluster2_notes.md.");
    }

    /// <summary>AS-IS <c>FragmentProcess</c> (KeyWordEffects/Fragment.cs:22): select exactly
    /// <paramref name="trashValue"/> of this Digimon's digivolution cards and trash them (cancelling the
    /// pending deletion — same posture as <see cref="EvadeProcess"/>/<see cref="DecoyProcess"/>: the AS-IS
    /// <c>willBeRemoveField</c>/<c>HideDeleteEffect</c> bookkeeping has no mirror and the live "does this save
    /// the Digimon" answer belongs to <see cref="Headless.Runtime.DeletionReplacementGate"/>).</summary>
    public static async Task FragmentProcess(ICardEffect activateClass, Permanent permanent, int trashValue)
    {
        if (permanent?.TopCard is null || permanent.DigivolutionCards.Count < trashValue)
        {
            return;
        }

        var selectedCards = new List<CardSource>();
        var selectCardEffect = GManager.instance!.GetComponent<SelectCardEffect>();
        selectCardEffect.SetUp(
            canTargetCondition: (CardSource _) => true,
            canTargetCondition_ByPreSelecetedList: null,
            canEndSelectCondition: null,
            canNoSelect: () => false,
            selectCardCoroutine: (CardSource source) => { selectedCards.Add(source); return Task.CompletedTask; },
            afterSelectCardCoroutine: null,
            message: "Select digivolution cards to trash.",
            maxCount: 3,
            canEndNotMax: false,
            isShowOpponent: true,
            mode: SelectCardEffect.Mode.Custom,
            root: SelectCardEffect.Root.Custom,
            customRootCardList: permanent.DigivolutionCards.ToList(),
            canLookReverseCard: false,
            selectPlayer: activateClass.EffectSourceCard!.Owner,
            cardEffect: activateClass);
        selectCardEffect.SetUseFaceDown();
        selectCardEffect.SetUpCustomMessage("Select digivolution cards to trash.", "The opponent is selecting digivolution cards to trash.");

        await selectCardEffect.Activate().ConfigureAwait(false);

        if (selectedCards.Count != trashValue)
        {
            return;
        }

        Headless.Bridge.EngineContext context = permanent.TopCard.Context;
        await DigivolutionStackHelpers.TrashSpecificSourcesAsync(
            context.CardInstanceRepository, context.ZoneMover, permanent.InstanceId,
            selectedCards.Select(cs => cs.InstanceId).ToList(),
            gameEventQueue: context.GameEventQueue,
            effectRegistry: context.EffectRegistry,
            context: context,
            causingEffectSourceId: activateClass.EffectSourceCard.InstanceId).ConfigureAwait(false);
    }
}
