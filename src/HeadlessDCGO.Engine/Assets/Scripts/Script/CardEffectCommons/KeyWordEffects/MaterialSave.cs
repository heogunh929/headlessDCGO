// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/MaterialSave.cs
// (P6 cluster2) 1:1 port. These functions are agnostic to what CanSelectCardCondition tests (the caller's
// closure) — the STOP for `CardSource.IsContainDigiXrosCondition` (no CardSource.cs edits in this cluster's
// scope; heavy DigiXros-condition scan) lives at the one call site in
// Script/CardEffectFactory/KeyWordEffects/MaterialSave.cs, design item RD-P6C2-4.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>CanActivateMaterialSave</c> (KeyWordEffects/MaterialSave.cs:10, verbatim).</summary>
    public static bool CanActivateMaterialSave(CardSource card, Func<CardSource, bool> canSelectCardCondition, Func<Permanent, bool> canSelectPermanentCondition)
    {
        if (!IsExistOnBattleArea(card))
        {
            return false;
        }

        Permanent permanent = ICardEffect.ResolvePermanentOfThisCard(card);
        return permanent.DigivolutionCards.Count(canSelectCardCondition) >= 1 && HasMatchConditionPermanent(card, canSelectPermanentCondition);
    }

    /// <summary>AS-IS <c>MaterialSaveProcess</c> (KeyWordEffects/MaterialSave.cs:27): owner selects 1 Tamer,
    /// then up to <paramref name="materialSaveCount"/> matching digivolution cards, placed under the selected
    /// Tamer.</summary>
    public static async Task MaterialSaveProcess(System.Collections.Hashtable hashtable, ICardEffect activateClass, CardSource card, Func<CardSource, bool> canSelectCardCondition, Func<Permanent, bool> canSelectPermanentCondition, int materialSaveCount)
    {
        if (!IsExistOnBattleArea(card))
        {
            return;
        }

        Permanent permanent = ICardEffect.ResolvePermanentOfThisCard(card);
        if (permanent.DigivolutionCards.Count(canSelectCardCondition) < 1)
        {
            return;
        }

        int maxPermanentCount = Math.Min(1, MatchConditionPermanentCount(card, canSelectPermanentCondition));
        var selectPermanentEffect = GManager.instance!.GetComponent<SelectPermanentEffect>();
        Permanent? selectedPermanent = null;
        selectPermanentEffect.SetUp(
            selectPlayer: card.Owner,
            canTargetCondition: (Headless.Services.HeadlessEntityId id) => canSelectPermanentCondition(PermanentOf(card, id)),
            canTargetCondition_ByPreSelecetedList: null,
            canEndSelectCondition: null,
            maxCount: maxPermanentCount,
            canNoSelect: true,
            canEndNotMax: false,
            selectPermanentCoroutine: (Permanent p) => { selectedPermanent = p; return Task.CompletedTask; },
            afterSelectPermanentCoroutine: null,
            mode: SelectPermanentEffect.Mode.Custom,
            cardEffect: activateClass);
        selectPermanentEffect.SetUpCustomMessage(customMessageArray: customPermanentMessageArrayTemplate(customText: "that will get a digivolution card", maxCount: 1, CanSelectDigimon: false, CanSelectTamer: true));
        await selectPermanentEffect.Activate().ConfigureAwait(false);

        if (selectedPermanent is null)
        {
            return;
        }

        int maxCardCount = Math.Min(materialSaveCount, permanent.DigivolutionCards.Count(canSelectCardCondition));
        var selectedCards = new List<CardSource>();
        var selectCardEffect = GManager.instance!.GetComponent<SelectCardEffect>();
        selectCardEffect.SetUp(
            canTargetCondition: canSelectCardCondition,
            canTargetCondition_ByPreSelecetedList: null,
            canEndSelectCondition: cardSources => !HasNoElement(cardSources),
            canNoSelect: () => true,
            selectCardCoroutine: (CardSource source) => { selectedCards.Add(source); return Task.CompletedTask; },
            afterSelectCardCoroutine: null,
            message: "Select cards to place on bottom of digivolution cards\n(cards will be placed so that cards with lower numbers are on top).",
            maxCount: maxCardCount,
            canEndNotMax: false,
            isShowOpponent: true,
            mode: SelectCardEffect.Mode.Custom,
            root: SelectCardEffect.Root.Custom,
            customRootCardList: permanent.DigivolutionCards.ToList(),
            canLookReverseCard: true,
            selectPlayer: card.Owner,
            cardEffect: activateClass);
        selectCardEffect.SetNotShowCard();
        await selectCardEffect.Activate().ConfigureAwait(false);

        if (selectedCards.Count >= 1)
        {
            await selectedPermanent.AddDigivolutionCardsBottom(selectedCards, activateClass?.EffectSourceCard?.InstanceId).ConfigureAwait(false);
        }
    }
}
