// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// BT19_081 (Blue Tamer) — [Blue Flare]/[Xros Heart] DigiXros support Tamer.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT19/Blue/BT19_081.cs (219 lines, 3 regions)
//    * Start of Your Main Phase :12-127 (OnStartMainPhase — 손패 [Blue Flare]/[Xros Heart] 디지몬 1장을
//        자기 테이머 밑에 진화원으로 배치 → memory +1)
//    * All Turns                :129-206 (BeforePayCost — [Blue Flare]+DigiXros 디지몬 플레이 시, 이 테이머
//        서스펜드 → AddMaxUnderTamerCountDigiXrosClass 등재(테이머 밑 카드를 DigiXros 진화원으로 선택 가능,
//        GetCount=100))
//    * Security Effect          :208-215 (SecuritySkill — CardEffectFactory.PlaySelfTamerSecurityEffect)
//
// ② 배선 관례 근거 (trigger-wiring-porting-rules): timing 상수 그대로(OnStartMainPhase/BeforePayCost/SecuritySkill).
//    DigiXros arm은 EX4_062(쌍둥이 카드)와 1:1 동형 — AddMaxUnderTamerCountDigiXrosClass를 카드가 직접 new,
//    UntilCalculateFixedCostEffect에 등재.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task; `yield return StartCoroutine(X)` / `yield return ContinuousController.instance
//      .StartCoroutine(X)` → `await X`; inner IEnumerator local func → Task-반환 local func(`Task.CompletedTask`);
//      `yield return null;` 소거.
//    * `card.PermanentOfThisCard()`(가변 필요: SuspendPermanentsClass) → `ICardEffect.ResolvePermanentOfThisCard(card)`.
//    * `new SuspendPermanentsClass(list, CardEffectCommons.CardEffectHashtable(activateClass)).Tap()`(AS-IS
//      Hashtable ctor) → `new SuspendPermanentsClass(list, activateClass, isBlock: false).Tap()`(EX4_062/ST16_14 idiom).
//    * `ContinuousController.instance.PlaySE(...BuffSE)` = UI, 스트립(EX4_062 관례).
//    * `card.Owner.UntilCalculateFixedCostEffect` → `new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect`
//      (Player 인스턴스 전용 리스트 — EX4_062 idiom); `card.Owner.AddMemory(...)` = PlayerId 확장 그대로(EX4_062).
//    * `selectedPermanent.AddDigivolutionCardsBottom(selectedCards, activateClass)`(AS-IS ICardEffect 인자) →
//      cause-id 인자 `activateClass.EffectSourceCard?.InstanceId`(BT18_065/AD1_006 idiom).
//    * SelectPermanentEffect canTargetCondition = Permanent-형 술어 직접 전달;
//      selectPermanentCoroutine/selectCardCoroutine = Task-반환.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT19.Blue;

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT19_081 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Start of Your Main Phase

        if (timing == EffectTiming.OnStartMainPhase)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Place 1 [Blue Flare]/[Xros Heart] card under 1 of your Tamers to gain 1 memory",
                CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[Start of Your Main Phase] By placing 1 Digimon card with the [Blue Flare]/[Xros Heart] trait from your hand under any of your Tamers, gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card);
            }

            bool IsOwnTamerCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card) &&
                       permanent.IsTamer;
            }

            bool HasBlueFlareXrosHeartTraitCondition(CardSource cardSource)
            {
                return cardSource.IsDigimon &&
                       (cardSource.EqualsTraits("Blue Flare") || cardSource.EqualsTraits("Xros Heart"));
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card) &&
                       CardEffectCommons.HasMatchConditionOwnersHand(card, HasBlueFlareXrosHeartTraitCondition);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                List<CardSource> selectedCards = new List<CardSource>();

                SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                selectHandEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: HasBlueFlareXrosHeartTraitCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    selectCardCoroutine: SelectCardCoroutine,
                    afterSelectCardCoroutine: null,
                    mode: SelectHandEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectHandEffect.SetUpCustomMessage("Select 1 card to place under a tamer.",
                    "The opponent is selecting 1 card to play.");
                selectHandEffect.SetUpCustomMessage_ShowCard("Placed card");

                await selectHandEffect.Activate();

                Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCards.Add(cardSource);

                    return Task.CompletedTask;
                }

                if (selectedCards.Count >= 1)
                {
                    Permanent? selectedPermanent = null;

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsOwnTamerCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Tamer to place the chosen card under.",
                        "The opponent is selecting 1 Tamer to place the chosen card under.");

                    await selectPermanentEffect.Activate();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanent = permanent;

                        return Task.CompletedTask;
                    }

                    if (selectedPermanent != null)
                    {
                        await selectedPermanent.AddDigivolutionCardsBottom(selectedCards, activateClass.EffectSourceCard?.InstanceId);

                        await card.Owner.AddMemory(1, activateClass);
                    }
                }
            }
        }

        #endregion

        #region All Turns

        if (timing == EffectTiming.BeforePayCost)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Can select DigiXros cards from Tamer's digivolution cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDescription());
            activateClass.SetHashString("CanSelectDigiXrosFromTamer_BT19_081");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[All Turns] When any of your [Blue Flare] trait Digimon cards with DigiXros requirements would be played, by suspending this Tamer, you may place cards from under your Tamers as digivolution cards for a DigiXros.";
            }

            bool CardCondition(CardSource cardSource)
            {
                return cardSource.Owner == card.Owner && cardSource.IsDigimon &&
                       cardSource.EqualsTraits("Blue Flare") && cardSource.HasDigiXros;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card) &&
                       CardEffectCommons.CanTriggerWhenPermanentWouldPlay(hashtable, CardCondition) &&
                       CardEffectCommons.IsOnly1CardPlayed(hashtable);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.CanActivateSuspendCostEffect(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await new SuspendPermanentsClass(
                    new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) },
                    activateClass, isBlock: false).Tap();

                // AS-IS `ContinuousController.instance.PlaySE(...BuffSE)` = UI (stripped).

                AddMaxUnderTamerCountDigiXrosClass addMaxTamerCountDigiXrosClass = new AddMaxUnderTamerCountDigiXrosClass();
                addMaxTamerCountDigiXrosClass.SetUpICardEffect("Can select DigiXros cards from Tamer's digivolution cards",
                    CanUseCondition1, card);
                addMaxTamerCountDigiXrosClass.SetUpAddMaxUnderTamerCountDigiXrosClass(getMaxUnderTamerCount: GetCount);
                new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add(_ => addMaxTamerCountDigiXrosClass);

                bool CanUseCondition1(Hashtable conHashtable)
                {
                    return true;
                }

                int GetCount(CardSource cardSource)
                {
                    if (CardSourceCondition(cardSource))
                    {
                        return 100;
                    }

                    return 0;
                }

                bool CardSourceCondition(CardSource cardSource)
                {
                    if (cardSource.Owner == card.Owner)
                    {
                        if (cardSource.HasDigiXros)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        #endregion

        #region Security Effect

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        #endregion

        return cardEffects;
    }
}
