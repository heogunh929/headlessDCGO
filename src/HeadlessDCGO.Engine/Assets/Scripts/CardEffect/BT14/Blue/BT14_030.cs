// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 정본 트랜치2A 카드 — MarineAngemon (BT14_030, Digimon / Blue)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT14/Blue/BT14_030.cs (354 lines, 3 regions)
//    * [On Play] bounce-chain        :13-150  (AS-IS timing == OnEnterFieldAnyone + CanTriggerOnPlay 게이트)
//    * [When Digivolving] bounce-chain:152-289 (AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving 게이트)
//    * [Your Turn][Once Per Turn] Recovery :291-350 (timing == OnPermamemtReturnedToHand)
//    NOTE: AS-IS의 [On Play]/[When Digivolving] 두 리전은 near-identical이지만 별개 등록이므로 dedupe 없이
//    둘 다 1:1 미러(no-simplification 원칙).
//
// ② 감사 축 매핑 (canonical comment ②):
//    * P:BouncePeremanentAndProcessAccordingToResult — 두 bounce-chain 리전의 몸통: 선택 대상(상대 Lv3 또는
//      자기 Digimon) 손패로 반환 후 successProcess에서 반환된 레벨(LevelJustBeforeRemoveField) 이하 상대
//      Digimon 1장 추가 반환 (AS-IS :100-147, :239-286).
//    * T:OnPermamemtReturnedToHand — [Your Turn][Once Per Turn] 다른 Digimon이 손패로 돌아갈 때 Recovery +1
//      (AS-IS :291-350; CanTriggerOnPermanentDeleted 트리거 패밀리 경유 — 미러 EffectTiming 열거값 존재
//      확인: EffectTiming.cs:111, CardEffectRegistrar.cs:64).
//    * (+E:SelectPermanentEffect Mode.Custom→Mode.Bounce 2단, P:IRecovery(Deck), T:OnPlay, T:WhenDigivolving)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [On Play] 리전 → OnEnterFieldAnyone + CanTriggerOnPlay(hashtable, card) 게이트(AS-IS :48 그대로).
//    * [When Digivolving] 리전 → EffectTiming.WhenDigivolving 전용 키(미러 방언; AS-IS는 OnEnterFieldAnyone
//      :152에 두지만 미러 DigivolveAction은 WhenDigivolving만 해소, 이중-키 등록 금지 — rule 3).
//      CanUseCondition의 CanTriggerWhenDigivolving(hashtable, card) 게이트는 AS-IS :187 그대로 유지.
//    * [Your Turn] Recovery 리전 → OnPermamemtReturnedToHand 그대로(AS-IS :291). SetHashString once-per-turn 키
//      "Recovery_BT14_030" 유지(AS-IS :296).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`
//      (BT8_092 idiom).
//    * `IEnumerator SelectPermanentCoroutine(Permanent)` (lone `yield return null`) →
//      `Task SelectPermanentCoroutine(Permanent){ bounceTargetPermanent = permanent; return Task.CompletedTask; }`.
//    * `IEnumerator SuccessProcess()` + `successProcess: SuccessProcess()` (invoked-enumerator) →
//      `async Task SuccessProcess()` + `successProcess: SuccessProcess` (deferred Func<Task> delegate;
//      미러 브릿지 ProcessAccordingToResultBridge.cs:35가 Func<Task>를 기대 — bare-IEnumerator→Func<Task>는
//      1:1 자연 번역). failureProcess: null 그대로.
//    * `GManager.instance.GetComponent<SelectPermanentEffect>()` 그대로.
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (ICardEffect.cs:537).
//    * SelectPermanentEffect.SetUp의 canTargetCondition은 정본 Func<Permanent,bool> 오버로드(id-flip 3b) —
//      AS-IS Func<Permanent,bool> 술어(CanSelectPermanentCondition / 중첩 CanSelectPermanentCondition1)를
//      어댑터 없이 직결. HasMatchConditionPermanent/MatchConditionPermanentCount 모두 Permanent-술어
//      오버로드(CardEffectCommons/KeyWordEffects/Save.cs:25)라 동일 술어 재사용.
//    * 두 번째 SetUp(Mode.Bounce)은 AS-IS와 동일하게 같은 selectPermanentEffect 인스턴스 재사용.
//    * `bounceTargetPermanent.LevelJustBeforeRemoveField` — 미러 Permanent.LevelJustBeforeRemoveField
//      (Permanent.cs:1689, default -1, >0 게이트) 그대로.
//    * `new IRecovery(card.Owner, 1, activateClass).Recovery()` → 미러 ctor(CardController.cs:409):
//      `new IRecovery(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Recovery()`
//      (확립 관례 = BT2_034 / BT1_107 call-site 동일).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT14.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT14_030 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region On Play

        // ③ 배선: [On Play] → OnEnterFieldAnyone (trigger-wiring rule 3) + CanTriggerOnPlay 게이트(AS-IS :48).
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return Digimon to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] By returning 1 of your opponent's level 3 Digimon or 1 of your Digimon to the hand, return 1 of your opponent's Digimon whose level is less than or equal to the returned Digimon's level to the hand.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.TopCard.HasLevel)
                    {
                        if (permanent.Level == 3)
                        {
                            return true;
                        }
                    }
                }

                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    return true;
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
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
                    Permanent bounceTargetPermanent = null;

                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to return to hand.", "The opponent is selecting 1 Digimon to return to hand.");

                    await selectPermanentEffect.Activate();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        bounceTargetPermanent = permanent;

                        return Task.CompletedTask;
                    }

                    if (bounceTargetPermanent != null)
                    {
                        await CardEffectCommons.BouncePeremanentAndProcessAccordingToResult(
                            targetPermanents: new List<Permanent>() { bounceTargetPermanent },
                            activateClass: activateClass,
                            successProcess: SuccessProcess,
                            failureProcess: null);

                        async Task SuccessProcess()
                        {
                            bool CanSelectPermanentCondition1(Permanent permanent)
                            {
                                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                                {
                                    if (bounceTargetPermanent.LevelJustBeforeRemoveField > 0)
                                    {
                                        if (permanent.Level <= bounceTargetPermanent.LevelJustBeforeRemoveField)
                                        {
                                            if (permanent.TopCard.HasLevel)
                                            {
                                                return true;
                                            }
                                        }
                                    }
                                }

                                return false;
                            }

                            if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition1))
                            {
                                maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition1));

                                selectPermanentEffect.SetUp(
                                    selectPlayer: card.Owner,
                                    canTargetCondition: CanSelectPermanentCondition1,
                                    canTargetCondition_ByPreSelecetedList: null,
                                    canEndSelectCondition: null,
                                    maxCount: maxCount,
                                    canNoSelect: false,
                                    canEndNotMax: false,
                                    selectPermanentCoroutine: null,
                                    afterSelectPermanentCoroutine: null,
                                    mode: SelectPermanentEffect.Mode.Bounce,
                                    cardEffect: activateClass);

                                await selectPermanentEffect.Activate();
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region When Digivolving

        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:152)이지만 미러 방언은 WhenDigivolving 전용 키
        //   (DigivolveAction이 WhenDigivolving만 해소; 이중-키 등록 금지 — trigger-wiring rule 3).
        //   CanUseCondition의 CanTriggerWhenDigivolving 게이트는 AS-IS :187 그대로.
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return Digimon to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] By returning 1 of your opponent's level 3 Digimon or 1 of your Digimon to the hand, return 1 of your opponent's Digimon whose level is less than or equal to the returned Digimon's level to the hand.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.TopCard.HasLevel)
                    {
                        if (permanent.Level == 3)
                        {
                            return true;
                        }
                    }
                }

                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    return true;
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
                    Permanent bounceTargetPermanent = null;

                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to return to hand.", "The opponent is selecting 1 Digimon to return to hand.");

                    await selectPermanentEffect.Activate();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        bounceTargetPermanent = permanent;

                        return Task.CompletedTask;
                    }

                    if (bounceTargetPermanent != null)
                    {
                        await CardEffectCommons.BouncePeremanentAndProcessAccordingToResult(
                            targetPermanents: new List<Permanent>() { bounceTargetPermanent },
                            activateClass: activateClass,
                            successProcess: SuccessProcess,
                            failureProcess: null);

                        async Task SuccessProcess()
                        {
                            bool CanSelectPermanentCondition1(Permanent permanent)
                            {
                                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                                {
                                    if (bounceTargetPermanent.LevelJustBeforeRemoveField > 0)
                                    {
                                        if (permanent.Level <= bounceTargetPermanent.LevelJustBeforeRemoveField)
                                        {
                                            if (permanent.TopCard.HasLevel)
                                            {
                                                return true;
                                            }
                                        }
                                    }
                                }

                                return false;
                            }

                            if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition1))
                            {
                                maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition1));

                                selectPermanentEffect.SetUp(
                                    selectPlayer: card.Owner,
                                    canTargetCondition: CanSelectPermanentCondition1,
                                    canTargetCondition_ByPreSelecetedList: null,
                                    canEndSelectCondition: null,
                                    maxCount: maxCount,
                                    canNoSelect: false,
                                    canEndNotMax: false,
                                    selectPermanentCoroutine: null,
                                    afterSelectPermanentCoroutine: null,
                                    mode: SelectPermanentEffect.Mode.Bounce,
                                    cardEffect: activateClass);

                                await selectPermanentEffect.Activate();
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Your Turn - Once Per Turn Recovery

        if (timing == EffectTiming.OnPermamemtReturnedToHand)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Recovery +1 (Deck)", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetHashString("Recovery_BT14_030");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn][Once Per Turn] When another Digimon returns to the hand, <Recovery +1 (Deck)>.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnBattleArea(permanent))
                {
                    if (permanent.IsDigimon)
                    {
                        if (permanent != ICardEffect.ResolvePermanentOfThisCard(card))
                        {
                            return true;
                        }
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
                        if (CardEffectCommons.CanTriggerOnPermanentDeleted(hashtable, PermanentCondition))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new IRecovery(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Recovery();
            }
        }

        #endregion

        return cardEffects;
    }
}
