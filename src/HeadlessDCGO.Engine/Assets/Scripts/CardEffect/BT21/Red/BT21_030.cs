// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T3B 정본 카드 — Shoutmon X7: Superior Mode (BT21_030, Digimon / Red)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT21/Red/BT21_030.cs (555 lines, 7 regions)
//    * Alternative Digivolution Condition :15-37  (None — AddSelfDigivolutionRequirementStaticEffect, Lv.6 [Hero] 위 코스트 5)
//    * DigiXros                           :39-120 (None — AddDigiXrosConditionClass, [Xros Heart]/[Blue Flare] ×1)
//    * DigiXros Special                   :122-323(BeforePayCost — [Shoutmon] 진화원 삽입→코스트 -1 + 트래시 DigiXros)
//    * On Play                            :376-415(OnEnterFieldAnyone + CanTriggerOnPlay — 상대 진화원 톱10 트래시)
//    * When Digivolving                   :417-456(AS-IS OnEnterFieldAnyone + CanTriggerWhenDigivolving)
//    * When Attacking                     :458-549(OnAllyAttack, Once Per Turn — 진화원 없는 상대 디지몬 덱밑)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #3, 7축):
//    * X:DigiXros / P:AddDigiXrosConditionClass / P:AddMaxTrashCountDigiXrosClass / P:SelectDigiXrosClass /
//      S:SelectDigiXrosClass — ★수확: None-타이밍 AddDigiXrosConditionClass 등록과 BeforePayCost 특수 팔의
//      비용-머신(SelectPermanent→AddDigivolutionCardInfos→ChangeCost -1→AddMaxTrashCount)은 모두 데이터-홀더/
//      플레이어-버킷 등록으로 실착지(surfaces 실존). 이들을 소비하는 인터랙티브 DigiXros 플레이
//      (SelectDigiXrosClass.Select)는 **7b(옵션 A)에서 완전 포팅됨 — STOP 0**: SelectDigiXrosClass는
//      Script/SelectDigiXrosClass.cs로 실착지(throw 없음), Permanent.CanSubstituteForDigiXrosCondition은
//      Permanent.cs:3644에 실존(RD-EXT3-01/RD-R5-04 상환), SelectHandEffect는 550줄 실구현(R5-A 00552dbf).
//      소비 경로=일반 PlayCard 펌프(PlayCardClass.PlayCard→Select, 실증 tests/RD-BATCH7B.Witness). 즉
//      **구 "잠복 STOP(RD-R5-04/RD-P6C1-5)" 주석은 stale — 수리-9에서 정정**. BeforePayCost 창 자체는 펌프에서
//      개방되나(G9-006), 이 카드의 BeforePayCost 게이트 CanTriggerWhenPermanentWouldPlay는 DigiXros 플레이
//      경로에서만 hashtable을 채우므로 펌프 표면에서 불발(정상 — 게이트 semantics, STOP 아님).
//    * P:DeckBottomBounceClass — When Attacking 덱밑(AS-IS :543). 미러는 SelectPermanentEffect Mode.PutLibraryBottom
//      로 select+bounce 융합(EX5_055 판례). ★수확 예상 정정: 감사 §5는 🟡stop-only(AD1_025)로 표기했으나
//      미러 Mode.PutLibraryBottom 실존 — 포팅 성공.
//    * (+P:AddSelfDigivolutionRequirementStaticEffect·P:ChangeCostClass·P:ITrashStack(진화원 톱10)·
//       E:SelectPermanentEffect, T:BeforePayCost/OnEnterFieldAnyone/OnAllyAttack)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * DigiXros Special → BeforePayCost + CanTriggerWhenPermanentWouldPlay(hashtable, CardCondition)(AS-IS :166).
//      SetHashString "PlayCost-1_BT21_030"(AS-IS :129).
//    * [On Play] → OnEnterFieldAnyone + CanTriggerOnPlay(AS-IS :394); [When Digivolving] → 미러 방언
//      WhenDigivolving 전용 키(AS-IS OnEnterFieldAnyone+CanTriggerWhenDigivolving :419/435, BT17_026 rule 3).
//    * [When Attacking] self-스코프 → CanTriggerOnAttack(hashtable, card)(AS-IS :477). SetHashString
//      "BottomDeck_BT21-030"(AS-IS :465), Once Per Turn(order 1).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`,
//      lone `yield return null`→`Task.CompletedTask`.
//    * `card.Owner`(AS-IS Player) → HeadlessPlayerId; Player 조작(CanReduceCost/UntilCalculateFixedCostEffect)은
//      `new Player(card.Context, card.Owner).*`.
//    * SelectPermanentEffect canTargetCondition는 id-형 — AS-IS Permanent-술어를 PermanentOf(id) 어댑터로 전달
//      (BT17_026/EX5_055 판례). Has/Count 스캔은 Permanent-술어 오버로드 직접 사용(+card 인자).
//    * `ContinuousController.instance.PlaySE(GManager.instance.GetComponent<Effects>().BuffSE)`(AS-IS :227/235) =
//      UI/SE 연출 — 미러 확립 관례상 스트립(ST17_13 :191 판례).
//    * `new DeckBottomBounceClass(List<Permanent>, hashtable).DeckBounce()`(AS-IS :543) → 미러 canonical
//      SelectPermanentEffect Mode.PutLibraryBottom(select+bounce 융합, EX5_055 헤더 판례).
//    * `new ITrashStack(permanent, 10, activateClass).TrashStack()`(AS-IS :369) → 미러 ITrashStack(permanent, 10,
//      cause, cardEffect)(CardController.cs:2184; cause=효과원 InstanceId).
//    * `cardSource.CardID`(AS-IS 퍼-인스턴스 고유 id, _cEntity_Base.CardID) → `cardSource.InstanceId.Value`
//      (미러 퍼-인스턴스 식별자; DigiXros byPreSelected 중복-선택 배제 술어 :143-159).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT21.Red;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT21_030 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // 미러 SelectPermanentEffect는 id-형 canTargetCondition — AS-IS Permanent-술어의 id 어댑터.
        Permanent? PermanentOf(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                ? new Permanent(card.Context, id, rec.OwnerId)
                : null;

        #region Alternative Digivolution Condition

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                if (targetPermanent.TopCard.Level == 6 && targetPermanent.TopCard.EqualsTraits("Hero"))
                {
                    return true;
                }
                return false;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition,
                digivolutionCost: 5,
                ignoreDigivolutionRequirement: false,
                card: card,
                condition: null));
        }

        #endregion

        #region DigiXros

        if (timing == EffectTiming.None)
        {
            AddDigiXrosConditionClass addDigiXrosConditionClass = new AddDigiXrosConditionClass();
            addDigiXrosConditionClass.SetUpICardEffect($"DigiXros", CanUseCondition, card);
            addDigiXrosConditionClass.SetUpAddDigiXrosConditionClass(getDigiXrosCondition: GetDigiXros);
            addDigiXrosConditionClass.SetNotShowUI(true);
            cardEffects.Add(addDigiXrosConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            DigiXrosCondition GetDigiXros(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    DigiXrosConditionElement element = new DigiXrosConditionElement(CanSelectCardCondition, "1 Digimon card with [Xros Heart] or [Blue Flare] trait");

                    bool CanSelectCardCondition(CardSource cardSource)
                    {
                        if (cardSource != null)
                        {
                            if (cardSource.Owner == card.Owner)
                            {
                                if (cardSource.IsDigimon)
                                {
                                    if (cardSource.EqualsTraits("Xros Heart"))
                                    {
                                        return true;
                                    }

                                    if (cardSource.EqualsTraits("Blue Flare"))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }

                        return false;
                    }

                    List<DigiXrosConditionElement> elements = new List<DigiXrosConditionElement>();

                    // AS-IS :86-89 quirk 1:1 유지 — 동일 element를 50회 복제(임의 다중-선택 표현).
                    for (int i = 0; i < 50; i++)
                    {
                        elements.Add(element);
                    }

                    bool CanTargetCondition_ByPreSelecetedList(List<CardSource> cardSources, CardSource cardSource)
                    {
                        List<string> cardIDs = new List<string>();

                        foreach (CardSource cardSource1 in cardSources)
                        {
                            if (!cardIDs.Contains(cardSource1.InstanceId.Value))
                            {
                                cardIDs.Add(cardSource1.InstanceId.Value);
                            }
                        }

                        if (cardIDs.Contains(cardSource.InstanceId.Value))
                        {
                            return false;
                        }

                        return true;
                    }

                    DigiXrosCondition digiXrosCondition = new DigiXrosCondition(elements, CanTargetCondition_ByPreSelecetedList, 1);

                    return digiXrosCondition;
                }

                return null;
            }
        }

        #endregion

        #region DigiXros Special

        if (timing == EffectTiming.BeforePayCost)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play Cost -1 and select trash cards for a DigiXros", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            activateClass.SetHashString("PlayCost-1_BT21_030");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "When you would play this card, by placing 1 [Shoutmon] as a digivolution card under this Digimon, reduce its play cost by 1 and place the cards in your trash as digivolution cards for a DigiXros.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card))
                {
                    if (permanent.TopCard.CardNames.Contains("Shoutmon"))
                    {
                        if (permanent.CanSelectBySkill(activateClass))
                        {
                            if (!permanent.TopCard.CanNotBeAffected(activateClass))
                            {
                                if (!permanent.IsToken)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }

            bool CanSelectPermanentById(HeadlessEntityId id) =>
                PermanentOf(id) is Permanent p && CanSelectPermanentCondition(p);

            bool CardCondition(CardSource cardSource)
            {
                return cardSource == card;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanTriggerWhenPermanentWouldPlay(hashtable, CardCondition))
                {
                    return true;
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = 1;

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: AfterSelectPermanentCoroutine,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 [Shoutmon].", "The opponent is selecting 1 [Shoutmon].");

                    await selectPermanentEffect.Activate();

                    async Task AfterSelectPermanentCoroutine(List<Permanent> permanents)
                    {
                        if (permanents.Count >= 1)
                        {
                            Permanent selectedPermanent = permanents[0];

                            if (selectedPermanent != null)
                            {
                                if (selectedPermanent.TopCard != null)
                                {
                                    if (!selectedPermanent.TopCard.CanNotBeAffected(activateClass))
                                    {
                                        GManager.instance.GetComponent<SelectDigiXrosClass>().AddDigivolutionCardInfos(new AddDigivolutionCardsInfo(activateClass, new List<CardSource>() { selectedPermanent.TopCard }));

                                        // AS-IS :225-228 PlaySE(BuffSE) = UI/SE 연출 — 스트립(ST17_13 판례).
                                        _ = new Player(card.Context, card.Owner).CanReduceCost(null, card);

                                        ChangeCostClass changeCostClass = new ChangeCostClass();
                                        changeCostClass.SetUpICardEffect("Play Cost -1", CanUseCondition1, card);
                                        changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => false, isChangePayingCost: () => true);
                                        new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add((_timing) => changeCostClass);

                                        await CardEffectCommons.ShowReducedCost(_hashtable);

                                        bool CanUseCondition1(Hashtable hashtable)
                                        {
                                            return true;
                                        }

                                        int ChangeCost(CardSource cardSource, int Cost, SelectCardEffect.Root root, List<Permanent> targetPermanents)
                                        {
                                            if (CardSourceCondition(cardSource))
                                            {
                                                if (RootCondition(root))
                                                {
                                                    if (PermanentsCondition(targetPermanents))
                                                    {
                                                        Cost -= 1;
                                                    }
                                                }
                                            }

                                            return Cost;
                                        }

                                        bool PermanentsCondition(List<Permanent> targetPermanents)
                                        {
                                            if (targetPermanents == null)
                                            {
                                                return true;
                                            }
                                            else
                                            {
                                                if (targetPermanents.Count((targetPermanent) => targetPermanent != null) == 0)
                                                {
                                                    return true;
                                                }
                                            }

                                            return false;
                                        }

                                        bool CardSourceCondition(CardSource cardSource)
                                        {
                                            return cardSource == card;
                                        }

                                        bool RootCondition(SelectCardEffect.Root root)
                                        {
                                            return true;
                                        }

                                        bool isUpDown()
                                        {
                                            return true;
                                        }

                                        #region can select digixros cards from trash

                                        AddMaxTrashCountDigiXrosClass addMaxTrashCountDigiXrosClass = new AddMaxTrashCountDigiXrosClass();
                                        addMaxTrashCountDigiXrosClass.SetUpICardEffect("Can select DigiXros cards from trash", CanUseCondition1, card);
                                        addMaxTrashCountDigiXrosClass.SetUpAddMaxTrashCountDigiXrosClass(getMaxTrashCount: GetCount);
                                        Func<EffectTiming, ICardEffect> getCardEffect = GetCardEffect;
                                        new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add(getCardEffect);

                                        ICardEffect GetCardEffect(EffectTiming _timing)
                                        {
                                            if (_timing == EffectTiming.None)
                                            {
                                                return addMaxTrashCountDigiXrosClass;
                                            }

                                            return null;
                                        }

                                        int GetCount(CardSource cardSource)
                                        {
                                            return 100;
                                        }

                                        #endregion
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region On Play / When Digivolving Shared

        bool SharedCanSelectPermanent(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
            {
                return true;
            }
            return false;
        }

        bool SharedCanSelectPermanentById(HeadlessEntityId id) =>
            PermanentOf(id) is Permanent p && SharedCanSelectPermanent(p);

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            if (CardEffectCommons.HasMatchConditionPermanent(card, SharedCanSelectPermanent))
            {
                Permanent selectedPermanent = null;
                int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, SharedCanSelectPermanent));

                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: SharedCanSelectPermanentById,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: maxCount,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: SelectPermanentCoroutine,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon.", "The opponent is selecting 1 Digimon.");
                await selectPermanentEffect.Activate();

                Task SelectPermanentCoroutine(Permanent permanent)
                {
                    selectedPermanent = permanent;
                    return Task.CompletedTask;
                }

                if (selectedPermanent != null)
                {
                    await new ITrashStack(selectedPermanent, 10, activateClass.EffectSourceCard?.InstanceId, cardEffect: activateClass).TrashStack();
                }
            }
        }

        #endregion

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash top 10 stack cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, hashtable => SharedActivateCoroutine(hashtable, activateClass), -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Trash the top 10 stacked cards of 1 of your opponent's Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.CanTriggerOnPlay(hashtable, card))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, SharedCanSelectPermanent))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        #endregion

        #region When Digivolving

        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:419)이나 미러 방언은 WhenDigivolving 전용 키(BT17_026 rule 3).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash top 10 stack cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, hashtable => SharedActivateCoroutine(hashtable, activateClass), -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Trash the top 10 stacked cards of 1 of your opponent's Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, SharedCanSelectPermanent))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        #endregion

        #region When Attacking

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Bottom deck digimon with no digivolution cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("BottomDeck_BT21-030");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] [Once Per Turn] You may return 1 of your opponent's Digimon with no digivolution cards to the bottom of the deck.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.CanTriggerOnAttack(hashtable, card))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.HasNoDigivolutionCards)
                    {
                        return true;
                    }
                }
                return false;
            }

            bool CanSelectPermanentById(HeadlessEntityId id) =>
                PermanentOf(id) is Permanent p && CanSelectPermanentCondition(p);

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    // AS-IS :531-543 Mode.Custom(select) + DeckBottomBounceClass.DeckBounce() → 미러 canonical
                    // Mode.PutLibraryBottom(select+bounce 융합, EX5_055 판례). canNoSelect true(옵션 "may").
                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.PutLibraryBottom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon.", "The opponent is selecting 1 Digimon.");

                    await selectPermanentEffect.Activate();
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
