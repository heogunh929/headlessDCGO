// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 정본 카드 — Thomas H. Norstein (BT7_087, Tamer / Blue)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT7/Blue/BT7_087.cs (4 arms)
//    * [Security]                 (SecuritySkill)  — PlaySelfTamerSecurityEffect
//    * [Main][Once Per Turn]      (OnDeclaration)  — 손패 [Hybrid] 5장을 이 Tamer 밑에 놓고, 이 Tamer를
//      level-5 blue Digimon 취급하여 손패 [MagnaGarurumon]으로 진화(:235 IsPlaceToTrashDueToNotHavingDP=false,
//      :259 =true 복원 — treat-as-Digimon 윈도우 동안 DP-없음-트래시 억제).
//    * [Your Turn][Once Per Turn] (OnAddHand)      — 손패에 카드 추가 시 Memory +1 + Unblockable(ESS/inherited)
//
// ② 프리미티브 매핑 (감사 축): P:ChangeCardColorClass, P:ChangePermanentLevelClass, P:TreatAsDigimonClass,
//    P:DontHaveDPClass, P:DigivolveIntoHandOrTrashCard(fixedCost 없음/payCost true, isHand), E:SelectHandEffect
//    (Mode.Custom 5장), E:SelectCardEffect(Root.Custom 순서), Permanent.IsPlaceToTrashDueToNotHavingDP WRITE(신규
//    setter — treat-as-Digimon 윈도우 동안 no-DP-trash 억제/복원).
//
// ③ 배선 관례 근거: [Main] 선언형 = OnDeclaration(AS-IS 그대로; BT25_104/EX8_072 관례). SetHashString
//    ("Digivolve_BT7_085")/([Your Turn] "Memory+1_BT7_087") once-per-turn 해시 유지. OnAddHand ESS =
//    SetIsInheritedEffect(true).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine(X)→await X, lone `yield return null`→Task.CompletedTask.
//    * `card.Owner.HandCards.Count(cond)` → `new Player(card.Context, card.Owner).HandCards.Count(cond)`.
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`.
//    * `TopCard.CardNames.Contains(name)` → `EqualsCardName(name)`(BT17_026 관례).
//    * `AddDigivolutionCardsBottom(list, activateClass)` → `.AddDigivolutionCardsBottom(list,
//      activateClass.EffectSourceCard?.InstanceId)`(BT17_026 미러 시그니처).
//    * treat-as 4효과는 `card.PermanentOfThisCard().PermanentEffects.Add/Remove`(Permanent-레벨 리스트) 그대로 —
//      BT17_026는 player-레벨이지만 BT7_087 AS-IS는 permanent-레벨(1:1 유지).
//    * `card.PermanentOfThisCard().IsPlaceToTrashDueToNotHavingDP = false/true` → 신규 Permanent setter(1:1 WRITE).
//    * `card.Owner.AddMemory(1, activateClass)` → HeadlessPlayerId.AddMemory 확장.
//    * `CanTriggerOnHandAdded(hashtable, card.Owner, null)` → 미러 시그니처 `CanTriggerOnHandAdded(ctx, card,
//      card.Owner, null)`(card 인자 삽입; Hashtable→CardEffectResolveContext 암시 변환).
//    * `GManager.instance.GetComponent<Effects>().CreateDebuffEffect(...)` 류 UI = 스트립(BT17_026 관례).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT7.Blue;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT7_087 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Security
        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }
        #endregion

        #region Main - treat this Tamer as a level 5 blue Digimon and digivolve
        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Place cards from hand to digivolution cards and digivolve", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetHashString("Digivolve_BT7_085");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main][Once Per Turn] You may place 5 cards with [Hybrid] in their traits from your hand under this Tamer in any order to digivolve it into a [MagnaGarurumon] in your hand for its digivolution cost as if this Tamer is a level 5 blue Digimon.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.CardTraits.Contains("Hybrid");
            }

            bool CanSelectCardCondition1(CardSource cardSource)
            {
                return cardSource.EqualsCardName("MagnaGarurumon");
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition) >= 5)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition) >= 5)
                    {
                        List<CardSource> selectedCards = new List<CardSource>();

                        int maxCount = 5;

                        SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                        selectHandEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanSelectCardCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: maxCount,
                            canNoSelect: false,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            mode: SelectHandEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectHandEffect.SetUpCustomMessage("Select cards to place in Digivolution cards.", "The opponent is selecting cards to place in Digivolution cards.");
                        selectHandEffect.SetNotShowCard();

                        await selectHandEffect.Activate();

                        Task SelectCardCoroutine(CardSource cardSource)
                        {
                            selectedCards.Add(cardSource);
                            return Task.CompletedTask;
                        }

                        List<CardSource> digivolutionCards = new List<CardSource>();

                        if (selectedCards.Count == 5)
                        {
                            SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                            selectCardEffect.SetUp(
                                canTargetCondition: (cardSource) => true,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                canNoSelect: () => false,
                                selectCardCoroutine: null,
                                afterSelectCardCoroutine: AfterSelectCardCoroutine1,
                                message: "Specify the order to place the cards in the digivolution cards\n(cards will be placed so that cards with lower numbers are on top).",
                                maxCount: selectedCards.Count,
                                canEndNotMax: false,
                                isShowOpponent: false,
                                mode: SelectCardEffect.Mode.Custom,
                                root: SelectCardEffect.Root.Custom,
                                customRootCardList: selectedCards,
                                canLookReverseCard: true,
                                selectPlayer: card.Owner,
                                cardEffect: activateClass);

                            selectCardEffect.SetUpCustomMessage_ShowCard("Digivolution Cards");

                            await selectCardEffect.Activate();

                            Task AfterSelectCardCoroutine1(List<CardSource> cardSources)
                            {
                                foreach (CardSource cardSource in cardSources)
                                {
                                    digivolutionCards.Add(cardSource);
                                }

                                return Task.CompletedTask;
                            }
                        }

                        if (digivolutionCards.Count == 5)
                        {
                            await ICardEffect.ResolvePermanentOfThisCard(card).AddDigivolutionCardsBottom(digivolutionCards, activateClass.EffectSourceCard?.InstanceId);

                            #region treat as blue level 5 Digimon

                            CardSource topCard = ICardEffect.ResolvePermanentOfThisCard(card).TopCard;

                            #region treat as blue
                            ChangeCardColorClass changeCardColorClass = new ChangeCardColorClass();
                            changeCardColorClass.SetUpICardEffect($"Treated as blue", CanUseCondition1, card);
                            changeCardColorClass.SetUpChangeCardColorClass(ChangeCardColors: ChangeCardColors);
                            changeCardColorClass.SetNotShowUI(true);

                            bool CanUseCondition1(Hashtable hashtable)
                            {
                                if (CardEffectCommons.IsExistOnBattleArea(card))
                                {
                                    if (card == topCard)
                                    {
                                        if (topCard == ICardEffect.ResolvePermanentOfThisCard(card).TopCard)
                                        {
                                            return true;
                                        }
                                    }
                                }

                                return false;
                            }

                            List<CardColor> ChangeCardColors(CardSource cardSource, List<CardColor> CardColors)
                            {
                                if (cardSource == card)
                                {
                                    CardColors.Add(CardColor.Blue);
                                }

                                return CardColors;
                            }
                            #endregion

                            #region treat as level 5
                            ChangePermanentLevelClass changePermanentLevelClass = new ChangePermanentLevelClass();
                            changePermanentLevelClass.SetUpICardEffect($"Treated as level 5", CanUseCondition1, card);
                            changePermanentLevelClass.SetUpChangePermanentLevelClass(GetLevel: GetLevel);
                            changePermanentLevelClass.SetNotShowUI(true);

                            int GetLevel(Permanent permanent, int level)
                            {
                                if (CardEffectCommons.IsExistOnBattleArea(card))
                                {
                                    if (permanent == ICardEffect.ResolvePermanentOfThisCard(card))
                                    {
                                        level = 5;
                                    }
                                }

                                return level;
                            }
                            #endregion

                            #region treat as Digimon
                            TreatAsDigimonClass treatAsDigimonClass = new TreatAsDigimonClass();
                            treatAsDigimonClass.SetUpICardEffect($"Treated as Digimon", CanUseCondition1, card);
                            treatAsDigimonClass.SetUpTreatAsDigimonClass(permanentCondition: PermanentCondition);
                            treatAsDigimonClass.SetNotShowUI(true);

                            bool PermanentCondition(Permanent permanent)
                            {
                                if (CardEffectCommons.IsExistOnBattleArea(card))
                                {
                                    if (permanent == ICardEffect.ResolvePermanentOfThisCard(card))
                                    {
                                        return true;
                                    }
                                }

                                return false;
                            }
                            #endregion

                            #region treat as not having DP(not to show on UI)
                            DontHaveDPClass dontHaveDPClass = new DontHaveDPClass();
                            dontHaveDPClass.SetUpICardEffect("Doesn't have DP", CanUseCondition1, card);
                            dontHaveDPClass.SetUpDontHaveDPClass(PermanentCondition: PermanentCondition);
                            dontHaveDPClass.SetNotShowUI(true);
                            #endregion

                            List<Func<EffectTiming, ICardEffect>> GetCardEffects = new List<Func<EffectTiming, ICardEffect>>()
                            {
                                (_timing) => changeCardColorClass,
                                (_timing) => changePermanentLevelClass,
                                (_timing) => treatAsDigimonClass,
                                (_timing) => dontHaveDPClass,
                            };

                            foreach (Func<EffectTiming, ICardEffect> GetCardEffect in GetCardEffects)
                            {
                                ICardEffect.ResolvePermanentOfThisCard(card).PermanentEffects.Add(GetCardEffect);
                            }

                            ICardEffect.ResolvePermanentOfThisCard(card).IsPlaceToTrashDueToNotHavingDP = false;

                            #endregion

                            await CardEffectCommons.DigivolveIntoHandOrTrashCard(
                                                targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),
                                                cardCondition: CanSelectCardCondition1,
                                                payCost: true,
                                                reduceCostTuple: null,
                                                fixedCostTuple: null,
                                                ignoreDigivolutionRequirementFixedCost: -1,
                                                isHand: true,
                                                activateClass: activateClass,
                                                successProcess: null);

                            #region release effects

                            if (CardEffectCommons.IsExistOnBattleArea(card))
                            {
                                foreach (Func<EffectTiming, ICardEffect> GetCardEffect in GetCardEffects)
                                {
                                    ICardEffect.ResolvePermanentOfThisCard(card).PermanentEffects.Remove(GetCardEffect);
                                }

                                ICardEffect.ResolvePermanentOfThisCard(card).IsPlaceToTrashDueToNotHavingDP = true;
                            }

                            #endregion
                        }
                    }
                }
            }
        }
        #endregion

        #region Your Turn - Memory +1 and gain unblockable
        if (timing == EffectTiming.OnAddHand)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1 and gain unblockable", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("Memory+1_BT7_087");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn][Once Per Turn] When an effect adds a card to your hand, gain 1 memory. Then, this Digimon can't be blocked for the turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        // AS-IS `CanTriggerOnHandAdded(hashtable, card.Owner, null)` — 미러의 AS-IS-형 Hashtable
                        // 오버로드(OnCardsAddedToHand.cs): (Hashtable, Player, Func<ICardEffect,bool>). `card.Owner`
                        // (AS-IS Player) → `new Player(card.Context, card.Owner)`.
                        if (CardEffectCommons.CanTriggerOnHandAdded(hashtable, new Player(card.Context, card.Owner), null))
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
                await card.Owner.AddMemory(1, activateClass);

                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                // (J-4) 미러 GainCanNotBeBlocked는 동기 bool(효과 즉시 부착) — AS-IS StartCoroutine 래핑을 대체.
                CardEffectCommons.GainCanNotBeBlocked(
                    targetPermanent: selectedPermanent,
                    defenderCondition: null,
                    effectDuration: EffectDuration.UntilEachTurnEnd,
                    sourceCard: card,
                    effectName: "Unblockable");

                await Task.CompletedTask;
            }
        }
        #endregion

        return cardEffects;
    }
}
