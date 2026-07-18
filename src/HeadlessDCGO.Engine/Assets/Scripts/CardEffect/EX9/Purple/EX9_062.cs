// Source: DCGO/Assets/Scripts/CardEffect/EX9/Purple/EX9_062.cs (1:1 mirror) — "SkullGreymon".
//
// S6 롱테일 트랜치 — 감사 시절 ⚠STOP-예상(Assembly 레벨변경)이었으나 표면 실존 확인: ChangeCardLevelForAssemblyClass
// 는 미러 CardEffects/ChangeCardLevelForAssemblyClass.cs로 1:1 존재(SetUpChangeCardLevelForAssemblyClass 시그니처
// 동일).
//   * [None] — AddSelfDigivolutionRequirementStaticEffect(Lv.4 [DM] 위 코스트3, `IsLevel4`(프로퍼티)→
//     `IsLevel(4)`(메서드) 인라인, symbol_map_guide §2.4 정본).
//   * [None] — ChangeCardLevelForAssemblyClass("Kimeramon 어셈블리에서도 Lv.4 취급").
//   * [On Play]/[When Digivolving] — 둘 다 AS-IS 그대로 EffectTiming.OnEnterFieldAnyone 버킷(다른 CanUse
//     게이트로 구분, BT12_044/BT9_013 정본 관례) — 뒷면 진화원 수만큼 덱 최상단 트래시 → [DM] 디지몬 1장을
//     트래시에서 손패로(선택).
//   * [On Deletion] x2([On Deletion]+ESS 상속) — 트래시의 Lv.4 이하 [DM] 디지몬 1장 무상 플레이.
//
// 치환(substrate translations only): IEnumerator→async Task, StartCoroutine(X)→await X;
// `card.PermanentOfThisCard().DigivolutionCards.Filter(x=>x.IsFlipped).Count` →
// `ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count(x=>x.IsFlipped)`(System.Linq, 미러
// DigivolutionCards=IReadOnlyList<CardSource>); `new IAddTrashCardsFromLibraryTop(trashCount, card.Owner,
// activateClass)` → `new IAddTrashCardsFromLibraryTop(card.Context, card.Owner, trashCount, activateClass)`
// (Context 선두 삽입, MIG3-3a); `GManager.instance.GetComponent<Effects>().ShowCardEffect2(...)` = UI(스트립).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX9.Purple;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX9_062 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution Requirement
        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent) =>
                targetPermanent.TopCard.IsLevel(4) && targetPermanent.TopCard.EqualsTraits("DM");

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 3, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region Alternative Assembly Level
        if (timing == EffectTiming.None)
        {
            ChangeCardLevelForAssemblyClass changeCardLevelForAssemblyClass = new ChangeCardLevelForAssemblyClass();
            changeCardLevelForAssemblyClass.SetUpICardEffect("This card is also treated as level 4 for [Kimeramon]'s assembly.", CanUseCondition, card);
            changeCardLevelForAssemblyClass.SetUpChangeCardLevelForAssemblyClass(changeCardLevel: ChangeCardLevel);

            cardEffects.Add(changeCardLevelForAssemblyClass);

            bool CanUseCondition(Hashtable hashtable) => true;

            List<int> ChangeCardLevel(CardSource cardSource, List<int> level)
            {
                if (cardSource == card)
                {
                    level.Add(4);
                }

                return level;
            }
        }
        #endregion

        #region On Play
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash cards from deck, then return 1 [DM] digimon from trash to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription() =>
                "[On Play] For each of this Digimon's face-down digivolution cards, trash the top card of your deck. Then, you may return 1 [DM] trait Digimon card from your trash to the hand.";

            bool CanUseCondition(Hashtable hashtable) => CardEffectCommons.CanTriggerOnPlay(hashtable, card);

            bool CanActivateCondition(Hashtable hashtable) => CardEffectCommons.IsExistOnBattleAreaDigimon(card);

            bool CanSelectCardCondition(CardSource cardSource) => cardSource.IsDigimon && cardSource.EqualsTraits("DM");

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                int trashCount = ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count(x => x.IsFlipped);
                if (trashCount >= 1)
                {
                    IAddTrashCardsFromLibraryTop addTrashCard = new IAddTrashCardsFromLibraryTop(card.Context, card.Owner, trashCount, activateClass);
                    addTrashCard.SetNotShowCards();
                    await addTrashCard.AddTrashCardsFromLibraryTop();
                    // AS-IS `GManager.instance.GetComponent<Effects>().ShowCardEffect2(...)` = UI (stripped).
                }

                if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                {
                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => true,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 [DM] digimon to add to hand",
                        maxCount: 1,
                        canEndNotMax: true,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.AddHand,
                        root: SelectCardEffect.Root.Trash,
                        customRootCardList: null,
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    selectCardEffect.SetUpCustomMessage("Select 1 [DM] digimon to add to hand", "The opponent is selecting 1 digimon to add to hand.");
                    await selectCardEffect.Activate();
                }
            }
        }
        #endregion

        #region When Digivolving
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash cards from deck, then return 1 [DM] digimon from trash to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription() =>
                "[When Digivolving] For each of this Digimon's face-down digivolution cards, trash the top card of your deck. Then, you may return 1 [DM] trait Digimon card from your trash to the hand.";

            bool CanUseCondition(Hashtable hashtable) => CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);

            bool CanActivateCondition(Hashtable hashtable) => CardEffectCommons.IsExistOnBattleAreaDigimon(card);

            bool CanSelectCardCondition(CardSource cardSource) => cardSource.IsDigimon && cardSource.EqualsTraits("DM");

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                int trashCount = ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count(x => x.IsFlipped);
                if (trashCount >= 1)
                {
                    IAddTrashCardsFromLibraryTop addTrashCard = new IAddTrashCardsFromLibraryTop(card.Context, card.Owner, trashCount, activateClass);
                    addTrashCard.SetNotShowCards();
                    await addTrashCard.AddTrashCardsFromLibraryTop();
                    // AS-IS `GManager.instance.GetComponent<Effects>().ShowCardEffect2(...)` = UI (stripped).
                }

                if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                {
                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => true,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 [DM] digimon to add to hand",
                        maxCount: 1,
                        canEndNotMax: true,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.AddHand,
                        root: SelectCardEffect.Root.Trash,
                        customRootCardList: null,
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    selectCardEffect.SetUpCustomMessage("Select 1 [DM] digimon to add to hand", "The opponent is selecting 1 digimon to add to hand.");
                    await selectCardEffect.Activate();
                }
            }
        }
        #endregion

        #region On Deletion
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 level 4 or less [DM] Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription() => "[On Deletion] you may play 1 level 4 or lower [DM] trait Digimon card from your trash without paying the cost.";

            bool CanUseCondition(Hashtable hashtable) => CardEffectCommons.CanTriggerOnDeletion(hashtable, card);

            bool CanActivateCondition(Hashtable hashtable) => CardEffectCommons.CanActivateOnDeletion(hashtable, card);

            bool CanSelectCardCondition(CardSource cardSource) =>
                cardSource.IsDigimon && cardSource.Level <= 4 && cardSource.EqualsTraits("DM")
                && CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass);

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                {
                    CardSource? selectedCard = null;
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOwnersCardCountInTrash(card, CanSelectCardCondition));
                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCard = cardSource;
                        return Task.CompletedTask;
                    }

                    selectCardEffect.SetUp(
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 [DM] digimon to play",
                        maxCount: maxCount,
                        canEndNotMax: true,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.Trash,
                        customRootCardList: null,
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    selectCardEffect.SetUpCustomMessage("Select 1 [DM] digimon to play", "The opponent is selecting 1 digimon to play.");
                    await selectCardEffect.Activate();

                    if (selectedCard != null)
                    {
                        await CardEffectCommons.PlayPermanentCards(cardSources: new List<CardSource> { selectedCard }, activateClass: activateClass, payCost: false, isTapped: false, root: SelectCardEffect.Root.Trash, activateETB: true);
                    }
                }
            }
        }
        #endregion

        #region ESS
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 level 4 or less [DM] Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription() => "[On Deletion] you may play 1 level 4 or lower [DM] trait Digimon card from your trash without paying the cost.";

            bool CanUseCondition(Hashtable hashtable) => CardEffectCommons.CanTriggerOnDeletion(hashtable, card);

            bool CanActivateCondition(Hashtable hashtable) => CardEffectCommons.CanActivateOnDeletion(hashtable, card);

            bool CanSelectCardCondition(CardSource cardSource) =>
                cardSource.IsDigimon && cardSource.Level <= 4 && cardSource.EqualsTraits("DM")
                && CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass);

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                {
                    CardSource? selectedCard = null;
                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCard = cardSource;
                        return Task.CompletedTask;
                    }

                    selectCardEffect.SetUp(
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 [DM] digimon to play",
                        maxCount: 1,
                        canEndNotMax: true,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.Trash,
                        customRootCardList: null,
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    selectCardEffect.SetUpCustomMessage("Select 1 [DM] digimon to play", "The opponent is selecting 1 digimon to play.");
                    await selectCardEffect.Activate();

                    if (selectedCard != null)
                    {
                        await CardEffectCommons.PlayPermanentCards(cardSources: new List<CardSource> { selectedCard }, activateClass: activateClass, payCost: false, isTapped: false, root: SelectCardEffect.Root.Trash, activateETB: true);
                    }
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
