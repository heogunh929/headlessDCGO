// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 정본 트랜치2A 카드 — Magnamon (ST17_13, Digimon / Blue / Lv.6)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/ST17/Blue/ST17_13.cs (320 lines, 4 regions)
//    * Blocker & Armor Purge      :17-27  (None → BlockerSelfStaticEffect; WhenPermanentWouldBeDeleted → ArmorPurgeEffect)
//    * Digivolution Condition     :29-43  (None — Veemon 위 3코스트 자기-진화 조건)
//    * [Security]                 :45-208 (SecuritySkill):
//        - <De-Digivolve 1>       :48-108 (IDegeneration; IsExistOnExecutingArea 게이트)
//        - Digivolve into this     :110-206 (UntilEndBattleEffects → OnEndBattle → DigivolveIntoExcecutingAreaCard)
//    * [When Digivolving]         :210-316 (AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving 게이트):
//        - 색당 top 진화원 트래시 (TrashDigivolutionCardsFromTopOrBottom) + 무-진화원 적 Bounce
//
// ② 감사 축 매핑 (canonical comment ②):
//    * P:DigivolveIntoExcecutingAreaCard        — [Security] after-battle self-digivolve (AS-IS :184-192)
//    * X:ExecutingAreaDigivolve                 — IsExistOnExecutingArea CanActivate 게이트 (AS-IS :73) +
//                                                 UntilEndBattleEffects→OnEndBattle 발화창 (AS-IS :118,:197-205)
//    * K:BlockerSelfStaticEffect                — (AS-IS :20)
//    * K:ArmorPurgeEffect                       — (AS-IS :25)
//    * P:AddSelfDigivolutionRequirementStaticEffect — Veemon 위 3코스트 (AS-IS :41)
//    * P:IDegeneration (<De-Digivolve 1>)       — [Security] 몸통 (AS-IS :104)
//    * P:TrashDigivolutionCardsFromTopOrBottom  — [When Digivolving] 색당 top 트래시 (AS-IS :287)
//    * (+E:SelectPermanentEffect Mode.Custom/Mode.Bounce, T:SecuritySkill, T:WhenDigivolving,
//       C:IsExistOnExecutingArea/IsExistOnBattleAreaDigimon)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(미러 방언; AS-IS는 OnEnterFieldAnyone에
//      두지만 DigivolveAction은 WhenDigivolving만 해소, BT17_026 헤더 rule 3 그대로). CanUseCondition의
//      CanTriggerWhenDigivolving(hashtable, card) 게이트는 AS-IS :243-246 유지.
//    * [Security] → SecuritySkill 등록(SecurityResolver 해소); CanUseCondition = CanTriggerSecurityEffect.
//    * Static/blocker/armor/진화조건 → AS-IS 타이밍 유지(None / WhenPermanentWouldBeDeleted).
//    * AS-IS :116-117 `SetIsSecurityEffect/SetIsDigimonEffect`가 activateClassDigivolve가 아니라
//      **activateClass(De-Digivolve)에 재설정** — 원본대로 1:1 유지(no-simplification: 원본 오작성 보존).
//    * AS-IS :95,:168,:191 SelectPermanent/DigivolveInto의 `cardEffect/activateClass: activateClass`
//      (activateClassDigivolve 아님) — 원본 그대로 유지.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`
//      (BT8_092 idiom). 중첩 `IEnumerator SelectPermanentCoroutine(Permanent)` 몸통 → `async Task`;
//      lone `yield return null` 몸통 → `Task ...{ ...; return Task.CompletedTask; }`.
//    * `GManager.instance.GetComponent<SelectPermanentEffect>()` — VERBATIM 유지.
//    * `SelectPermanentEffect.SetUp`의 canTargetCondition은 미러에서 `Func<HeadlessEntityId,bool>` —
//      AS-IS `Func<Permanent,bool>` 술어는 그대로 두고 id 어댑터(PermanentOf + CanSelect…ById)를 덧댐
//      (BT17_026 idiom; 술어 뭉갬 금지). HasMatchConditionPermanent/MatchConditionPermanentCount는
//      Permanent-술어 오버로드가 존재(CardEffectCommons.cs:4079 / Save.cs:25)하므로 AS-IS 술어 직접 전달,
//      단 미러 시그니처가 `card` 선행 → `(card, 술어)`로 배선.
//    * `new IDegeneration(selectedPermanent, 1, activateClass).Degeneration()` (AS-IS :104) → 미러 ctor
//      (CardController.cs:831)는 3번째가 `HeadlessEntityId? causeEffectSourceId`이므로
//      `new IDegeneration(selectedPermanent, 1, activateClass.EffectSourceCard?.InstanceId, cardEffect: activateClass)`
//      (EX8_051 확립 call-site 관례) + `await`.
//    * `card.Owner.UntilEndBattleEffects.Add(...)` (AS-IS :118) → `new Player(card.Context, card.Owner)
//      .UntilEndBattleEffects.Add(...)` (Player.cs:279 미러 store-백드 리스트).
//    * `ContinuousController.instance.PlaySE(GManager.instance.GetComponent<Effects>().BuffSE)` (AS-IS :111)
//      = UI/SE 연출 — 미러 확립 관례상 스트립.
//    * `card.CanPlayCardTargetFrame(permanent.PermanentFrame, false, activateClassDigivolve, root:
//      SelectCardEffect.Root.Execution)` (AS-IS :134) — 미러에 frame/slot 모델 부재(RD-P6C1-1/2 확립).
//      BT17_026 :432-436 관례로 환원: owner 검사(바깥 IsPermanentExistsOnOwnerBattleAreaDigimon) +
//      `card.CanEvolve(permanent, false)` (CardSource.cs:1993; AS-IS의 bool `false` 유지). RD-P6C1-2 앵커.
//    * `CardEffectCommons.DigivolveIntoExcecutingAreaCard(...)`(DigivolveAndTrashBridge.cs:68 → 실체
//      CardEffectCommons.cs:2825, verbatim 구현·STOP 아님) / `TrashDigivolutionCardsFromTopOrBottom(...)`
//      (DigivolveAndTrashBridge.cs:174) — 시그니처 by-name 일치, `await`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST17.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST17_13 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Blocker and Armor Purge

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.ArmorPurgeEffect(card: card));
        }

        #endregion

        #region Digivolution Condition

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                if ((targetPermanent.TopCard.CardNames.Contains("Veemon")))
                {
                    return true;
                }
                return false;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 3, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        #endregion

        #region Security Effect

        if (timing == EffectTiming.SecuritySkill)
        {
            #region De-Digivolve

            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("De-Digivolve 1 to 1 Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            activateClass.SetIsDigimonEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] <De-Digivolve 1> 1 Digimon. At the end of the battle, 1 of your Digimon may digivolve into this card without paying the digivolution cost.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnBattleAreaDigimon(permanent);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnExecutingArea(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable1)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    // (미러 idiom — BT17_026) SelectPermanentEffect.SetUp의 canTargetCondition은 id-형이므로
                    // AS-IS Permanent-술어는 그대로 두고 id 어댑터를 덧댄다(술어 뭉갬 금지).
                    Permanent? PermanentOf(HeadlessEntityId id) =>
                        card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                            ? new Permanent(card.Context, id, rec.OwnerId)
                            : null;

                    bool CanSelectPermanentById(HeadlessEntityId id)
                    {
                        Permanent? permanent = PermanentOf(id);
                        return permanent is not null && CanSelectPermanentCondition(permanent);
                    }

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to De-Digivolve.", "The opponent is selecting 1 Digimon to De-Digivolve.");
                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        Permanent selectedPermanent = permanent;

                        await new IDegeneration(selectedPermanent, 1, activateClass.EffectSourceCard?.InstanceId, cardEffect: activateClass).Degeneration();
                    }
                }
            }

            #endregion

            #region Digivolve into this card

            // AS-IS :111 ContinuousController.instance.PlaySE(...BuffSE) = UI/SE 연출 — 스트립.

            ActivateClass activateClassDigivolve = new ActivateClass();
            activateClassDigivolve.SetUpICardEffect("Digivolve into this Digimon", CanUseConditionAfterBattle, card);
            activateClassDigivolve.SetUpActivateClass(null, ActivateCoroutineAfterBattle, -1, true, EffectDiscriptionAfterBattle());
            // AS-IS :116-117 — 원본이 activateClassDigivolve가 아닌 activateClass에 재설정(원본대로 유지).
            activateClass.SetIsSecurityEffect(true);
            activateClass.SetIsDigimonEffect(true);
            new Player(card.Context, card.Owner).UntilEndBattleEffects.Add(GetCardEffectAfterBattle);

            string EffectDiscriptionAfterBattle()
            {
                return "[Security] At the end of the battle, 1 of your Digimon may digivolve into this card without paying the digivolution cost.";
            }

            bool CanSelectCardConditionAferBattle(CardSource cardSource)
            {
                return cardSource == card;
            }

            bool CanSelectPermanentConditionAfterBattle(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    // AS-IS :134 card.CanPlayCardTargetFrame(permanent.PermanentFrame, false,
                    // activateClassDigivolve, root: Execution) — 점유-프레임 환원(RD-P6C1-2): owner 검사(위)
                    // + CanEvolve. AS-IS의 bool `false` 유지.
                    if (card.CanEvolve(permanent, false))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool CanUseConditionAfterBattle(Hashtable hashtable)
            {
                return CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentConditionAfterBattle);
            }

            async Task ActivateCoroutineAfterBattle(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentConditionAfterBattle))
                {
                    Permanent? selectedPermanent = null;

                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentConditionAfterBattle));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    Permanent? PermanentOf(HeadlessEntityId id) =>
                        card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                            ? new Permanent(card.Context, id, rec.OwnerId)
                            : null;

                    bool CanSelectPermanentByIdAfterBattle(HeadlessEntityId id)
                    {
                        Permanent? permanent = PermanentOf(id);
                        return permanent is not null && CanSelectPermanentConditionAfterBattle(permanent);
                    }

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentByIdAfterBattle,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will digivolve.", "The opponent is selecting 1 Digimon that will digivolve.");

                    await selectPermanentEffect.Activate();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanent = permanent;

                        return Task.CompletedTask;
                    }


                    if (selectedPermanent != null)
                    {
                        await CardEffectCommons.DigivolveIntoExcecutingAreaCard(
                            targetPermanent: selectedPermanent,
                            cardCondition: CanSelectCardConditionAferBattle,
                            payCost: false,
                            reduceCostTuple: null,
                            fixedCostTuple: null,
                            ignoreDigivolutionRequirementFixedCost: -1,
                            activateClass: activateClass,
                            successProcess: null);
                    }
                }
            }

            ICardEffect GetCardEffectAfterBattle(EffectTiming _timing)
            {
                if (_timing == EffectTiming.OnEndBattle)
                {
                    return activateClassDigivolve;
                }

                return null;
            }

            #endregion
        }

        #endregion

        #region When Digivolving

        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:212)이지만 미러 방언은 WhenDigivolving 전용 키
        //   (DigivolveAction이 WhenDigivolving만 해소; 이중-키 등록 금지 — trigger-wiring rule 3).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash digivolution cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsDigimonEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] For each color of 1 of your opponent's Digimon has, trash that Digimon's top digivolution card. Then, return 1 of your opponent's Digimon without digivolution cards to the hand.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanSelectPermanentCondition1(Permanent permanent)
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

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    Permanent? PermanentOf(HeadlessEntityId id) =>
                        card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                            ? new Permanent(card.Context, id, rec.OwnerId)
                            : null;

                    bool CanSelectPermanentById(HeadlessEntityId id)
                    {
                        Permanent? permanent = PermanentOf(id);
                        return permanent is not null && CanSelectPermanentCondition(permanent);
                    }

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will trash digivolution cards.", "The opponent is selecting 1 Digimon that will trash digivolution cards.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        Permanent selectedPermanent = permanent;

                        int count()
                        {
                            return selectedPermanent.TopCard.CardColors.Count();
                        }

                        await CardEffectCommons.TrashDigivolutionCardsFromTopOrBottom(targetPermanent: selectedPermanent, trashCount: count(), isFromTop: true, activateClass: activateClass);

                    }
                }

                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition1))
                {
                    int maxCount1 = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition1));

                    SelectPermanentEffect selectPermanentEffect1 = GManager.instance.GetComponent<SelectPermanentEffect>();

                    Permanent? PermanentOf(HeadlessEntityId id) =>
                        card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                            ? new Permanent(card.Context, id, rec.OwnerId)
                            : null;

                    bool CanSelectPermanentById1(HeadlessEntityId id)
                    {
                        Permanent? permanent = PermanentOf(id);
                        return permanent is not null && CanSelectPermanentCondition1(permanent);
                    }

                    selectPermanentEffect1.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById1,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount1,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Bounce,
                        cardEffect: activateClass);

                    await selectPermanentEffect1.Activate();
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
