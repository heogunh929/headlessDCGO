// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S5 카드 — BT23_057 (Digimon / Black, "Gankoomon")
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT23/Black/BT23_057.cs (410 lines, 6 regions)
//    * Alternate Digivolve Condition  :14-27  (timing == None — [CS] Lv5 위, 코스트 3)
//    * Before Pay Cost - Condition    :36-212 (timing == BeforePayCost — 트래시 3장 덱 위로 되돌려 코스트-5
//      부여, 표시 arm)
//    * Reduce Play Cost - Not Shown   :214-305 (timing == None — 실제 코스트 절감 몸통, UI 미표시)
//    * OP/WD Shared                   :307-371 (공유 로컬함수 — 히누카무이 토큰 + 상대 디지몬 삭제)
//    * On Play                        :373-388 (timing == OnEnterFieldAnyone + CanTriggerOnPlay)
//    * When Digivolving               :390-405 (AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving
//      — 미러 방언 변환 필수, 아래 참조)
//
// ② 프리미티브 매핑:
//    * P:AddDigivolutionRequirementClass(AddSelfDigivolutionRequirementStaticEffect) — Lv5+[CS] 위 코스트 3
//      (AS-IS :17-25).
//    * P:ChangeCostClass ×2(표시 BeforePayCost arm + 비표시 None arm) — Play Cost -5 (AS-IS :145-147/218-222;
//      symbol_map row 40 OK, P_223:102 established idiom).
//    * P:CanTriggerWhenPermanentWouldPlay(Hashtable-오버로드, AS-IS 시그니처 그대로) — BeforePayCost 게이트
//      (AS-IS :79; symbol_map row 76 OK, CanUseEffects/WhenPermanentWouldPlay.cs:13가 AS-IS와 동일
//      (Hashtable, Func&lt;CardSource,bool&gt;) 2-인자 시그니처를 그대로 노출 — BT17_068:115 established idiom).
//    * P:MatchConditionOwnersCardCountInTrash — 트래시 카운트 게이트 (AS-IS :86/99/232/250; symbol_map row 77 OK).
//    * `CardObjectController.AddLibraryTopCards(selectedCards)`(AS-IS :136, 명명 헬퍼 미이관) →
//      MatchStateMutationSink + `ReturnToDeckTopKind` 뮤테이션 루프를 카드-파일 스코프에서 직접 재사용
//      (AD1_025.cs:146-150/P_048.cs:180-191 established idiom — public ctor 직접 구성 선례, ReturnToDeckBottomKind
//      의 Top 자매 kind, MatchStateMutationSink.cs:176).
//    * P:ShowReducedCost(Hashtable) — 미러 UI-only no-op 브릿지, §2.6 예외로 유지(AS-IS :208; symbol_map row 51
//      OK, P_223:164 established idiom).
//    * P:PlayHinukamuyToken — OP/WD 공유 몸통 토큰 소환 (AS-IS :349; symbol_map row 408 OK).
//    * E:SelectPermanentEffect Mode.Destroy — OP/WD 공유 몸통 상대 디지몬 삭제 (AS-IS :351-369).
//
// ③ 배선 관례 근거 — 방언 변환:
//    * [When Digivolving] → AS-IS는 OnEnterFieldAnyone(:392)이지만 미러 방언은 WhenDigivolving 전용 키
//      (DigivolveAction이 WhenDigivolving만 해소; 이중-키 등록 금지 — symbol_map_guide §2.7, BT17_026 idiom).
//      CanTriggerWhenDigivolving(hashtable, card) 게이트 본문은 그대로 유지.
//    * [On Play] → AS-IS 그대로 OnEnterFieldAnyone+CanTriggerOnPlay. [Before Pay Cost]/[None]은 AS-IS 자체가
//      상시/코스트-파이프 창(방언 변환 대상 아님).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`/`yield return
//      StartCoroutine(X)`→`await X`, lone `yield return null`→`Task.CompletedTask` (BT8_092 idiom).
//    * `targetPermanent.TopCard.IsLevel5` → `.IsLevel(5)` (미러 IsLevel(int) idiom; BT24_062:62 idiom).
//    * `targetPermanent.TopCard.HasCSTraits` → `.EqualsTraits("CS")` (§2.4/§3 인라인 조립, DCGO
//      CardSource.cs:3727-3733 getter 본문 1:1; BT23_081 동일 카드군 선례).
//    * `cardSource.Owner.MaxMemoryCost` → `new Player(card.Context, cardSource.Owner).MaxMemoryCost`
//      (symbol_map_guide §2.2; BT1_081 idiom).
//    * `card.Owner.HandCards.Contains(card)` → `new Player(card.Context, card.Owner).HandCards.Contains(card)`
//      (§2.2).
//    * `card.Owner.UntilCalculateFixedCostEffect.Add(...)` → `new Player(card.Context, card.Owner)
//      .UntilCalculateFixedCostEffect.Add(...)` (§2.2; BT17_068:174/BT21_030:290 idiom).
//    * `card.Owner.CanReduceCost(null, card)` + `ContinuousController.instance.PlaySE(...BuffSE)`(AS-IS :140-143)
//      — 순수 UI 연출 게이트(다른 상태변화 없음) — 통째로 스트립(§2.6, BT17_068:166/BT3_056:230 established
//      idiom — CanReduceCost 판정은 SE 재생만 감쌈, 보존할 상태 없음).
//    * `card.Owner.fieldCardFrames.Count(f => f.IsEmptyFrame() && f.IsBattleAreaFrame()) >= 1` — 빈-프레임 용량
//      체크 소거(§2.8, RD-P6C1-1/-2; BT19_091 idiom); co-conjunct(IsExistOnBattleAreaDigimon)만 유지.
//    * `card.Owner.GetBattleAreaPermanents()` → HeadlessPlayerId 확장 그대로 사용(§2.2 예외).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (BT17_026 idiom).
//    * `CardEffectCommons.HasMatchConditionPermanent(cond)`(구식, card 없음) → card 파라미터 추가(Permanent-술어
//      오버로드 존재로 어댑터 불필요). `SelectPermanentEffect.SetUp`의 `canTargetCondition`은 id-전용이라
//      SharedCanSelectPermanentCondition에 id 어댑터를 덧댐(§2.3, BT17_026 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT23.Black;

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT23_057 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolve Condition
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.IsLevel(5) &&
                       targetPermanent.TopCard.EqualsTraits("CS");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition, digivolutionCost: 3, ignoreDigivolutionRequirement: false,
                card: card, condition: null));
        }
        #endregion

        bool CanSelectReturnCardCondition(CardSource cardSource)
        {
            return cardSource.ContainsCardName("Huckmon") ||
                   cardSource.ContainsCardName("Sistermon") ||
                   cardSource.ContainsCardName("Jesmon");
        }

        #region Before Pay Cost - Condition Effect

        if (timing == EffectTiming.BeforePayCost)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return 3 cards with [Huckmon], [Sistermon] or [Jesmon] in their names to get Play Cost -5", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetHashString("PlayCost-5_BT12_057");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "When this card would be played, by returning 3 cards with [Huckmon], [Sistermon] or [Jesmon] in their names from your trash to the top or bottom of the deck, reduce the play cost by 5.";
            }

            bool CardCondition(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    if (CardEffectCommons.IsExistOnHand(cardSource))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanNoSelect(CardSource cardSource)
            {
                if (cardSource != null)
                {
                    if (cardSource.PayingCost(SelectCardEffect.Root.Hand, null, checkAvailability: false) > new Player(card.Context, cardSource.Owner).MaxMemoryCost)
                    {
                        return false;
                    }
                }

                return true;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenPermanentWouldPlay(hashtable, CardCondition);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnHand(card))
                {
                    if (CardEffectCommons.MatchConditionOwnersCardCountInTrash(card, CanSelectReturnCardCondition) >= 3)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<CardSource> selectedCards = new List<CardSource>();

                if (CardEffectCommons.MatchConditionOwnersCardCountInTrash(card, CanSelectReturnCardCondition) >= 3)
                {
                    bool noSelect = CanNoSelect(CardEffectCommons.GetCardFromHashtable(_hashtable));

                    int maxCount = 3;

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: CanSelectReturnCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => noSelect,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        message: "Select cards to add to the top your deck\n(cards will be placed back to the top of the deck so that cards with lower numbers are on top).",
                        maxCount: maxCount,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.Trash,
                        customRootCardList: null,
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    await selectCardEffect.Activate();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);
                        return Task.CompletedTask;
                    }

                    if (selectedCards.Count == 3)
                    {
                        selectedCards.Reverse();

                        // AS-IS :136 `CardObjectController.AddLibraryTopCards(selectedCards)` — 명명 헬퍼
                        // 미이관(헤더 참조). AD1_025.cs:146-150/P_048.cs:180-191 established idiom: sink
                        // ReturnToDeckTopKind 뮤테이션 루프를 카드-파일 스코프에서 직접 재사용, 선택-순서(여기선
                        // Reverse 이후 순서) 그대로 보존.
                        EngineContext context = card.Context;
                        var topSink = new MatchStateMutationSink(
                            context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController,
                            context.EffectRegistry, context.GameEventQueue, context: context);
                        foreach (CardSource cs in selectedCards)
                        {
                            topSink.Apply(new EffectMutation(
                                MatchStateMutationSink.ReturnToDeckTopKind, activateClass.EffectSourceCard?.InstanceId ?? card.InstanceId,
                                new Dictionary<string, object?>(System.StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cs.InstanceId.Value }));
                        }

                        await topSink.FlushAsync();
                    }
                }

                // AS-IS :140-143 `if (card.Owner.CanReduceCost(null, card)) PlaySE(BuffSE)` — 순수 UI 연출
                // 게이트(다른 상태변화 없음), 통째로 스트립(§2.6, BT17_068:166/BT3_056:230 idiom).

                ChangeCostClass changeCostClass = new ChangeCostClass();
                changeCostClass.SetUpICardEffect("Play Cost -1", CanUseCondition1, card);
                changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => false, isChangePayingCost: () => true);
                new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add((_timing) => changeCostClass);

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
                                int targetCost = 0;

                                if (selectedCards.Count >= 3)
                                    targetCost += 5;

                                Cost -= targetCost;
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

                await CardEffectCommons.ShowReducedCost(_hashtable);
            }
        }

        #endregion

        #region Reduce Play Cost - Not Shown

        if (timing == EffectTiming.None)
        {
            ChangeCostClass changeCostClass = new ChangeCostClass();
            changeCostClass.SetUpICardEffect("Play Cost -5", CanUseCondition, card);
            changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => true, isChangePayingCost: () => true);
            changeCostClass.SetNotShowUI(true);
            cardEffects.Add(changeCostClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (new Player(card.Context, card.Owner).HandCards.Contains(card))
                {
                    ICardEffect activateClass = card.EffectList(EffectTiming.BeforePayCost).Find(cardEffect => cardEffect.EffectName == "Return 3 cards with [Huckmon], [Sistermon] or [Jesmon] in their names to get Play Cost -5");

                    if (activateClass != null)
                    {
                        if (CardEffectCommons.MatchConditionOwnersCardCountInTrash(card, CanSelectReturnCardCondition) >= 3)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            int ChangeCost(CardSource cardSource, int Cost, SelectCardEffect.Root root, List<Permanent> targetPermanents)
            {
                if (CardSourceCondition(cardSource))
                {
                    if (RootCondition(root))
                    {
                        if (PermanentsCondition(targetPermanents))
                        {
                            int trashCount = CardEffectCommons.MatchConditionOwnersCardCountInTrash(card, CanSelectReturnCardCondition);
                            int targetCount = 0;

                            if (trashCount >= 3)
                                targetCount += 5;

                            Cost -= targetCount;
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
                if (cardSource != null)
                {
                    if (cardSource == card)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool RootCondition(SelectCardEffect.Root root)
            {
                return true;
            }

            bool isUpDown()
            {
                return true;
            }
        }

        #endregion

        #region OP/WD Shared

        string SharedEffectDiscription(string tag)
        {
            return $"[{tag}] You may play 1 [Hinukamuy] Token. (Digimon/White/6000 DP/<Alliance> <Reboot> <Blocker>) Then, delete 1 of your opponent's Digimon with a play cost of 6 or less. For each of your other Digimon, add 3 to this effect's play cost maximum.";
        }

        bool SharedCanActivateCondition(Hashtable hashtable)
        {
            // AS-IS :318 `&& card.Owner.fieldCardFrames.Count(f => f.IsEmptyFrame() && f.IsBattleAreaFrame())
            // >= 1` — 빈-프레임 용량 체크 소거(§2.8, RD-P6C1-1/-2).
            if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
            {
                return true;
            }

            return false;
        }

        bool SharedCanSelectPermanentCondition(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
            {
                int maxCost = 6;

                maxCost += 3 * card.Owner.GetBattleAreaPermanents().Count(permanent => permanent.IsDigimon && permanent != ICardEffect.ResolvePermanentOfThisCard(card));

                if (permanent.TopCard.GetCostItself <= maxCost)
                {
                    if (permanent.TopCard.HasPlayCost)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        Permanent? PermanentOfShared(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                ? new Permanent(card.Context, id, rec.OwnerId)
                : null;

        bool SharedCanSelectPermanentById(HeadlessEntityId id)
        {
            Permanent? permanent = PermanentOfShared(id);
            return permanent is not null && SharedCanSelectPermanentCondition(permanent);
        }

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            await CardEffectCommons.PlayHinukamuyToken(activateClass);

            if (CardEffectCommons.HasMatchConditionPermanent(card, SharedCanSelectPermanentCondition))
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: SharedCanSelectPermanentById,
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
            activateClass.SetUpICardEffect("Play token", CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, (hashTable) => SharedActivateCoroutine(hashTable, activateClass), -1, true, SharedEffectDiscription("On Play"));
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return (CardEffectCommons.CanTriggerOnPlay(hashtable, card));
            }
        }

        #endregion

        #region When Digivolving
        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:392)이지만 미러 방언은 WhenDigivolving 전용 키
        //   (DigivolveAction이 WhenDigivolving만 해소; 이중-키 등록 금지 — symbol_map_guide §2.7, BT17_026 idiom).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play token", CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, (hashTable) => SharedActivateCoroutine(hashTable, activateClass), -1, true, SharedEffectDiscription("When Digivolving"));
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }
        }
        #endregion

        return cardEffects;
    }
}
