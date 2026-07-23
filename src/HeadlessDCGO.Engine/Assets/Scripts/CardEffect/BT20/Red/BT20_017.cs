// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 정본 트랜치2A 카드 — Jesmon (BT20_017, Digimon / Red / Lv.6, "Regular Jesmon")
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT20/Red/BT20_017.cs (250 lines, 3 regions)
//    * [On Play] 토큰 플레이        :16-53   (timing == OnEnterFieldAnyone + CanTriggerOnPlay)
//    * [When Digivolving] 토큰 플레이:55-90   (AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving
//                                              게이트 — 미러는 WhenDigivolving 전용 키로 이동; ③ 참조)
//    * [Your Turn][Once Per Turn]   :94-246  (timing == OnEnterFieldAnyone + CanTriggerOnPermanentPlay,
//      on-other-play 삭제→공격         SelectPermanentEffect Mode.Destroy(≤8000DP) → SelectAttackEffect)
//
// ② 프리미티브 매핑 (감사 축 이름):
//    * K:Decoy                 — [Atho, René & Por] 토큰(Digimon/White/6000DP/<Reboot>/<Blocker>/
//                                <Decoy Red/Black>)이 Decoy를 **보유**함. 이 카드(BT20_017) 자체는 Decoy
//                                API를 호출하지 않음 — Decoy는 플레이된 토큰의 카드-정의에 실려 오며,
//                                본 카드의 발화 경로에는 Decoy 프리미티브 호출부가 존재하지 않는다.
//    * P:PlayAthoRenePorToken  — [On Play](:50) & [When Digivolving](:87) 몸통
//                                (CardEffectCommons.PlayAthoRenePorToken 경유; PlayCardsBridge.cs:384).
//    * (+E:SelectPermanentEffect Mode.Destroy(+MaxDP_DeleteEffect 8000)/Mode.Custom,
//       S:SelectAttackEffect, T:OnPlay/WhenDigivolving/OnPermanentPlay)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [On Play] 토큰 → OnEnterFieldAnyone 유지 + CanTriggerOnPlay 게이트(AS-IS :16/:32 그대로).
//    * [When Digivolving] 토큰 → EffectTiming.WhenDigivolving 전용 키(미러 방언; DigivolveAction이
//      WhenDigivolving만 해소, 이중-키 등록 금지 — BT17_026 ③ 동일 근거). CanUseCondition의
//      CanTriggerWhenDigivolving(hashtable, card) 게이트는 AS-IS :69 그대로 유지.
//    * [Your Turn] on-other-play 블록 → OnEnterFieldAnyone 유지(AS-IS :94). CanTriggerOnPermanentPlay는
//      enter-field 트리거이므로 OnEnterFieldAnyone에 그대로 둔다(WhenDigivolving로 옮기지 않음).
//      AS-IS는 세 블록 모두 OnEnterFieldAnyone 아래 있으나, When-Digivolving 토큰 블록만 분리한다.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`
//      (BT8_092 idiom). AfterSelectPermanentCoroutine의 `yield return null`→`return Task.CompletedTask`.
//    * `card.Owner.fieldCardFrames.Count(frame => frame.IsEmptyFrame() && frame.IsBattleAreaFrame()) >= 1`
//      (AS-IS :39/:76) — 미러는 frame/slot 모델 없음: 빈-프레임 용량 절반은 확립 어댑테이션으로 소거
//      (RD-P6C1-1; BT19_091 미러 동일 근거). IsExistOnBattleAreaDigimon(card) 절반은 1:1 유지.
//      PlayAthoRenePorToken 브릿지가 내부적으로 용량 검사를 수행한다.
//    * `GManager.instance.GetComponent<SelectPermanentEffect>()`/`<SelectAttackEffect>()` — VERBATIM
//      (GManager.cs:114/163 지원). SelectPermanentEffect.SetUp의 canTargetCondition은
//      Func<Permanent,bool> 술어(CanSelectDelete/AttackPermanentCondition)를 직접 받음
//      (BT17_026 idiom; 술어 뭉갬 금지).
//    * `card.Owner.MaxDP_DeleteEffect(8000, activateClass)` → `new Player(card.Context, card.Owner)
//      .MaxDP_DeleteEffect(8000, activateClass)` (Player.cs:425).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (ICardEffect.cs:537).
//    * `permanent.CanAttack(activateClass)` — 미러 Permanent.CanAttack(ICardEffect, …) 1:1(:3043).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT20.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT20_017 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region On Play
        // ③ AS-IS :16 OnEnterFieldAnyone 유지 + CanTriggerOnPlay 게이트.
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play a token", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Play 1 [Atho, René & Por] Token. (Digimon/White/6000 DP/<Reboot>/<Blocker>/<Decoy Red/Black>)";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return (CardEffectCommons.CanTriggerOnPlay(hashtable, card));
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                // AS-IS :37-45 — IsExistOnBattleAreaDigimon(card) 절반 1:1 유지. 빈-프레임 용량 절반
                // (AS-IS :39 `card.Owner.fieldCardFrames.Count(...) >= 1`)은 미러 frame-모델 부재로
                // 소거(RD-P6C1-1). PlayAthoRenePorToken 브릿지가 용량 검사를 자체 수행.
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await CardEffectCommons.PlayAthoRenePorToken(activateClass);
            }
        }
        #endregion

        #region When Digivolving
        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:55) 아래 두지만 미러 방언은 WhenDigivolving 전용 키
        //   (DigivolveAction이 WhenDigivolving만 해소; 이중-키 등록 금지 — BT17_026 ③ 동일). CanUseCondition의
        //   CanTriggerWhenDigivolving(hashtable, card) 게이트는 AS-IS :69 그대로 유지.
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play token", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Play 1 [Atho, René & Por] Token. (Digimon/White/6000 DP/<Reboot> <Blocker> <Decoy Red/Black>)";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return (CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card));
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                // AS-IS :74-82 — 빈-프레임 용량 절반 소거(RD-P6C1-1), IsExistOnBattleAreaDigimon 절반 1:1.
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await CardEffectCommons.PlayAthoRenePorToken(activateClass);
            }
        }
        #endregion

        #region Your Turn
        // ③ 배선: AS-IS :94 OnEnterFieldAnyone 유지 — CanTriggerOnPermanentPlay는 enter-field 트리거.
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 8k DP or less, Then 1 Digimon may attack.", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetHashString("PlayLevel6_BT20_017");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] [Once Per Turn] When any of your other Digimon are played, delete 1 of your opponent's Digimon with 8000 DP or less. Then, 1 of you Digimon may attack.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) &&
                       permanent != ICardEffect.ResolvePermanentOfThisCard(card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerOnPermanentPlay(hashtable, PermanentCondition))
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

            bool CanSelectDeletePermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.DP <= new Player(card.Context, card.Owner).MaxDP_DeleteEffect(8000, activateClass))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanSelectAttackPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent.CanAttack(activateClass))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                List<Permanent> selectedPermanents = new List<Permanent>();

                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectDeletePermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectDeletePermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectDeletePermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to delete.", "Opponent is selecting one Digimon to delete.");
                    await selectPermanentEffect.Activate();

                }

                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectAttackPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectAttackPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectAttackPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: AfterSelectPermanentCoroutine,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will attack.", "Opponent is selecting on Digimon that will attack.");
                    await selectPermanentEffect.Activate();

                    // AS-IS :216-220 `IEnumerator AfterSelectPermanentCoroutine(...) { ...; yield return null; }`
                    // → Task 치환(BT8_092 idiom).
                    Task AfterSelectPermanentCoroutine(List<Permanent> permanents)
                    {
                        selectedPermanents = permanents;
                        return Task.CompletedTask;
                    }

                    foreach (Permanent permanent in selectedPermanents)
                    {
                        Permanent selectedPermanent = permanent;

                        if (selectedPermanent != null)
                        {
                            if (selectedPermanent.CanAttack(activateClass))
                            {
                                SelectAttackEffect selectAttackEffect = GManager.instance.GetComponent<SelectAttackEffect>();

                                selectAttackEffect.SetUp(
                                    attacker: selectedPermanent,
                                    canAttackPlayerCondition: () => true,
                                    defenderCondition: (permanent) => true,
                                    cardEffect: activateClass);

                                await selectAttackEffect.Activate();
                            }
                        }
                    }

                }
            }
        }
        #endregion

        return cardEffects;
    }
}
