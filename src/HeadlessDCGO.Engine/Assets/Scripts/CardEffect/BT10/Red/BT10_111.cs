// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S4 카드 — BT10_111 (Shoutmon 계열, Digimon / Red)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT10/Red/BT10_111.cs (246 lines, 5 timing 블록)
//    * Name Rule          :17-39  (timing == None — ChangeCardNamesClass, "Also treated as [Shoutmon]")
//    * [On Play]          :41-161 (OnEnterFieldAnyone — 트래시에서 DigiXros요건 카드 1장 손패로,
//      CanSelectDigiXrosClass 부여 UntilEachTurnEndEffects)
//    * <Material Save 1>  :163-166 (WhenPermanentWouldBeDeleted — MaterialSaveEffect)
//    * DigiXros           :168-220 (None — AddDigiXrosConditionClass, "[Xros Heart] Digimon" x2, cost2)
//    * ESS DP+2000        :222-241 (None — ChangeSelfDPStaticEffect, Shoutmon명 카드 위 진화 시 조건부)
//
// ② 프리미티브 매핑:
//    * P:ChangeCardNamesClass        — Name Rule (AS-IS :19-38; P_223 established idiom).
//    * P:CanSelectDigiXrosClass      — [On Play] 몸통이 UntilEachTurnEndEffects에 부여 (AS-IS :117-158).
//    * P:MaterialSaveEffect          — <Material Save 1> (AS-IS :165; CardEffectFactory.cs KeyWordEffects).
//    * P:AddDigiXrosConditionClass   — DigiXros 조건 (AS-IS :170-219; BT21_030 established idiom, 2-element
//      construction 없이 단일 element + null preSelected + cost 2).
//    * P:ChangeSelfDPStaticEffect    — ESS DP+2000 (AS-IS :240).
//    * G-Xros 소비 파이프 표면 실존 확인: SelectDigiXrosClass 실장(Assets/Scripts/Script/SelectDigiXrosClass.cs)
//      + CardSource.HasDigiXros(digiXrosCondition != null, CardSource.cs:1797) — 감사 시절 STOP-예상(RD-R5-04
//      "SelectDigiXrosClass substitution")은 등록-arm 포팅에는 적용되지 않음(등록 arm=AddDigiXrosConditionClass/
//      CanSelectDigiXrosClass 둘 다 STOP-프리 실장, symbol_map §4 latent-gap 목록에도 부재). 그대로 포팅.
//
// ③ 배선 관례 근거: [On Play] → OnEnterFieldAnyone + CanTriggerOnPlay(hashtable, card) 그대로(rule 3).
//    나머지 블록(Name Rule/Material Save/DigiXros/ESS)은 AS-IS 자체가 상시효과(None) 또는 전용 창
//    (WhenPermanentWouldBeDeleted)이라 방언 변환 대상 아님.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine→await (BT8_092 idiom).
//    * `card.Owner.TrashCards` → `new Player(card.Context, card.Owner).TrashCards` (symbol_map_guide §2.2).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`
//      (UntilEachTurnEndEffects.Add/TopCard 접근에 가변 Permanent 필요; BT19_091 idiom).
//    * AS-IS :122 `yield return ...CreateBuffEffect(selectedPermanent)` — UI 연출(가드 없음), 스트립
//      (symbol_map §2.6; ChangeSelfDPStaticEffect 헤더의 CreateBuffEffect 스트립과 동형).
//    * `cardSource.CardTraits.Contains("Xros Heart")` / `.Contains("XrosHeart")` — AS-IS 그대로 유지
//      (CardTraits는 IReadOnlyList<string>, System.Linq.Contains 확장으로 그대로 동작).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT10.Red;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT10_111 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Name Rule

        if (timing == EffectTiming.None)
        {
            ChangeCardNamesClass changeCardNamesClass = new ChangeCardNamesClass();
            changeCardNamesClass.SetUpICardEffect("Also treated as [Shoutmon]", CanUseCondition, card);
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
                    CardNames.Add("Shoutmon");
                }

                return CardNames;
            }
        }

        #endregion

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return 1 card from trash to hand and  this Digimon gets effects", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Return 1 card with a DigiXros requirement from your trash to your hand. When DigiXrosing this turn, you may use this Digimon in place of one of the DigiXros requirements.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource != null)
                {
                    if (cardSource.Owner == card.Owner)
                    {
                        if (cardSource.HasDigiXros)
                        {
                            return true;
                        }
                    }
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
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                {
                    int maxCount = Math.Min(1, new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardCondition));

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => true,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 card to add to your hand.",
                        maxCount: maxCount,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.AddHand,
                        root: SelectCardEffect.Root.Trash,
                        customRootCardList: null,
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    await selectCardEffect.Activate();
                }

                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                if (selectedPermanent != null)
                {
                    CanSelectDigiXrosClass canSelectDigiXrosClass = new CanSelectDigiXrosClass();
                    canSelectDigiXrosClass.SetUpICardEffect("Can be used for DigiXros requirements", CanUseCondition1, card);
                    canSelectDigiXrosClass.SetUpCanSelectDigiXrosClass(CanSelectCondition: CanSelectCondition);
                    selectedPermanent.UntilEachTurnEndEffects.Add((_timing) => canSelectDigiXrosClass);

                    // AS-IS :122 CreateBuffEffect(selectedPermanent) — UI 연출(가드 없음), 스트립.

                    bool CanUseCondition1(Hashtable hashtable)
                    {
                        if (selectedPermanent.TopCard != null)
                        {
                            return true;
                        }

                        return false;
                    }

                    bool CanSelectCondition(CardSource cardSource, Permanent permanent)
                    {
                        if (PermanentCondition(permanent))
                        {
                            return true;
                        }

                        return false;
                    }

                    bool PermanentCondition(Permanent permanent)
                    {
                        if (permanent != null)
                        {
                            if (permanent.TopCard != null)
                            {
                                if (permanent == selectedPermanent)
                                {
                                    return true;
                                }
                            }
                        }

                        return false;
                    }
                }
            }
        }

        #endregion

        #region Material Save

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.MaterialSaveEffect(card: card, materialSaveCount: 1));
        }

        #endregion

        #region DigiXros

        if (timing == EffectTiming.None)
        {
            AddDigiXrosConditionClass addDigiXrosConditionClass = new AddDigiXrosConditionClass();
            addDigiXrosConditionClass.SetUpICardEffect($"DigiXros", CanUseCondition, card);
            addDigiXrosConditionClass.SetUpAddDigiXrosConditionClass(getDigiXrosCondition: GetDigiXros);
            addDigiXrosConditionClass.SetNotShowUI(true);
            cardEffects.Add(addDigiXrosConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            DigiXrosCondition GetDigiXros(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    DigiXrosConditionElement element = new DigiXrosConditionElement(CanSelectCardCondition, "[Xros Heart] Digimon");

                    bool CanSelectCardCondition(CardSource cardSource)
                    {
                        if (cardSource != null)
                        {
                            if (cardSource.Owner == card.Owner)
                            {
                                if (cardSource.IsDigimon)
                                {
                                    if (cardSource.CardTraits.Contains("Xros Heart"))
                                    {
                                        return true;
                                    }

                                    if (cardSource.CardTraits.Contains("XrosHeart"))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }

                        return false;
                    }

                    List<DigiXrosConditionElement> elements = new List<DigiXrosConditionElement>() { element };

                    DigiXrosCondition digiXrosCondition = new DigiXrosCondition(elements, null, 2);

                    return digiXrosCondition;
                }

                return null!;
            }
        }

        #endregion

        #region ESS DP+2000

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (ICardEffect.ResolvePermanentOfThisCard(card).TopCard.ContainsCardName("Shoutmon"))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(changeValue: 2000, isInheritedEffect: true, card: card, condition: Condition));
        }

        #endregion

        return cardEffects;
    }
}
