// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S4 카드 — P_048 (Digimon / Blue)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/P/Blue/P_048.cs (186 lines, 2 timing 블록)
//    * [When Digivolving]                :14-136 (OnEnterFieldAnyone, CanTriggerWhenDigivolving — 트래시
//      비-디지에그 카드 3장을 순서 지정해 덱 밑으로 되돌려 자기+자기 태머 1체 언서스펜드)
//    * [Your Turn][Once Per Turn]        :138-181 (OnReturnCardsToLibraryFromTrash — 트래시→덱 카드 반환
//      시 +1메모리; 위 arm 자신의 덱-밑-복귀가 이 트리거의 발화원)
//
// ② 프리미티브 매핑:
//    * P:IUnsuspendPermanents — [When Digivolving] 몸통 후반(AS-IS :114; ctor 불변, CardController.cs:1931).
//    * T:OnReturnCardsToLibraryFromTrash — 신규 창 타이밍 소비자. 표면 실존 확인: EffectTiming 키 실존
//      (EffectTiming.cs:114), CanTriggerWhenOwnerCardsReturnToLibraryFromTrash(Hashtable, Func<CardSource,bool>,
//      CardSource) 실장(CanUseEffects/OnCardsReturnToLibraryFromTrash.cs:13) — 이 오버로드는 (AS-IS와 동형)
//      "CardSources" 해시테이블 키를 읽는 GetCardSourcesFromHashtable(Hashtable) 경유(GetFromHashtable.cs:600),
//      **존이동 자동파생(TriggerTimingMap) 경유가 아님** — witness 실측으로 확인(아래 치환 노트 참조). AS-IS
//      AddLibraryBottomCards(:867-874)가 `isFromTrash`일 때 `StackSkillInfos(hashtable, ...)`를 명시 호출하는
//      것과 동일하게, 이 카드 자신도 명시 호출로 창을 연다.
//
// ③ 배선 관례 근거: [When Digivolving] → AS-IS는 OnEnterFieldAnyone에 CanTriggerWhenDigivolving 게이트를
//    달아 등록(:24, 단일 arm) — trigger-wiring rule 3 적용: WhenDigivolving 전용 키로 재배선(BT17_026 idiom).
//    [Your Turn] → AS-IS 자체가 EffectTiming.OnReturnCardsToLibraryFromTrash 키를 씀(방언 변환 대상 아님).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine→await (BT8_092 idiom).
//    * `card.Owner.TrashCards` → `new Player(card.Context, card.Owner).TrashCards`(§2.2).
//    * AS-IS :99 `CardObjectController.AddLibraryBottomCards(cardSources)` — 명명 헬퍼 미이관(mirror
//      CardObjectController에 대응 없음). 유일 공개 대체재 `CardEffectCommons.ReturnRevealedCardsToLibraryBottom`
//      은 리빌-전용(카드 수≥2면 자체 재정렬 프롬프트를 다시 열어 SelectCardEffect가 이미 확정한 순서 위에
//      원치 않는 2차 프롬프트를 만듦 — AS-IS엔 그런 2차 프롬프트가 없음). 대신 그 헬퍼의 내부 몸통이 쓰는
//      바로 그 프리미티브(MatchStateMutationSink + EffectMutation(ReturnToDeckBottomKind, ...) 루프,
//      CardEffectCommons.cs:2811-2819)를 카드-파일 스코프에서 직접 재사용 — `MatchStateMutationSink`의
//      public ctor를 카드 파일에서 직접 구성하는 선례는 AD1_025.cs:146-150(DeckBottomBounceEffect 스테이징)에
//      이미 실존. 이 물리 이동은 선택-순서를 그대로 보존한다(2차 프롬프트 없음).
//      **witness 실측(RD-S4-P048-note)**: `ReturnToDeckBottomKind`(→`IZoneMover.MoveToDeckBottomAsync`)는
//      `MoveCardToSingleZone`을 경유하며 `FromZone`을 `ChoiceZone.None`으로 하드코딩(InMemoryZoneMover.cs:414-425
//      `MoveCardToSingleZone`) — 즉 기록되는 CardMoved 이벤트가 "출처=트래시"를 보존하지 못해
//      TriggerTimingMap의 존-derive 경로로는 OnReturnCardsToLibraryFromTrash가 파생되지 않는다(원래 헤더의
//      "자동 파생" 서술은 틀렸음 — 정정). 그러나 AS-IS 자체도 이 창을 존-이동 파생이 아니라 **명시
//      StackSkillInfos 호출**로 여는 설계(AddLibraryBottomCards :867-874, `isFromTrash` 분기)이므로, 미러도
//      동일하게 물리 이동 직전에 `GManager.instance.autoProcessing.StackSkillInfos({"CardSources":
//      cardSources}, EffectTiming.OnReturnCardsToLibraryFromTrash)`를 명시 호출한다(AS-IS 순서: 창이
//      물리 제거보다 먼저 열림 — IDigiBurst.DigiBurst()의 OnUseDigiburst 명시-호출과 동형 idiom).
//    * AS-IS :101 `Effects.ShowCardEffect(...)` — UI 연출, 스트립.
//    * `new IUnsuspendPermanents(list, activateClass).Unsuspend()` — ctor 불변(§2.5 명시 예외), `.Unsuspend()`
//      Task 반환 그대로.
//    * `CardEffectCommons.MatchConditionPermanentCount(CanSelectPermanentCondition)`(AS-IS 1-인자) →
//      mirror는 leading card + id-형 필수(§2.3): id-어댑터 + `MatchConditionPermanentCount(card, ...)`.
//    * `card.Owner.AddMemory(1, activateClass)` — HeadlessPlayerId 확장 그대로(§2.2 예외절).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.P.Blue;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class P_048 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region When Digivolving

        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:14)에 등록하지만, 미러 방언은 WhenDigivolving 전용 키
        //   (trigger-wiring rule 3 — 이중-키 등록 금지).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return cards from trash to unsuspend this Digimon and your 1 Tamer", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] You may place 3 non-Digi-Egg cards from your trash at the bottom of your deck in any order to unsuspend this Digimon and 1 of your Tamers.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return !cardSource.IsDigiEgg;
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card))
                {
                    if (permanent.IsTamer)
                    {
                        return true;
                    }
                }

                return false;
            }

            Permanent? PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            bool CanSelectPermanentById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanSelectPermanentCondition(permanent);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardCondition) >= 3)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool returned = false;

                if (new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardCondition) >= 3)
                {
                    int maxCount = 3;

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                    canTargetCondition: (cardSource) => CanSelectCardCondition(cardSource),
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    canNoSelect: () => true,
                    selectCardCoroutine: null,
                    afterSelectCardCoroutine: AfterSelectCardCoroutine,
                    message: "Select cards to place at the bottom of the deck\n(cards will be placed back to the bottom of the deck so that cards with lower numbers are on top).",
                    maxCount: maxCount,
                    canEndNotMax: false,
                    isShowOpponent: false,
                    mode: SelectCardEffect.Mode.Custom,
                    root: SelectCardEffect.Root.Trash,
                    customRootCardList: null,
                    canLookReverseCard: true,
                    selectPlayer: card.Owner,
                    cardEffect: activateClass);

                    selectCardEffect.SetNotShowCard();
                    selectCardEffect.SetNotAddLog();

                    await selectCardEffect.Activate();

                    async Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                    {
                        if (cardSources.Count == 3)
                        {
                            // AS-IS :99 CardObjectController.AddLibraryBottomCards(cardSources) — 명명 헬퍼
                            // 미이관(위 헤더 참조). AS-IS AddLibraryBottomCards(:867-874)는 물리 이동보다
                            // 먼저 "isFromTrash" 창을 명시 연다 — 이 카드는 root:Root.Trash로 방금 뽑은
                            // 카드들이므로 항상 트래시발(발화 무조건). 순서 그대로 재현.
                            await GManager.instance.autoProcessing.StackSkillInfos(
                                new Hashtable { { "CardSources", cardSources } },
                                EffectTiming.OnReturnCardsToLibraryFromTrash);

                            // 물리 이동: 선택-순서 그대로 ReturnToDeckBottomKind 뮤테이션 루프로 재구현
                            // (AD1_025.cs:146-150 직접-sink 구성 선례 + CardEffectCommons.cs:2811-2819
                            // 프리미티브 재사용).
                            EngineContext context = card.Context;
                            var bottomSink = new MatchStateMutationSink(
                                context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController,
                                context.EffectRegistry, context.GameEventQueue, context: context);
                            foreach (CardSource cs in cardSources)
                            {
                                bottomSink.Apply(new EffectMutation(
                                    MatchStateMutationSink.ReturnToDeckBottomKind, activateClass.EffectSourceCard?.InstanceId ?? card.InstanceId,
                                    new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cs.InstanceId.Value }));
                            }

                            await bottomSink.FlushAsync();

                            // AS-IS :101 Effects.ShowCardEffect(...) — UI 연출, 스트립.

                            returned = true;
                        }
                    }
                }

                if (returned)
                {
                    if (CardEffectCommons.IsExistOnBattleArea(card))
                    {
                        Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                        await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass).Unsuspend();
                    }

                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentById));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.UnTap,
                        cardEffect: activateClass);
                    await selectPermanentEffect.Activate();
                }
            }
        }

        #endregion

        #region Your Turn - Once Per Turn

        if (timing == EffectTiming.OnReturnCardsToLibraryFromTrash)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetHashString("Memory_P_048");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn][Once Per Turn] When a card is returned from your trash to your deck, gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerWhenOwnerCardsReturnToLibraryFromTrash(hashtable, null, card))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        #endregion

        return cardEffects;
    }
}
