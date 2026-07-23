// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// PILOT-S1 카드 — EX8_037 (Digimon / Yellow)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX8/Yellow/EX8_037.cs (229 lines, 3 regions)
//    * Alternate Digivolution Conditions :15-26 (timing == None — AddSelfDigivolutionRequirementStaticEffect)
//    * [When Digivolving]                :32-77 (timing == OnEnterFieldAnyone — [Uka-no-Mitama] 토큰 플레이)
//    * [Your Turn]                       :83-224 (timing == OnAllyAttack — 단색 옵션 코스트 무료 사용→언서스펜드)
//
// ② 프리미티브 매핑:
//    * P:AddDigivolutionRequirementClass(AddSelfDigivolutionRequirementStaticEffect) — Lv.6 [Sakuyamon]
//      비-X항체 위 코스트 1 (AS-IS :17-25)
//    * P:PlayUkaNoMitama(PlayCardsBridge) — [When Digivolving] 토큰 플레이 (AS-IS :74-76)
//    * E:SelectHandEffect Mode.Custom + P:PlayOptionCards(payCost:false) + E:SelectPermanentEffect Mode.UnTap
//      — [Your Turn] 단색 옵션 무료 사용→언서스펜드 (AS-IS :141-219)
//
// ③ 배선 관례 근거:
//    * [When Digivolving] → OnEnterFieldAnyone + CanTriggerWhenDigivolving(hashtable, card) 게이트(AS-IS :47
//      그대로).
//    * [Your Turn] → OnAllyAttack + CanTriggerOnPermanentAttack(hashtable, PermanentCondition) 게이트(AS-IS
//      :109-120 그대로, Once Per Turn SetHashString).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`,
//      `yield return StartCoroutine(X)`→`await X` (BT17_026 idiom).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (BT8_092 관례).
//    * `cardSource.OptionCardColors` — 미러 CardSource에 해당 프로퍼티 없음(AS-IS CardSource.cs:1539-1551 게터:
//      `IsOption ? (IsDigimon ? DualCardColors : CardColors) : []`). 이미 존재하는 IsOption/IsDigimon/
//      DualCardColors/CardColors 1차 프리미티브만으로 그 게터 몸통을 그대로 조립하는 로컬 헬퍼
//      `OptionCardColorsOf(CardSource)`를 카드 파일 내부에 둔다(발명 아님 — id-adapter 관례와 동일 성격의
//      조립; Script/ 레이어 수정 없음).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Yellow;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX8_037 : CEntity_Effect
{
    // AS-IS CardSource.OptionCardColors (CardSource.cs:1539-1551) — no mirror property; assembled here from
    // the primitives that DO exist (IsOption/IsDigimon/DualCardColors/CardColors).
    private static IReadOnlyList<string> OptionCardColorsOf(CardSource cardSource)
    {
        if (!cardSource.IsOption)
        {
            return Array.Empty<string>();
        }

        if (cardSource.IsDigimon)
        {
            return cardSource.DualCardColors;
        }

        return cardSource.CardColors;
    }

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution Conditions

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.ContainsCardName("Sakuyamon")
                       && targetPermanent.TopCard.HasLevel
                       && targetPermanent.TopCard.Level == 6
                       && !targetPermanent.TopCard.HasXAntibodyTraits;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 1, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        #endregion

        #region When Digivolving

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play a Token", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[When Digivolving] If [Sakuyamon] or [X Antibody] is in this Digimon's digivolution cards, play 1 [Uka-no-Mitama] (Digimon/Yellow/9000 DP/<Rush>) Token.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                    return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            bool CanActivateTokenEffectCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count((cardSource) => cardSource.EqualsCardName("Sakuyamon") || cardSource.EqualsCardName("X Antibody") || cardSource.EqualsCardName("XAntibody")) >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CanActivateTokenEffectCondition(hashtable))
                {
                    await CardEffectCommons.PlayUkaNoMitama(activateClass);
                }
            }
        }

        #endregion

        #region Your Turn

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 single-color option card", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true,
                EffectDescription());
            activateClass.SetHashString("PlayOption_EX8_037");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[Your Turn] [Once Per Turn] When any of your Digimon attack, you may use 1 single-color Option card with a cost of 5 or less from your hand without paying the cost. If you did, 1 of your Digimon unsuspends.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);
            }

            bool CanSelectOptionCard(CardSource cardSource)
            {
                return OptionCardColorsOf(cardSource).Count == 1
                    && cardSource.GetCostItself <= 5
                    && !cardSource.CanNotPlayThisOption;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerOnPermanentAttack(hashtable, PermanentCondition))
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

            bool CanSelectYourSuspendedDigimon(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) &&
                       permanent.IsDigimon && permanent.IsSuspended;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (new Player(card.Context, card.Owner).HandCards.Count(CanSelectOptionCard) >= 1)
                {
                    bool played = false;

                    List<CardSource> selectedCards = new List<CardSource>();

                    int maxCount = 1;

                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectOptionCard,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        mode: SelectHandEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectHandEffect.SetUpCustomMessage(
                        "Select 1 option card to use.",
                        "The opponent is selecting 1 option card to use.");
                    selectHandEffect.SetUpCustomMessage_ShowCard("Used Card");

                    await selectHandEffect.Activate();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);

                        return Task.CompletedTask;
                    }

                    await CardEffectCommons.PlayOptionCards(
                        cardSources: selectedCards,
                        activateClass: activateClass,
                        payCost: false,
                        root: SelectCardEffect.Root.Hand);

                    if (selectedCards.Any())
                    {
                        played = true;
                    }

                    if (played)
                    {
                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectYourSuspendedDigimon))
                        {
                            SelectPermanentEffect selectPermanentEffect =
                                GManager.instance.GetComponent<SelectPermanentEffect>();

                            selectPermanentEffect.SetUp(
                                selectPlayer: card.Owner,
                                canTargetCondition_ByPreSelecetedList: null,
                                canTargetCondition: CanSelectYourSuspendedDigimon,
                                canEndSelectCondition: null,
                                maxCount: 1,
                                canNoSelect: false,
                                canEndNotMax: false,
                                selectPermanentCoroutine: null,
                                afterSelectPermanentCoroutine: null,
                                mode: SelectPermanentEffect.Mode.UnTap,
                                cardEffect: activateClass);

                            selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to unsuspend.",
                                "The opponent is selecting 1 Digimon to unsuspend.");

                            await selectPermanentEffect.Activate();
                        }
                    }
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
