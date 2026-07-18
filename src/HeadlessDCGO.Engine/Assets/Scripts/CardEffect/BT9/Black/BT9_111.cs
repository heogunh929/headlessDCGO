// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// PILOT-S1 카드 — BT9_111 (Digimon / Black)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT9/Black/BT9_111.cs (189 lines, no region markers, 3 timing 블록)
//    * Alternate Digivolution :15-23 (timing == None — AddSelfDigivolutionRequirementStaticEffect)
//    * [When Digivolving]     :25-65 (timing == OnEnterFieldAnyone — 상대 최고코스트 디지몬 전멸)
//    * [End of Your Turn]     :67-185 (timing == OnEndTurn — 진화원 반환→메모리 획득)
//
// ② 프리미티브 매핑:
//    * P:AddDigivolutionRequirementClass(AddSelfDigivolutionRequirementStaticEffect) — [Alphamon]+진화원
//      [Ouryumon] 위 코스트 3 (AS-IS :17-22)
//    * P:DestroyPermanentsClass — [When Digivolving] 상대 최고코스트 디지몬 전멸 (AS-IS :62-64)
//    * P:ReturnToLibraryBottomDigivolutionCardsClass + Player.AddMemory — [End of Your Turn] 진화원 반환→
//      메모리 획득 (AS-IS :177-181)
//
// ③ 배선 관례 근거:
//    * [When Digivolving] → OnEnterFieldAnyone + CanTriggerWhenDigivolving(hashtable, card) 게이트(AS-IS :44
//      그대로).
//    * [End of Your Turn] → OnEndTurn, Once Per Turn(SetHashString), isOptional true — AS-IS 그대로.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`,
//      `yield return StartCoroutine(X)`→`await X` (BT17_026 idiom); lone `yield return null`→Task.CompletedTask
//      (BT8_092 idiom).
//    * `card.Owner.Enemy.GetBattleAreaDigimons()` → `new Player(card.Context, card.Owner).Enemy!
//      .GetBattleAreaDigimons()` (BT18_042/EX7_014 idiom).
//    * `card.PermanentOfThisCard()`(가변 필요) → `ICardEffect.ResolvePermanentOfThisCard(card)` (BT8_092 관례).
//    * `new ReturnToLibraryBottomDigivolutionCardsClass(permanent, cardSources, hashtable)` → 미러 ctor
//      (CardController.cs:1467)는 (permanent, cardSources, causeEffectSourceId, cardEffect) — BT5_086의
//      ITrashDigivolutionCards idiom과 동일하게 `activateClass.EffectSourceCard?.InstanceId` + `activateClass` 삽입.
//    * `card.Owner.AddMemory(count, activateClass)` — HeadlessPlayerId 확장 메서드(Player.cs:876) AS-IS
//      시그니처 그대로 사용 가능.
//    * `selectedPermanent.DigivolutionCards`(IReadOnlyList) → `customRootCardList`에는 `.ToList()` (BT17_026 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.Black;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT9_111 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.CardNames.Contains("Alphamon") && targetPermanent.DigivolutionCards.Count((cardSource) => cardSource.CardNames.Contains("Ouryumon")) >= 1;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 3, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete opponent's all Digimon with the highest play cost", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Delete all of your opponent's Digimon with the highest play cost.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsMaxCost(permanent, new Player(card.Context, card.Owner).Enemy!.PlayerId, true);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, PermanentCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<Permanent> destroyTargetPermanents = new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons().Filter(PermanentCondition);
                await new DestroyPermanentsClass(destroyTargetPermanents, CardEffectCommons.CardEffectHashtable(activateClass)).Destroy();
            }
        }

        if (timing == EffectTiming.OnEndTurn)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return digivolution cards from this Digimon to deck to gain Memory", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("Memory+_BT9_111");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[End of Your Turn][Once Per Turn] You may return up to 7 non-Digi-Egg cards with [X Antibody] in their traits from this Digimon's digivolution cards to the bottom of your deck in any order; if you do, gain 1 memory for each card returned.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (!cardSource.IsDigiEgg)
                {
                    if (cardSource.HasXAntibodyTraits)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count(CanSelectCardCondition) >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                    List<CardSource> selectedCards = new List<CardSource>();

                    if (ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count(CanSelectCardCondition) == 1)
                    {
                        selectedCards = ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Where(CanSelectCardCondition).ToList();
                    }

                    else
                    {
                        int maxCount = Math.Min(7, selectedPermanent.DigivolutionCards.Count(CanSelectCardCondition));

                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                                    canTargetCondition: CanSelectCardCondition,
                                    canTargetCondition_ByPreSelecetedList: null,
                                    canEndSelectCondition: CanEndSelectCondition,
                                    canNoSelect: () => false,
                                    selectCardCoroutine: SelectCardCoroutine,
                                    afterSelectCardCoroutine: null,
                                    message: "Select cards that will return to the bottom of deck\n(cards will be placed back to the bottom of the deck so that cards with lower numbers are on top).",
                                    maxCount: maxCount,
                                    canEndNotMax: true,
                                    isShowOpponent: true,
                                    mode: SelectCardEffect.Mode.Custom,
                                    root: SelectCardEffect.Root.Custom,
                                    customRootCardList: selectedPermanent.DigivolutionCards.ToList(),
                                    canLookReverseCard: true,
                                    selectPlayer: card.Owner,
                                    cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage("Select digivolution cards to return to the bottom of deck.", "The opponent is selecting digivolution cards to return to the bottom of deck.");
                        selectCardEffect.SetNotShowCard();
                        await selectCardEffect.Activate();

                        bool CanEndSelectCondition(List<CardSource> cardSources)
                        {
                            if (CardEffectCommons.HasNoElement(cardSources))
                            {
                                return false;
                            }

                            return true;
                        }

                        Task SelectCardCoroutine(CardSource cardSource)
                        {
                            selectedCards.Add(cardSource);
                            return Task.CompletedTask;
                        }
                    }

                    if (selectedCards.Count >= 1)
                    {
                        await new ReturnToLibraryBottomDigivolutionCardsClass(
                            ICardEffect.ResolvePermanentOfThisCard(card), selectedCards,
                            activateClass.EffectSourceCard?.InstanceId, activateClass).ReturnToLibraryBottomDigivolutionCards();

                        await card.Owner.AddMemory(selectedCards.Count, activateClass);
                    }
                }
            }
        }

        return cardEffects;
    }
}
