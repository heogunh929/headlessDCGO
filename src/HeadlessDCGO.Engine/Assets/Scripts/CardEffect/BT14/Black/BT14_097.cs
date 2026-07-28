// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T2B 정본 카드 — Suka's Curse (BT14_097, Option / Black)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT14/Black/BT14_097.cs (273 lines, 3 regions)
//    * [None] name-rule       :13-35  (timing == None — ChangeCardNamesClass: 자신을 [Sukamon]으로도 취급)
//    * [Main]                 :37-130 (OptionSkill — 비-white 디지몬 1장이 손패의 [Sukamon]명 디지몬으로
//                                      무료·요구무시 진화)
//    * [Security]             :132-269(SecuritySkill — 상대 디지몬 1장을 white·3000DP·원본명 [Sukamon]으로 변경)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #22):
//    * P:ChangeBaseCardNameClass  — [Security] 몸통 (AS-IS :195-204; 원본명 [Sukamon]으로 변경)
//    * P:ChangeBaseCardColorClass — [Security] 몸통 (AS-IS :232-241; 원본색 white로 변경)
//    * (+P:ChangeCardNamesClass    — [None] name-rule (AS-IS :15-19)
//       +P:DigivolveIntoHandOrTrashCard payCost:false·ignoreReq:0 변형 (AS-IS :117-126),
//       +ChangeBaseDigimonDP(=ChangeBaseDPClass) (AS-IS :186-190), AddEffectToPermanent,
//       +E:SelectPermanentEffect Mode.Custom, T:OptionSkill/SecuritySkill)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [Main] 옵션 주효과 → OptionSkill + CanTriggerOptionMainEffect 게이트(AS-IS :76-79 그대로).
//    * [Security] → SecuritySkill + CanTriggerSecurityEffect 게이트 + SetIsSecurityEffect(true)
//      (AS-IS :132-153 그대로; SecurityResolver가 해소).
//    * [None] 상시 name-rule → EffectTiming.None(스캔형; ChangeCardNames는 이름 판독 훅).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine(X)→await X, lone `yield return null`→Task.CompletedTask (BT8_092 idiom).
//    * `card.Owner.HandCards` → `new Player(card.Context, card.Owner).*` (미러 Player 접근 idiom).
//    * SelectPermanentEffect.canTargetCondition은 정본 Func<Permanent,bool> 오버로드 — 어댑터 없이 AS-IS
//      Permanent-술어 직결(id-flip 3b).
//    * AS-IS :117-126 `CardEffectCommons.DigivolveIntoHandOrTrashCard(...)` — 미러 bridge(payCost:false,
//      fixedCostTuple:null, ignoreDigivolutionRequirementFixedCost:0, isHand:true, successProcess:null).
//    * AS-IS :186-190 `ChangeBaseDigimonDP(targetPermanent, 3000, UntilOwnerTurnEnd, activateClass)` →
//      미러 AS-IS-시그니처 async bridge(GiveEffectToPermanent/ChangeOriginDP.cs) 동일 호출.
//    * `activateClass.SetIsSecurityEffect(true)` — [Security] 발화 마킹(SecurityResolver 관례).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT14.Black;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT14_097 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region None - name rule
        if (timing == EffectTiming.None)
        {
            ChangeCardNamesClass changeCardNamesClass = new ChangeCardNamesClass();
            changeCardNamesClass.SetUpICardEffect("Also treated as having [Sukamon] in its name", CanUseCondition, card);
            changeCardNamesClass.SetUpChangeCardNamesClass(changeCardNames: changeCardNames);

            cardEffects.Add(changeCardNamesClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            List<string> changeCardNames(CardSource cardSource, List<string> CardNames)
            {
                if (cardSource == card)
                {
                    CardNames.Add("Sukamon_BT14_097");
                }

                return CardNames;
            }
        }
        #endregion

        #region Main
        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] 1 of your non-white Digimon may digivolve into a Digimon card with [Sukamon] in its name in your hand without paying the cost, ignoring its digivolution requirements.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (!permanent.TopCard.CardColors.Contains("White"))
                    {
                        foreach (CardSource cardSource in new Player(card.Context, card.Owner).HandCards)
                        {
                            if (CanSelectCardCondition(cardSource))
                            {
                                if (!cardSource.CanNotEvolve(permanent))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.IsDigimon && cardSource.ContainsCardName("Sukamon");
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    Permanent? selectedPermanent = null;

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

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to digivolve.", "The opponent is selecting 1 Digimon to digivolve.");

                    await selectPermanentEffect.Activate();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanent = permanent;

                        return Task.CompletedTask;
                    }

                    if (selectedPermanent != null)
                    {
                        await CardEffectCommons.DigivolveIntoHandOrTrashCard(
                            targetPermanent: selectedPermanent,
                            cardCondition: CanSelectCardCondition,
                            payCost: false,
                            reduceCostTuple: null,
                            fixedCostTuple: null,
                            ignoreDigivolutionRequirementFixedCost: 0,
                            isHand: true,
                            activateClass: activateClass,
                            successProcess: null);
                    }
                }
            }
        }
        #endregion

        #region Security
        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"Opponent's 1 Digimon becomes [Sukamon]", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] Until the end of your turn, change 1 of your opponent's Digimon into being white and having 3000 DP and an original name of [Sukamon].";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

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

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get effects.", "The opponent is selecting 1 Digimon that will get effects.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        Permanent selectedPermanent = permanent;

                        if (selectedPermanent != null)
                        {
                            await CardEffectCommons.ChangeBaseDigimonDP(
                                targetPermanent: permanent,
                                changeValue: 3000,
                                effectDuration: EffectDuration.UntilOwnerTurnEnd,
                                activateClass: activateClass);
                        }

                        if (selectedPermanent != null)
                        {
                            ChangeBaseCardNameClass changeBaseCardNameClass = new ChangeBaseCardNameClass();
                            changeBaseCardNameClass.SetUpICardEffect("Original card name is [Sukamon]", CanUseCondition1, card);
                            changeBaseCardNameClass.SetUpChangeBaseCardNamesClass(changeBaseCardNames: changeBaseCardNames);
                            CardEffectCommons.AddEffectToPermanent(
                                targetPermanent: selectedPermanent,
                                effectDuration: EffectDuration.UntilOwnerTurnEnd,
                                card: card,
                                cardEffect: changeBaseCardNameClass,
                                timing: EffectTiming.None);

                            bool CanUseCondition1(Hashtable hashtable)
                            {
                                if (selectedPermanent.TopCard != null)
                                {
                                    if (!selectedPermanent.TopCard.CanNotBeAffected(activateClass))
                                    {
                                        return true;
                                    }
                                }

                                return false;
                            }

                            List<string> changeBaseCardNames(CardSource cardSource, List<string> CardNames)
                            {
                                if (cardSource == selectedPermanent.TopCard)
                                {
                                    CardNames = new List<string>() { "Sukamon" };
                                }

                                return CardNames;
                            }
                        }

                        if (selectedPermanent != null)
                        {
                            ChangeBaseCardColorClass changeBaseCardColorClass = new ChangeBaseCardColorClass();
                            changeBaseCardColorClass.SetUpICardEffect("Original card color is white", CanUseCondition1, card);
                            changeBaseCardColorClass.SetUpChangeBaseCardColorClass(ChangeBaseCardColors: ChangeBaseCardColors);
                            CardEffectCommons.AddEffectToPermanent(
                                targetPermanent: selectedPermanent,
                                effectDuration: EffectDuration.UntilOwnerTurnEnd,
                                card: card,
                                cardEffect: changeBaseCardColorClass,
                                timing: EffectTiming.None);

                            bool CanUseCondition1(Hashtable hashtable)
                            {
                                if (selectedPermanent.TopCard != null)
                                {
                                    if (!selectedPermanent.TopCard.CanNotBeAffected(activateClass))
                                    {
                                        return true;
                                    }
                                }

                                return false;
                            }

                            List<CardColor> ChangeBaseCardColors(CardSource cardSource, List<CardColor> CardColors)
                            {
                                if (cardSource == selectedPermanent.TopCard)
                                {
                                    CardColors = new List<CardColor>() { CardColor.White };
                                }

                                return CardColors;
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
