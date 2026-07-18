// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S2 카드 — BT25_092 "Asuna Shiroki" (Tamer / Purple)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Purple/BT25_092.cs (269 lines, 3 regions)
//    * Shared Condition (몸통 최상단, timing 무관) :16-21 — Valid3MOrTSCard 공유 술어.
//    * [Start of your Main Phase] :24-70  (timing == OnStartMainPhase — StartOfYourMainPhaseClass 팩토리)
//      — 손패의 [Three Musketeers] 텍스트/[TS] 특성 카드 1장 트래시 → <Draw 1> + 메모리 1.
//    * [Main]                     :74-256(timing == OnDeclaration)
//      — 자신 서스펜드 → 손패/진화원 중 옵션 1장 트래시(둘 다 가능하면 Yes/No 선택으로 출처 결정) →
//        자기 디지몬 1체가 손패/트래시의 [Three Musketeers]/[TS] 카드로 코스트-1 진화(둘 다 가능하면
//        Yes/No 선택으로 출처 결정).
//    * [Security]                 :259-262(timing == SecuritySkill — PlaySelfTamerSecurityEffect 팩토리)
//
// ② 프리미티브 매핑:
//    * P:StartOfYourMainPhaseClass — [Start of your Main Phase] 팩토리 (AS-IS :26-34)
//    * P:SelectHandEffect Mode.Discard — 손패 트래시(공유 헬퍼, [Start]/[Main] 양쪽 사용) (AS-IS :44/151)
//    * P:CanActivateSuspendCostEffect + P:SuspendPeremanentAndProcessAccordingToResult — 자신 서스펜드 비용
//      (AS-IS :101/115)
//    * P:SelectTrashDigivolutionCards — 진화원에서 옵션 트래시(대안 출처) (AS-IS :169)
//    * P:SelectPermanentEffect Mode.Custom — 진화 대상 디지몬 선택 (AS-IS :185)
//    * P:DigivolveIntoHandOrTrashCard — 코스트-1 진화 (AS-IS :238)
//    * P:PlaySelfTamerSecurityEffect — [Security] 팩토리 (AS-IS :261)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [Start of your Main Phase] → EffectTiming.OnStartMainPhase(팩토리가 CanTriggerStartOfMainPhase 게이트
//      내장 — StartOfYourMainPhaseClass 관례).
//    * [Main] → EffectTiming.OnDeclaration(선언형), activateCondition 없음(null, AS-IS :78 그대로).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`,
//      `yield return StartCoroutine(X)`→`await X`(BT17_026 idiom).
//    * `Valid3MOrTSCard` 공유 지역함수는 AS-IS 그대로 몸통 최상단에 위치(카드 몸통 전체가 캡처).
//    * `GManager.instance.userSelectionManager.SetBoolSelection/WaitForEndSelect/SelectedBoolValue` — 미러
//      1:1(bridge W5, UserSelectionManager.cs).
//    * `cardSource.CanPlayCardTargetFrame(permanent.PermanentFrame, false, activateClass, root)` — 프레임
//      모델이 이제 존재(RD-P6C2-10, CardSource.cs:462-541)하므로 AS-IS 그대로 직접 호출(ArtsDigivolve.cs:46
//      판례와 동일; `permanent.PermanentFrame`은 `FieldCardFrame?`이지만 비-null 파라미터로 그대로 전달 —
//      기존 KeyWordEffects/ArtsDigivolve.cs와 동일한 확립 idiom, nullable 경고만 발생).
//    * `card.Owner.HandCards`/`TrashCards` → `new Player(card.Context, card.Owner).*`.
//    * `new DrawClass(card.Owner, 1, activateClass)` → 미러 ctor `(context, playerId, count, cardEffect)`
//      (BT8_092/BT2_070 idiom).
//    * `card.Owner.AddMemory(1, activateClass)` → HeadlessPlayerId 확장(BT9_111 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Purple;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_092 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Shared Condition
        bool Valid3MOrTSCard(CardSource cardSource)
        {
            return cardSource.HasText("Three Musketeers")
                || cardSource.HasTSTraits;
        }
        #endregion

        #region Start of your Main Phase
        if (timing == EffectTiming.OnStartMainPhase)
        {
            cardEffects.Add(CardEffectFactory.StartOfYourMainPhaseClass(
                card,
                "By trashing a [Three Musketeers] in test or [TS] trait card form hand, <Draw 1> and gain 1 memory",
                ActivateCoroutine,
                EffectDescription(),
                additionalActivateCondition: AdditionalActivateCondition,
                optional: false,
                isSkippable: true
                ));

            string EffectDescription() => "[Start of Your Main Phase] By trashing 1 card with [Three Musketeers] in its text or [TS] trait from your hand, <Draw 1> and gain 1 memory.";

            bool AdditionalActivateCondition(Hashtable hashtable, ActivateClass activateClass) => CardEffectCommons.HasMatchConditionOwnersHand(card, Valid3MOrTSCard);

            async Task ActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
            {
                SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                selectHandEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: Valid3MOrTSCard,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    selectCardCoroutine: null,
                    afterSelectCardCoroutine: AfterSelectCardCoroutine,
                    mode: SelectHandEffect.Mode.Discard,
                    cardEffect: activateClass);

                await selectHandEffect.Activate();

                async Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                {
                    if (cardSources.Count >= 1)
                    {
                        await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();

                        await card.Owner.AddMemory(1, activateClass);
                    }
                }
            }
        }
        #endregion

        #region Main
        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Suspend and trash an option from hand or sources to digivolve into [Three Musketeers]/[TS] Digimon for 1 less", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "[Main] By suspending this Tamer and trashing 1 Option card from your hand or your Digimon's digivolution cards, 1 of your Digimon may digivolve into a Digimon card with [Three Musketeers] in its text or the [TS] trait in the hand or trash with the cost reduced by 1.";

            bool ValidTrashCard(CardSource cardSource) => cardSource.IsOption;

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                    && permanent.DigivolutionCards.Any(ValidTrashCard);
            }

            bool ValidTarget(CardSource cardSource, SelectCardEffect.Root root, Permanent permanent)
            {
                return Valid3MOrTSCard(cardSource)
                    && cardSource.CanPlayCardTargetFrame(permanent.PermanentFrame, false, activateClass, root);
            }

            bool CanDigivolveDigimon(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                    && (CardEffectCommons.HasMatchConditionOwnersHand(card, cardSource => ValidTarget(cardSource, SelectCardEffect.Root.Hand, permanent))
                        || CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, cardSource => ValidTarget(cardSource, SelectCardEffect.Root.Trash, permanent)));
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanActivateSuspendCostEffect(card)
                    && (CardEffectCommons.HasMatchConditionOwnersHand(card, ValidTrashCard)
                        || CardEffectCommons.HasMatchConditionOwnersPermanent(card, PermanentCondition));
            }

            Permanent? PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            bool CanDigivolveDigimonById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanDigivolveDigimon(permanent);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                #region Pay costs
                await CardEffectCommons.SuspendPeremanentAndProcessAccordingToResult(
                    new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) },
                    activateClass,
                    SuccessProcess,
                    null);

                async Task SuccessProcess(List<Permanent> suspendedPermaments)
                {
                    bool validHandCard = CardEffectCommons.HasMatchConditionOwnersHand(card, ValidTrashCard);
                    bool validDigivolutionCard = CardEffectCommons.HasMatchConditionOwnersPermanent(card, PermanentCondition);
                    if (validHandCard && validDigivolutionCard)
                    {
                        List<SelectionElement<bool>> selectionElements1 = new List<SelectionElement<bool>>()
                        {
                            new SelectionElement<bool>(message: $"From hand", value : true, spriteIndex: 0),
                            new SelectionElement<bool>(message: $"From digivolution cards", value : false, spriteIndex: 1),
                        };

                        string selectPlayerMessage1 = "From which area will you trash an option?";
                        string notSelectPlayerMessage1 = "The opponent is choosing from which area to trash a card.";

                        GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements1, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage1, notSelectPlayerMessage: notSelectPlayerMessage1);
                    }
                    else
                    {
                        GManager.instance.userSelectionManager.SetBool(validHandCard);
                    }

                    await GManager.instance.userSelectionManager.WaitForEndSelect();

                    bool fromHand = GManager.instance.userSelectionManager.SelectedBoolValue;

                    if (fromHand)
                    {
                        SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                        selectHandEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: ValidTrashCard,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: 1,
                            canNoSelect: false,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            selectCardCoroutine: null,
                            afterSelectCardCoroutine: null,
                            mode: SelectHandEffect.Mode.Discard,
                            cardEffect: activateClass);

                        await selectHandEffect.Activate();
                    }
                    else
                    {
                        await CardEffectCommons.SelectTrashDigivolutionCards(
                            permanentCondition: PermanentCondition,
                            cardCondition: ValidTrashCard,
                            maxCount: 1,
                            canNoTrash: false,
                            isFromOnly1Permanent: true,
                            activateClass: activateClass
                        );
                    }
                    #endregion

                    #region Digivolve
                    if (CardEffectCommons.HasMatchConditionOwnersPermanent(card, CanDigivolveDigimon))
                    {
                        Permanent? selectedPermanent = null;

                        SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectPermanentEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanDigivolveDigimonById,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: 1,
                            canNoSelect: true,
                            canEndNotMax: false,
                            selectPermanentCoroutine: SelectPermanentCoroutine,
                            afterSelectPermanentCoroutine: null,
                            mode: SelectPermanentEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to Digivolve", "The opponent is selecting 1 Digimon to return to digivolve");

                        await selectPermanentEffect.Activate();

                        Task SelectPermanentCoroutine(Permanent permanent)
                        {
                            selectedPermanent = permanent;

                            return Task.CompletedTask;
                        }

                        if (selectedPermanent != null)
                        {
                            bool validCardInHand = new Player(card.Context, card.Owner).HandCards.Any(cardSource => ValidTarget(cardSource, SelectCardEffect.Root.Hand, selectedPermanent));
                            bool validCardInTrash = new Player(card.Context, card.Owner).TrashCards.Any(cardSource => ValidTarget(cardSource, SelectCardEffect.Root.Trash, selectedPermanent));

                            if (validCardInHand && validCardInTrash)
                            {
                                List<SelectionElement<bool>> selectionElements1 = new List<SelectionElement<bool>>()
                                {
                                    new SelectionElement<bool>(message: $"From hand", value : true, spriteIndex: 0),
                                    new SelectionElement<bool>(message: $"From trash", value : false, spriteIndex: 1),
                                };

                                string selectPlayerMessage1 = "From which area do you select a card?";
                                string notSelectPlayerMessage1 = "The opponent is choosing from which area to select a card.";

                                GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements1, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage1, notSelectPlayerMessage: notSelectPlayerMessage1);
                            }
                            else
                            {
                                GManager.instance.userSelectionManager.SetBool(validCardInHand);
                            }

                            await GManager.instance.userSelectionManager.WaitForEndSelect();

                            bool fromHand1 = GManager.instance.userSelectionManager.SelectedBoolValue;

                            await CardEffectCommons.DigivolveIntoHandOrTrashCard(
                                selectedPermanent,
                                Valid3MOrTSCard,
                                payCost: true,
                                reduceCostTuple: (1, null!),
                                fixedCostTuple: null,
                                ignoreDigivolutionRequirementFixedCost: -1,
                                isHand: fromHand1,
                                activateClass,
                                successProcess: null
                            );
                            #endregion
                        }
                    }
                }
            }

        }
        #endregion

        #region Security
        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }
        #endregion

        return cardEffects;
    }
}
