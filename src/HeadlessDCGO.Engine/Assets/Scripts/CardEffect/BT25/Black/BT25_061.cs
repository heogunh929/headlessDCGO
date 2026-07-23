// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S3 카드 — BT25_061 (Digimon / Black, Offmon)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Black/BT25_061.cs (195 lines, 5 regions)
//    * Link Condition                :14-24  (timing None — AddSelfLinkConditionStaticEffect, Appmon, cost1)
//    * Alternate Digivolution Cond.   :26-36  (timing None — AddSelfDigivolutionRequirementStaticEffect,
//                                               Appmon, cost0, level 2)
//    * Basic link effect keyword      :38-43  (OnDeclaration — CardEffectFactory.LinkEffect)
//    * [Start of Your Main Phase]     :45-109 (OnStartMainPhase — trash 1 [Appmon] hand card -> Draw1 + mem+1)
//    * [When Linked]                  :111-190(WhenLinked — CanTriggerWhenLinking gate, select 1 opponent
//                                               Digimon -> CanNotUnsuspendClass until owner turn end)
//
// ② 프리미티브 매핑:
//    * P:AddSelfLinkConditionStaticEffect, P:AddSelfDigivolutionRequirementStaticEffect, P:LinkEffect
//    * P:CanNotUnsuspendClass — [When Linked] 몸통 (AS-IS :166-169).
//
// ③ 배선 관례 근거:
//    * [When Linked] → EffectTiming.WhenLinked + CanTriggerWhenLinking(hashtable, null, card) 그대로
//      (AS-IS :124 그대로; permanentCondition 인자는 AS-IS도 null).
//    * HasAppmonTraits(파생-getter, 미러 부재) → EqualsTraits("Appmon") 인라인(BT22_035/EX10_029 확립 idiom).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return StartCoroutine(X)`/`ContinuousController.instance.StartCoroutine(X)`
//      → `await X` (BT8_092 idiom).
//    * `card.Owner.HandCards` → `new Player(card.Context, card.Owner).HandCards` (§2.2).
//    * `card.Owner.AddMemory(1, activateClass)` — HeadlessPlayerId 확장(예외 규칙, §2.2) — 그대로.
//    * `new DrawClass(card.Owner, 1, activateClass).Draw()` → `new DrawClass(card.Context, card.Owner, 1, activateClass).Draw()`.
//    * AS-IS :173 `!selectedPermanent.TopCard.CanNotBeAffected(activateClass)` 가드 하 `CreateDebuffEffect`(UI) —
//      스트립하되 가드 술어 자체는 보존할 필요가 없음(가드는 순수 UI 호출의 조건이었을 뿐, 상태효과인
//      CanNotUnsuspendClass 등재는 가드 밖에서 무조건 실행 — AS-IS :166-169 그대로 무조건 등재).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Black;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_061 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Link Condition
        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("Appmon"); // AS-IS TopCard.HasAppmonTraits
            }

            cardEffects.Add(CardEffectFactory.AddSelfLinkConditionStaticEffect(permanentCondition: PermanentCondition, linkCost: 1, card: card));
        }
        #endregion

        #region Alternate Digivolution Condition
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("Appmon"); // AS-IS TopCard.HasAppmonTraits
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 0, ignoreDigivolutionRequirement: false, card: card, condition: null, level: 2));
        }
        #endregion

        #region Basic link effect keyword
        if (timing == EffectTiming.OnDeclaration)
        {
            cardEffects.Add(CardEffectFactory.LinkEffect(card));
        }
        #endregion

        #region Start of Your Main Phase
        if (timing == EffectTiming.OnStartMainPhase)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash 1 card from hand to Draw 1 and memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Start of Your Main Phase] By trashing 1 card with the [Appmon] trait from your hand, <Draw 1> and gain 1 memory.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.EqualsTraits("Appmon"); // AS-IS HasAppmonTraits
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && new Player(card.Context, card.Owner).HandCards.Count >= 1;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (new Player(card.Context, card.Owner).HandCards.Some(CanSelectCardCondition))
                {
                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectCardCondition,
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
                            await card.Owner.AddMemory(1, activateClass);

                            await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();
                        }
                    }
                }
            }
        }
        #endregion

        #region When Linked
        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("1 enemy Digimon can't unsuspend until their turn ends.", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsLinkedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription() => "[When Linking] 1 of your opponent's Digimon can't unsuspend until their turn ends.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenLinking(hashtable, null, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get unable to suspend.", "The opponent is selecting 1 Digimon that will get unable to suspend.");

                await selectPermanentEffect.Activate();

                Task SelectPermanentCoroutine(Permanent permanent)
                {
                    Permanent selectedPermanent = permanent;

                    if (selectedPermanent != null)
                    {
                        CanNotUnsuspendClass canNotUnsuspendClass = new CanNotUnsuspendClass();
                        canNotUnsuspendClass.SetUpICardEffect("Can't Unsuspend", CanUseCondition1, card);
                        canNotUnsuspendClass.SetUpCanNotUntapClass(PermanentCondition: PermanentCondition);
                        selectedPermanent.UntilOwnerTurnEndEffects.Add((_timing) => canNotUnsuspendClass);

                        bool CanUseCondition1(Hashtable hashtable1)
                        {
                            return selectedPermanent.TopCard != null
                                && !selectedPermanent.TopCard.CanNotBeAffected(activateClass);
                        }

                        bool PermanentCondition(Permanent permanent)
                        {
                            return permanent == selectedPermanent;
                        }
                    }

                    return Task.CompletedTask;
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
