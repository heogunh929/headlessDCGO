// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S2 카드 — BT14_081 (Digimon / Purple)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT14/Purple/BT14_081.cs (246 lines, no region markers)
//    * [When Digivolving] :14-125 (timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving 게이트)
//      — 트래시에서 [Dark Animal]/[SoC] 레벨4 이하 디지몬 카드를 무료·요구무시 플레이(최대 1장, 진화원에
//        [Eiji Nagasumi]가 있으면 최대 3장).
//    * [When Attacking]   :127-220(timing == OnAllyAttack + CanTriggerOnAttack 게이트, Once Per Turn)
//      — 상대 레벨(3+자기 디지몬 수) 이하 디지몬 1체 삭제 → 성공 시 자신 언서스펜드.
//    * All Turns          :222-241(timing == None — ChangeEndTurnMinMemoryClass, 최소메모리 3).
//
// ② 프리미티브 매핑:
//    * P:CanPlayAsNewPermanent — 트래시 카드 후보 필터 (AS-IS :30)
//    * P:PlayPermanentCards    — 선택 카드 플레이 (AS-IS :123)
//    * P:DeletePeremanentAndProcessAccordingToResult — 상대 디지몬 삭제 (AS-IS :205)
//    * P:IUnsuspendPermanents  — 삭제 성공 시 자기 언서스펜드 (AS-IS :215)
//    * P:ChangeEndTurnMinMemoryClass — 턴종료 최소메모리 3 (AS-IS :224-227)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [When Digivolving] → EffectTiming.OnEnterFieldAnyone + CanTriggerWhenDigivolving(hashtable, card)
//      게이트(AS-IS :55 그대로).
//    * [When Attacking] self-스코프 → CanTriggerOnAttack(hashtable, card) triggerGate 필수(AS-IS :160 그대로).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`,
//      lone `yield return null`→`Task.CompletedTask`(BT8_092 idiom).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`(BT8_092 관례).
//    * `card.Owner.TrashCards`/`GetBattleAreaDigimons()` → `new Player(card.Context, card.Owner).*`.
//    * AS-IS card-less `HasMatchConditionPermanent(cond)`(전역 스캔) → 미러 `(card, cond)` 오버로드
//      (ST2_06/ST1_08 관례); `MatchConditionPermanentCount`는 id-형 오버로드(BT17_026 idiom).
//    * AS-IS :87 `card.Owner.fieldCardFrames.Count((frame) => frame.IsEmptyFrame() && frame.IsBattleAreaFrame())`
//      로 maxCount를 추가로 캡핑하는 절 — 미러는 슬롯/용량 모델이 없음(RD-P6C1-2, Player.cs CanMove의
//      확립된 동일 생략과 동일 근거) — 이 Math.Min 단계만 생략, 나머지 로직은 1:1.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT14.Purple;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT14_081 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play Digimon cards from trash", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] You may play 1 level 4 or lower card with the [Dark Animal] or [SoC] trait from your trash without paying the cost. If [Eiji Nagasumi] is in this Digimon's digivolution cards, add 2 to the number of cards this effect may play.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.Level <= 4)
                {
                    if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                    {
                        if (cardSource.HasLevel && cardSource.HasPlayCost)
                        {
                            if (cardSource.CardTraits.Contains("Dark Animal"))
                            {
                                return true;
                            }

                            if (cardSource.CardTraits.Contains("DarkAnimal"))
                            {
                                return true;
                            }

                            if (cardSource.CardTraits.Contains("SoC"))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                int maxCount = 1;

                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Some(cardSource => cardSource.CardNames.Contains("Eiji Nagasumi")) || ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Some(cardSource => cardSource.CardNames.Contains("EijiNagasumi")))
                    {
                        maxCount += 2;
                    }
                }

                maxCount = Math.Min(maxCount, new Player(card.Context, card.Owner).TrashCards.Count((cardSource) => CanSelectCardCondition(cardSource)));

                // AS-IS :87 fieldCardFrames.Count(empty && battle-area) 캡 — 미러 슬롯/용량 모델 없음(RD-P6C1-2,
                // Player.cs CanMove 확립 생략과 동일 근거) — 이 단계만 생략.

                List<CardSource> selectedCards = new List<CardSource>();

                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                selectCardEffect.SetUp(
                            canTargetCondition: CanSelectCardCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select cards to play.",
                            maxCount: maxCount,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Trash,
                            customRootCardList: null,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                selectCardEffect.SetUpCustomMessage("Select cards to play.", "The opponent is selecting cards to play.");
                selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                await selectCardEffect.Activate();

                Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCards.Add(cardSource);

                    return Task.CompletedTask;
                }

                await CardEffectCommons.PlayPermanentCards(cardSources: selectedCards, activateClass: activateClass, payCost: false, isTapped: false, root: SelectCardEffect.Root.Trash, activateETB: true);
            }
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 opponent's Digimon to unsuspend this Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("Delete_BT14_081");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking][Once Per Turn] By deleting 1 of your opponent's level 3 or lower Digimon, unsuspend this Digimon. For each of your Digimon, add 1 to the level this effect may choose.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    int maxLevel = 3;

                    maxLevel += new Player(card.Context, card.Owner).GetBattleAreaDigimons().Count;

                    if (permanent.Level <= maxLevel)
                    {
                        if (permanent.TopCard.HasLevel)
                        {
                            return true;
                        }
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
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentById));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to delete.", "The opponent is selecting 1 Digimon to delete.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(
                            targetPermanents: new List<Permanent>() { permanent },
                            activateClass: activateClass,
                            successProcess: permanents => SuccessProcess(),
                            failureProcess: null);

                        async Task SuccessProcess()
                        {
                            Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                            await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass).Unsuspend();
                        }
                    }
                }
            }
        }

        if (timing == EffectTiming.None)
        {
            ChangeEndTurnMinMemoryClass changeEndTurnMinMemoryClass = new ChangeEndTurnMinMemoryClass();
            changeEndTurnMinMemoryClass.SetUpICardEffect("The turn end condition is the opponent having 3 or more memory.", CanUseCondition, card);
            changeEndTurnMinMemoryClass.SetUpChangeEndTurnMinMemoryClass(minMemory => 3);
            cardEffects.Add(changeEndTurnMinMemoryClass);

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
        }

        return cardEffects;
    }
}
