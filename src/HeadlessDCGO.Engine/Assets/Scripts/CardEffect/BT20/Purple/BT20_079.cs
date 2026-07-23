// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S5 카드 — BT20_079 (Digimon / Purple)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT20/Purple/BT20_079.cs (297 lines, 6 regions)
//    * Security attack +1        :14-18  (timing == None — ChangeSelfSAttackStaticEffect)
//    * Execute                    :22-28  (timing == OnEndTurn — ExecuteSelfEffect, K:Execute)
//    * On Play/When Digivolving Shared :32-45 (공유 로컬함수)
//    * On Play(삭제)              :47-85  (timing == OnEnterFieldAnyone + CanTriggerOnPlay)
//    * When Digivolving(삭제)     :89-127 (AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving —
//      미러 방언 변환 필수, 아래 참조)
//    * On Play(트래시 플레이)      :131-209 (timing == OnEnterFieldAnyone + CanTriggerOnPlay, 별개 효과)
//    * On Deletion(트래시 플레이)  :215-291 (timing == OnDestroyedAnyone)
//
// ② 프리미티브 매핑:
//    * P:ChangeSelfSAttackStaticEffect — Security Attack +1 (AS-IS :16-17; symbol_map row 47 OK).
//    * K:Execute(ExecuteSelfEffect) — [Execute] 자기 부여 (AS-IS :26-27; symbol_map row 245 OK). 사용자
//      사전-경고("K:Execute 재하우징 착지로 스테일 가능성")대로 표면 재확인 결과 실존 확인 —
//      CardEffectFactory/KeyWordEffects/Execute.cs:18에 AS-IS 동일 시그니처(isInheritedEffect, card, condition)
//      로 이식돼 있고 실사용 경로(ExecuteProcess/OnEndTurn 창)까지 확인됨. STOP 불필요.
//    * E:SelectPermanentEffect Mode.Destroy + P:IsMinLevel — On Play/When Digivolving 삭제 몸통 (AS-IS :34-84;
//      symbol_map row 121 OK).
//    * E:SelectCardEffect Mode.Custom/Root.Trash + P:PlayPermanentCards — On Play/On Deletion 트래시 플레이
//      몸통 (AS-IS :164-207/246-289; BT19_091 idiom).
//    * P:CanTriggerOnDeletion/CanActivateOnDeletion — [On Deletion] 게이트 (AS-IS :230/235; symbol_map row
//      25/34 OK).
//
// ③ 배선 관례 근거 — 방언 변환:
//    * [When Digivolving](삭제 몸통) → AS-IS는 OnEnterFieldAnyone(CanTriggerWhenDigivolving 게이트)에 등록하지만,
//      미러는 전용 EffectTiming.WhenDigivolving 키로 등록해야 함(symbol_map_guide §2.7; 이중-키 등록 금지 —
//      STOP 가드 있음). CanTriggerWhenDigivolving(hashtable, card) 게이트 본문은 그대로 유지.
//    * [On Play](삭제/트래시 플레이 둘 다)는 AS-IS 그대로 OnEnterFieldAnyone+CanTriggerOnPlay.
//    * [Execute]는 AS-IS 자체가 OnEndTurn(K:Execute 창). [On Deletion]은 AS-IS 그대로 OnDestroyedAnyone.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`/`yield return
//      StartCoroutine(X)`→`await X`, lone `yield return null`→`Task.CompletedTask` (BT8_092 idiom).
//    * `card.Owner.Enemy` → `CardEffectCommons.OpponentOf(card)` (IsMinLevel이 HeadlessPlayerId 요구;
//      symbol_map_guide §2.2, EX7_014 idiom).
//    * `CardEffectCommons.HasMatchConditionPermanent(cond)`(구식, card 없음) → card 파라미터 추가(Permanent-술어
//      오버로드 존재로 어댑터 불필요). `SelectPermanentEffect.SetUp`의 `canTargetCondition`은 Permanent-술어
//      (CanSelectPermanentConditionShared)를 직접 받음(§2.3, BT17_026 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT20.Purple;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT20_079 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Security attack +1

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, isInheritedEffect: false, card: card,
                condition: null));
        }

        #endregion

        #region Execute

        if (timing == EffectTiming.OnEndTurn)
        {
            cardEffects.Add(CardEffectFactory.ExecuteSelfEffect(isInheritedEffect: false, card: card,
                condition: null));
        }

        #endregion

        #region On Play/When Digivolving Shared

        bool CanSelectPermanentConditionShared(Permanent permanent)
        {
            return CardEffectCommons.IsMinLevel(permanent, CardEffectCommons.OpponentOf(card));
        }

        bool CanActivateConditionShared(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                   CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentConditionShared);
        }

        #endregion

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 of your opponent's Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateConditionShared, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[On Play] Delete 1 of your opponent's Digimon with the lowest level.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectPermanentConditionShared,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
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

        #region When Digivolving
        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:91)이지만 미러 방언은 WhenDigivolving 전용 키
        //   (DigivolveAction이 WhenDigivolving만 해소; 이중-키 등록 금지 — symbol_map_guide §2.7, BT17_026 idiom).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 of your opponent's Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateConditionShared, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[When Digivolving] Delete 1 of your opponent's Digimon with the lowest level.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectPermanentConditionShared,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
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

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("You may play 1 level 5 or lower Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescriptionShared());
            cardEffects.Add(activateClass);

            string EffectDescriptionShared()
            {
                return
                    "[On Play] You may play 1 level 5 or lower Digimon card with the [Ghost] trait from your trash without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, HasCorrectTrait);
            }

            bool HasCorrectTrait(CardSource cardSource)
            {
                return cardSource.IsDigimon && cardSource.EqualsTraits("Ghost") &&
                       cardSource.HasLevel && cardSource.Level <= 5 &&
                       CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass);
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

        #region On Deletion

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("You may play 1 level 5 or lower Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescriptionShared());
            cardEffects.Add(activateClass);

            string EffectDescriptionShared()
            {
                return
                    "[On Deletion] You may play 1 level 5 or lower Digimon card with the [Ghost] trait from your trash without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnDeletion(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanActivateOnDeletion(hashtable, card) &&
                       CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, HasCorrectTrait);
            }

            bool HasCorrectTrait(CardSource cardSource)
            {
                return cardSource.IsDigimon && cardSource.EqualsTraits("Ghost") &&
                       cardSource.HasLevel && cardSource.Level <= 5 &&
                       CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass);
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
