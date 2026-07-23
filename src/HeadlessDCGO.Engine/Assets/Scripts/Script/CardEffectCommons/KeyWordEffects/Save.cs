// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Save.cs
// (P6 cluster2) 1:1 port of the AS-IS Hashtable-based helpers (a DIFFERENT CardEffectResolveContext-based
// CanActivateSave/SaveProcess overload already lives in the monolith CardEffectCommons.cs for the newer
// kind-class DeletionReplacementGate consumer — see that overload's own header). ADAPTATION: AS-IS's
// single-arg HasMatchConditionPermanent/MatchConditionPermanentCount (global scan) need a CardSource for the
// mirror's scoped overload; AS-IS's own signature has none in scope at the CanActivateSave call site, so an
// explicit `card` parameter is threaded here and at the one call site
// (Script/CardEffectFactory/KeyWordEffects/Save.cs) — any live card works as the scan's context handle.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Services;

public static partial class CardEffectCommons
{
    /// <summary>(P6 cluster2, general helper — see file header) AS-IS <c>MatchConditionPermanentCount</c>
    /// (Func&lt;Permanent,bool&gt; overload, GameContextDeterminarion.cs:600, global scan): the count of
    /// battle-area (+ breeding, optionally) permanents across BOTH players matching <paramref name="condition"/>.
    /// This is the sole mirror overload of this name — the <c>Func&lt;Permanent,bool&gt;</c> shape the
    /// KeyWordEffects consumers (Save/Alliance/MaterialSave/…) need, defined once here (any file of this
    /// partial class may call it unqualified).</summary>
    public static int MatchConditionPermanentCount(CardSource card, Func<Permanent, bool> condition, bool isContainBreedingArea = false) =>
        EnumerateFieldPermanentViews(card, isContainBreedingArea).Count(condition);

    /// <summary>AS-IS <c>CanActivateSave</c> (KeyWordEffects/Save.cs:10, verbatim modulo the added
    /// <paramref name="card"/> scan-scope parameter — see file header).</summary>
    public static bool CanActivateSave(System.Collections.Hashtable hashtable, CardSource card, Func<Permanent, bool> canSelectPermanentCondition) =>
        IsTopCardInTrashOnDeletion(hashtable) && HasMatchConditionPermanent(card, canSelectPermanentCondition);

    /// <summary>AS-IS <c>SaveProcess</c> (KeyWordEffects/Save.cs:25): owner selects 1 matching permanent (a
    /// non-token Tamer on their own battle area) and this card goes under it as a digivolution card.</summary>
    public static async Task SaveProcess(System.Collections.Hashtable hashtable, ICardEffect activateClass, CardSource card, Func<Permanent, bool> canSelectPermanentCondition)
    {
        if (!CanActivateSave(hashtable, card, canSelectPermanentCondition))
        {
            return;
        }

        int maxCount = Math.Min(1, MatchConditionPermanentCount(card, canSelectPermanentCondition));
        var selectPermanentEffect = GManager.instance!.GetComponent<SelectPermanentEffect>();
        Permanent? selected = null;
        selectPermanentEffect.SetUp(
            selectPlayer: card.Owner,
            canTargetCondition: canSelectPermanentCondition,
            canTargetCondition_ByPreSelecetedList: null,
            canEndSelectCondition: null,
            maxCount: maxCount,
            canNoSelect: true,
            canEndNotMax: false,
            selectPermanentCoroutine: (Permanent p) => { selected = p; return Task.CompletedTask; },
            afterSelectPermanentCoroutine: null,
            mode: SelectPermanentEffect.Mode.Custom,
            cardEffect: activateClass);
        selectPermanentEffect.SetUpCustomMessage(customMessageArray: customPermanentMessageArrayTemplate(customText: "that will get a digivolution card", maxCount: 1, CanSelectDigimon: false, CanSelectTamer: true));
        await selectPermanentEffect.Activate().ConfigureAwait(false);

        if (selected is null)
        {
            return;
        }

        await selected.AddDigivolutionCardsBottom(new System.Collections.Generic.List<CardSource> { card }, activateClass?.EffectSourceCard?.InstanceId).ConfigureAwait(false);
    }
}
