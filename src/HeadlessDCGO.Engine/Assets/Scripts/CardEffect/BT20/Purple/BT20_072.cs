// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 1:1 포팅 카드 — BT20_072 (Digimon / Purple)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT20/Purple/BT20_072.cs (190 lines, 3 regions)
//    * Execute            :12-20  (OnEndTurn — CardEffectFactory.ExecuteSelfEffect(isInheritedEffect:false))
//    * On Deletion        :22-102 (OnDestroyedAnyone — 자기 [On Deletion]; 트래시의 [Ghost] Lv≤4 디지몬 1장 무료 플레이)
//    * On Deletion - ESS  :104-185(OnDestroyedAnyone — 위와 동일 몸통 + SetIsInheritedEffect(true); 진화원[Inherited] 효과)
//
// ② 배선 관례 근거 (trigger-wiring-porting-rules):
//    * Execute 키워드 → EffectTiming.OnEndTurn 에 ExecuteSelfEffect 등록(NewModelContinuousScan가 해소).
//    * [On Deletion] → OnDestroyedAnyone + CanTriggerOnDeletion(hashtable, card) 게이트 / CanActivateOnDeletion
//      게이트(AS-IS :37-53, :120-136 그대로). ESS(진화원) 팔은 SetIsInheritedEffect(true) 로만 구분.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task; `yield return StartCoroutine(X)`→`await X`;
//      `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`; 내부 `yield return null`→Task.CompletedTask.
//    * `CardColor`(enum) 미사용. EqualsTraits("Ghost")/HasLevel/Level 은 미러 CardSource 동명 멤버 그대로.
//    * SelectCardEffect / PlayPermanentCards / CanPlayAsNewPermanent / HasMatchConditionOwnersCardInTrash —
//      미러 동명 서프리스(SelectCardEffect.cs:273 full SetUp, PlayCardsBridge.cs:37, CardEffectCommons.cs) 1:1.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT20.Purple;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT20_072 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Execute

        if (timing == EffectTiming.OnEndTurn)
        {
            cardEffects.Add(CardEffectFactory.ExecuteSelfEffect(isInheritedEffect: false, card: card,
                condition: null));
        }

        #endregion

        #region On Deletion

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("You may play 1 level 4 or lower Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[On Deletion] You may play 1 level 4 or lower Digimon card with the [Ghost] trait from your trash without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnDeletion(hashtable, card);
            }

            bool HasCorrectTrait(CardSource cardSource)
            {
                return cardSource.IsDigimon && cardSource.EqualsTraits("Ghost") &&
                       cardSource.HasLevel && cardSource.Level <= 4 &&
                       CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanActivateOnDeletion(hashtable, card) &&
                       CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, HasCorrectTrait);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                List<CardSource> selectedCards = new List<CardSource>();

                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                selectCardEffect.SetUp(
                    canTargetCondition: HasCorrectTrait,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    canNoSelect: () => true,
                    selectCardCoroutine: SelectCardCoroutine,
                    afterSelectCardCoroutine: null,
                    message: "Select 1 Digimon card to play.",
                    maxCount: 1,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    mode: SelectCardEffect.Mode.Custom,
                    root: SelectCardEffect.Root.Trash,
                    customRootCardList: null,
                    canLookReverseCard: true,
                    selectPlayer: card.Owner,
                    cardEffect: activateClass);

                selectCardEffect.SetUpCustomMessage("Select 1 Digimon card to play.",
                    "The opponent is selecting 1 Digimon card to play.");
                selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                await selectCardEffect.Activate();

                Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCards.Add(cardSource);

                    return Task.CompletedTask;
                }

                await CardEffectCommons.PlayPermanentCards(
                    cardSources: selectedCards,
                    activateClass: activateClass,
                    payCost: false,
                    isTapped: false,
                    root: SelectCardEffect.Root.Trash,
                    activateETB: true);
            }
        }

        #endregion

        #region On Deletion - ESS

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("You may play 1 level 4 or lower Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[On Deletion] You may play 1 level 4 or lower Digimon card with the [Ghost] trait from your trash without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnDeletion(hashtable, card);
            }

            bool HasCorrectTrait(CardSource cardSource)
            {
                return cardSource.IsDigimon && cardSource.EqualsTraits("Ghost") &&
                       cardSource.HasLevel && cardSource.Level <= 4 &&
                       CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanActivateOnDeletion(hashtable, card) &&
                       CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, HasCorrectTrait);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                List<CardSource> selectedCards = new List<CardSource>();

                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                selectCardEffect.SetUp(
                    canTargetCondition: HasCorrectTrait,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    canNoSelect: () => true,
                    selectCardCoroutine: SelectCardCoroutine,
                    afterSelectCardCoroutine: null,
                    message: "Select 1 Digimon card to play.",
                    maxCount: 1,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    mode: SelectCardEffect.Mode.Custom,
                    root: SelectCardEffect.Root.Trash,
                    customRootCardList: null,
                    canLookReverseCard: true,
                    selectPlayer: card.Owner,
                    cardEffect: activateClass);

                selectCardEffect.SetUpCustomMessage("Select 1 Digimon card to play.",
                    "The opponent is selecting 1 Digimon card to play.");
                selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                await selectCardEffect.Activate();

                Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCards.Add(cardSource);

                    return Task.CompletedTask;
                }

                await CardEffectCommons.PlayPermanentCards(
                    cardSources: selectedCards,
                    activateClass: activateClass,
                    payCost: false,
                    isTapped: false,
                    root: SelectCardEffect.Root.Trash,
                    activateETB: true);
            }
        }

        #endregion

        return cardEffects;
    }
}
