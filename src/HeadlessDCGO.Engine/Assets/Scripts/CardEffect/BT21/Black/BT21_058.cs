// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S3 카드 — BT21_058 (Digimon / Black, Snatchmon)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT21/Black/BT21_058.cs (231 lines, 3 regions + shared)
//    * OP/WD Shared         :15-133 (리빌 3장 → [Vemmon] 텍스트 카드 1장 손패, 나머지 트래시 → 트래시의
//                                     [Vemmon] 최대 2장 진화원 최하단 배치)
//    * On Play              :137-149(AS-IS timing == OnEnterFieldAnyone + CanTriggerOnPlay)
//    * When Digivolving     :155-167(AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving —
//                                     **미러 방언 재배선**: WhenDigivolving 전용 키)
//    * All Turns Inherited  :172-225(OnDigivolutionCardReturnToDeckBottom — [Vemmon]이 덱바닥으로 반환될 때
//                                     상대 플레이코스트4- 디지몬 삭제, Once Per Turn)
//
// ② 프리미티브 매핑:
//    * P:SimplifiedRevealDeckTopCardsAndSelect, P:CanTriggerOnReturnToLibraryBottomDigivolutionCard
//
// ③ 배선 관례 근거:
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(trigger-wiring rule 3, §2.7) — On Play와
//      동일 OnEnterFieldAnyone에 CanUseCondition만으로 구분 등재하는 AS-IS 원본을 이중-키 금지 규칙에 맞춰 재배선.
//    * [All Turns] → OnDigivolutionCardReturnToDeckBottom(AS-IS 그대로) +
//      CanTriggerOnReturnToLibraryBottomDigivolutionCard(hashtable, cardCondition, card) Hashtable 오버로드.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)` → `await X`.
//    * `card.Owner.GetBattleAreaDigimons()` → HeadlessPlayerId 확장(§2.2 예외) 그대로.
//    * SelectPermanentEffect.SetUp의 canTargetCondition은 Permanent-술어(HasDigimonOnOwnerBattleArea /
//      CanSelectPermanentCondition)를 직접 받는다(§2.3, BT17_026 판례).
//    * `permanent.TopCard.HasPlayCost && permanent.TopCard.GetCostItself <= 4` — 둘 다 미러 실존(CardSource.cs)
//      그대로.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT21.Black;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT21_058 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region OP/WD Shared

        string SharedEffectName = "Reveal 3, add 1 [Vemmon] in text, trash rest, place up to 2 [Vemmon] from trash under 1 Digimon.";

        string SharedEffectDescription(string tag) => $"[{tag}] Reveal the top 3 cards of your deck. Add 1 card with [Vemmon] in its text among them to the hand. Trash the rest. Then, you may place up to 2 [Vemmon] from your trash as 1 of your Digimon's bottom digivolution cards.";

        bool HasVemmonInText(CardSource cardSource)
        {
            return cardSource.HasText("Vemmon");
        }

        bool IsVemmon(CardSource cardSource)
        {
            return cardSource.EqualsCardName("Vemmon");
        }

        bool HasDigimonOnOwnerBattleArea(Permanent permanent)
        {
            return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);
        }

        bool SharedCanActivateCondition(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
        }

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            await CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect(
                revealCount: 3,
                simplifiedSelectCardConditions:
                new SimplifiedSelectCardConditionClass[]
                {
                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition:HasVemmonInText,
                        message: "Select 1 card with [Vemmon] in text.",
                        mode: SelectCardEffect.Mode.AddHand,
                        maxCount: 1,
                        selectCardCoroutine: null),
                },
                remainingCardsPlace: RemainingCardsPlace.Trash,
                activateClass: activateClass,
                canNoSelect: false
            );

            if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, IsVemmon))
            {
                List<CardSource> selectedCardsFromTrash = new List<CardSource>();

                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();
                int maxCountFromTrash = Math.Min(2, CardEffectCommons.MatchConditionOwnersCardCountInTrash(card, IsVemmon));

                selectCardEffect.SetUp(
                    canTargetCondition: IsVemmon,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    canNoSelect: () => true,
                    selectCardCoroutine: SelectCardCoroutine,
                    afterSelectCardCoroutine: null,
                    message: "Select up to 2 cards to place on bottom of digivolution cards.",
                    maxCount: maxCountFromTrash,
                    canEndNotMax: true,
                    isShowOpponent: true,
                    mode: SelectCardEffect.Mode.Custom,
                    root: SelectCardEffect.Root.Trash,
                    customRootCardList: null,
                    canLookReverseCard: true,
                    selectPlayer: card.Owner,
                    cardEffect: activateClass);

                Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCardsFromTrash.Add(cardSource);
                    return Task.CompletedTask;
                }

                selectCardEffect.SetUpCustomMessage("Select up to 2 cards to place on bottom of digivolution cards.", "The opponent is selecting up to 2 cards to place on bottom of digivolution cards.");
                await selectCardEffect.Activate();

                Permanent selectedPermanent = null;

                if (selectedCardsFromTrash.Count != 0)
                {
                    if (card.Owner.GetBattleAreaDigimons().Count(HasDigimonOnOwnerBattleArea) > 1)
                    {
                        SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectPermanentEffect.SetUp(
                                selectPlayer: card.Owner,
                                canTargetCondition: HasDigimonOnOwnerBattleArea,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                maxCount: 1,
                                canNoSelect: true,
                                canEndNotMax: false,
                                selectPermanentCoroutine: SelectPermanentCoroutine,
                                afterSelectPermanentCoroutine: null,
                                mode: SelectPermanentEffect.Mode.Custom,
                                cardEffect: activateClass);

                        Task SelectPermanentCoroutine(Permanent permanent)
                        {
                            selectedPermanent = permanent;
                            return Task.CompletedTask;
                        }

                        selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get the digivolution card(s).", "The opponent is selecting 1 Digimon that will get the digivolution card(s).");
                        await selectPermanentEffect.Activate();
                    }

                    else selectedPermanent = card.Owner.GetBattleAreaDigimons().FirstOrDefault();
                    if (selectedPermanent != null) await selectedPermanent.AddDigivolutionCardsBottom(selectedCardsFromTrash, activateClass.EffectSourceCard?.InstanceId);
                }
            }
        }

        #endregion

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(SharedEffectName, CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, hash => SharedActivateCoroutine(hash, activateClass), -1, false, SharedEffectDescription("On Play"));
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card)
                    && CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }
        }

        #endregion

        #region When Digivolving

        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(SharedEffectName, CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, hash => SharedActivateCoroutine(hash, activateClass), -1, false, SharedEffectDescription("When Digivolving"));
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card)
                    && CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }
        }

        #endregion

        #region All Turns Inherited
        if (timing == EffectTiming.OnDigivolutionCardReturnToDeckBottom)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 opponent Digimon with play cost 4 or less", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("Delete_BT21_058");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[All Turns] [Once Per Turn] When any [Vemmon] are returned to the bottom of the deck from this Digimon's digivolution cards, delete 1 of your opponent's Digimon with a play cost of 4 or less.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                    return permanent.TopCard.HasPlayCost && permanent.TopCard.GetCostItself <= 4;

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnReturnToLibraryBottomDigivolutionCard(hashtable, cardSource => cardSource.EqualsCardName("Vemmon"), card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectPermanentCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: maxCount,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Destroy,
                    cardEffect: activateClass);

                await selectPermanentEffect.Activate();
            }
        }
        #endregion

        return cardEffects;
    }
}
