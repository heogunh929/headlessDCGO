// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S5 카드 — BT23_081 (Tamer / Yellow, "Chitose Imai")
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT23/Yellow/BT23_081.cs (216 lines, 4 regions)
//    * Start of Main Phase :16-34  (timing == OnStartMainPhase — Gain1MemoryTamerOwnerDigimonConditionalEffect)
//    * On Play              :40-116 (timing == OnEnterFieldAnyone — [Hudie] 손패 무상 플레이)
//    * All Turns            :122-201 (timing == OnTappedAnyone — 자기 서스펜드로 상대 디지몬 DP-3000)
//    * Security              :207-210 (timing == SecuritySkill — PlaySelfTamerSecurityEffect)
//
// ② 프리미티브 매핑:
//    * P:Gain1MemoryTamerOwnerDigimonConditionalEffect — [CS] 트레잇 보유 시 메모리+1 (AS-IS :29-33;
//      symbol_map row 304 OK).
//    * E:SelectHandEffect Mode.Custom + P:PlayPermanentCards — [On Play] (AS-IS :77-113; BT19_091 idiom).
//    * P:SuspendPermanentsClass(자기 서스펜드) + E:SelectPermanentEffect Mode.Custom + P:ChangeDigimonDP(-3000)
//      — [All Turns] (AS-IS :159-198; EX11_074 established idiom).
//    * P:PlaySelfTamerSecurityEffect — [Security] (AS-IS :209; symbol_map row 31 OK).
//    * HasCSTraits(derived getter, 미러 CardSource에 없음) → `cardSource.EqualsTraits("CS")` 인라인 조립
//      (AS-IS CardSource.cs:3727-3733 getter 본문 그대로; symbol_map_guide §2.4/§3, HasTSTraits 동형 선례).
//
// ③ 배선 관례 근거: [On Play]→OnEnterFieldAnyone+CanTriggerOnPlay 그대로. [All Turns]는 AS-IS 자체가
//    OnTappedAnyone(임의 서스펜드 감지) + CanTriggerWhenPermanentSuspends 게이트. [Security]는 AS-IS 그대로
//    SecuritySkill.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`,
//      lone `yield return null`→`Task.CompletedTask` (BT8_092 idiom).
//    * `new SuspendPermanentsClass(list, CardEffectCommons.CardEffectHashtable(activateClass)).Tap()` →
//      미러 ctor `(list, activateClass, isBlock: false).Tap()` (Hashtable→직접 파라미터; EX11_074:168 idiom).
//    * `CardEffectCommons.HasMatchConditionPermanent(cond)`/`MatchConditionPermanentCount(cond)`(구식, card
//      없음) → card 파라미터 추가(`HasMatchConditionPermanent`는 Permanent-술어 오버로드 존재로 어댑터 불필요;
//      `MatchConditionPermanentCount`는 id-전용이라 id 어댑터 필요; symbol_map_guide §2.3, BT1_017 idiom).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (BT19_091 idiom).
//    * `cardSource.BasePlayCostFromEntity` (AS-IS property) → mirror exposes it only as the extension method
//      `BasePlayCostFromEntity()` (CardController.cs:4520, `this CardSource card => card.GetCostItself`) — add
//      the call parens, no logic change.
//    * `Gain1MemoryTamerOwnerDigimonConditionalEffect(..., permamentCondition: ...)` (AS-IS misspelled param
//      name) → mirror factory corrected the parameter name to `permanentCondition` (CardEffectFactory.cs:648);
//      call-site spelling only, same argument passed.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT23.Yellow;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT23_081 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Start of Main Phase

        if (timing == EffectTiming.OnStartMainPhase)
        {
            bool PermamentCondition(Permanent permanent)
            {
                // AS-IS `permanent.TopCard.HasCSTraits` — 미러 CardSource에 없는 파생 getter, AS-IS 본문
                // 그대로 인라인 조립(§2.4/§3): HasCSTraits => EqualsTraits("CS") (DCGO CardSource.cs:3727-3733).
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                    && permanent.TopCard.EqualsTraits("CS");
            }

            bool Condition()
            {
                return CardEffectCommons.IsOwnerTurn(card);
            }

            cardEffects.Add(CardEffectFactory.Gain1MemoryTamerOwnerDigimonConditionalEffect(
                effectDescription: "[Start of Your Main Phase] If you have a Digimon with the [CS] trait, gain 1 memory.",
                permanentCondition: PermamentCondition,
                condition: Condition,
                card: card));
        }

        #endregion

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("play 1 play cost 5- [Hudie] trait digimon from hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] You may play 1 play cost 5 or lower Digimon card with the [Hudie] trait from your hand without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.IsDigimon
                    && cardSource.HasPlayCost && cardSource.BasePlayCostFromEntity() <= 5
                    && cardSource.EqualsTraits("Hudie")
                    && CardEffectCommons.CanPlayAsNewPermanent(cardSource, false, activateClass);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersHand(card, CanSelectCardCondition))
                {
                    CardSource? selectedCard = null;

                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOwnersCardCountInHand(card, CanSelectCardCondition));


                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectCardCondition,
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


                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCard = cardSource;
                        return Task.CompletedTask;
                    }

                    selectHandEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                    selectHandEffect.SetUpCustomMessage_ShowCard("Played Card");

                    await selectHandEffect.Activate();

                    if (selectedCard != null)
                    {
                        await CardEffectCommons.PlayPermanentCards(
                            cardSources: new List<CardSource>() { selectedCard },
                            activateClass: activateClass,
                            payCost: false,
                            isTapped: false,
                            root: SelectCardEffect.Root.Hand,
                            activateETB: true);
                    }
                }
            }
        }

        #endregion

        #region All Turns

        if (timing == EffectTiming.OnTappedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("By suspending this tamer, -3K DP 1 digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns] When any of your [Hudie] trait Digimon suspend, by suspending this Tamer, 1 of your opponent's Digimon gets -3000 DP for the turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerWhenPermanentSuspends(hashtable, PermanentCondition);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanActivateSuspendCostEffect(card);
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                    && permanent.TopCard.EqualsTraits("Hudie");
            }

            bool IsOpponentsDigimon(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await new SuspendPermanentsClass(new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) },
                    activateClass, isBlock: false).Tap();

                if (CardEffectCommons.HasMatchConditionPermanent(card, IsOpponentsDigimon))
                {
                    Permanent? selectedPermanent = null;

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, IsOpponentsDigimon));

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsOpponentsDigimon,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectedPermanent,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    Task SelectedPermanent(Permanent target)
                    {
                        selectedPermanent = target;
                        return Task.CompletedTask;
                    }

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will gain -3K DP.", "The opponent is selecting 1 Digimon that will gain -3K DP..");

                    await selectPermanentEffect.Activate();

                    if (selectedPermanent != null)
                    {
                        await CardEffectCommons.ChangeDigimonDP(
                            targetPermanent: selectedPermanent,
                            changeValue: -3000,
                            effectDuration: EffectDuration.UntilEachTurnEnd,
                            activateClass: activateClass);
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
