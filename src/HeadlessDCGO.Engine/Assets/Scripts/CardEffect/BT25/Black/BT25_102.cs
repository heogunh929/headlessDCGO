// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S3 카드 — BT25_102 (Digimon / Black, Factorial Area)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Black/BT25_102.cs (253 lines, 5 regions)
//    * Ignore Color Requirement :17-33  (timing None — IgnoreColorConditionClass, "0 unflipped security" gate)
//    * All Turns - Security      :39-80  (timing None — Blocker + Link+1, both gated IsExistInSecurity)
//    * [Main]                    :85-136 (OptionSkill — ReplaceBottomSecurityWithFaceUpOptionEffect then
//                                          play 1 black/red [TS] Digimon from hand at cost -3)
//    * [Security]                :142-246(SecuritySkill — play 1 lvl4- black/red [TS] Digimon from hand/trash free)
//
// ② 프리미티브 매핑:
//    * P:IgnoreColorConditionClass, P:BlockerStaticEffect, P:ChangeLinkMaxStaticEffect
//    * P:ReplaceBottomSecurityWithFaceUpOptionEffect — [Main] 몸통 1문 (AS-IS :111). **주의: 미러 substrate
//      스텁이 이미 NotSupportedException으로 선언되어 있음(CardEffectFactory.cs:244-250, design item
//      RD-P6C3-B1 — ContinuousController/CardObjectController.AddHandCards+AddSecurityCard/CreateRecoveryEffect
//      미포팅). 공용층 수정 금지 규칙상 이 스텁은 그대로 두고 AS-IS 그대로 호출 — 등재 팔(CanUseCondition 등)은
//      실배선, ActivateCoroutine은 실행 시 이 지점에서 낙뢰(loud throw)한다. STOP 대상은 이 substrate 갭이며
//      카드 자체가 아니다(레지스트리/게이팅 로직은 100% 이식).**
//
// ③ 배선 관례 근거:
//    * [Main] → OptionSkill + CanTriggerOptionMainEffect. [Security] → SecuritySkill + CanTriggerSecurityEffect.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)` → `await X`.
//    * `card.Owner.SecurityCards`/`HandCards` → `new Player(card.Context, card.Owner).*`.
//    * `cardSource.HasDigimonColor(CardColor.Black/Red)`(enum) → `cardSource.HasDigimonColor("Black"/"Red")`
//      (미러 string-색 idiom).
//    * `permanent.TopCard.CardColors.Contains(CardColor.Black/Red)` → `.CardColors.Contains("Black"/"Red")`.
//    * `permanent.TopCard.HasTSTraits` — 미러 실존(EqualsTraits("TS") 파생, CardSource.cs 확인) 그대로.
//    * `card.Owner.HandCards.Count(cond)` → `new Player(...).HandCards.Count(cond)`.
//    * GManager.instance.userSelectionManager.SetIntSelection/WaitForEndSelect/SelectedIntValue — 확립 UI 표면
//      그대로 유지(§2.6 kept-UI-decorators).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Black;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_102 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Ignore Color Requirement

        if (timing == EffectTiming.None)
        {
            IgnoreColorConditionClass ignoreColorConditionClass = new IgnoreColorConditionClass();
            ignoreColorConditionClass.SetUpICardEffect("Ignore color requirements", CanUseCondition, card);
            ignoreColorConditionClass.SetUpIgnoreColorConditionClass(cardCondition: CardCondition);
            cardEffects.Add(ignoreColorConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return new Player(card.Context, card.Owner).SecurityCards.Count(cardSource => !cardSource.IsFlipped) == 0;
            }

            bool CardCondition(CardSource cardSource)
            {
                return cardSource == card;
            }
        }

        #endregion

        #region All Turns - Security

        bool SecurityPermanentCondition(Permanent permanent)
        {
            return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                && (permanent.TopCard.CardColors.Contains("Black") || permanent.TopCard.CardColors.Contains("Red"))
                && permanent.TopCard.HasTSTraits;
        }

        #region Blocker
        if (timing == EffectTiming.None)
        {
            bool CanUseCondition()
            {
                return CardEffectCommons.IsExistInSecurity(card, false);
            }

            cardEffects.Add(CardEffectFactory.BlockerStaticEffect(permanentCondition: SecurityPermanentCondition, isInheritedEffect: false, card: card, condition: CanUseCondition));
        }
        #endregion

        #region Link +1
        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistInSecurity(card, false)
                    && CardEffectCommons.HasMatchConditionOwnersPermanent(card, HasOXII);
            }

            bool HasOXII(Permanent permanent)
            {
                return permanent.TopCard.EqualsCardName("Vulcanusmon");
            }

            cardEffects.Add(CardEffectFactory.ChangeLinkMaxStaticEffect(
                permanentCondition: SecurityPermanentCondition,
                changeValue: 1,
                isInheritedEffect: false,
                card: card,
                condition: Condition));
        }
        #endregion

        #endregion

        #region Main Effect

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Replace your bottom sec with this face-up card, play a [TS] Digimon for -3", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[Main] Add your bottom security card to the hand and place this card face up as the bottom security card. Then, you may play 1 black or red [TS] trait Digimon card from your hand with the play cost reduced by 3.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return (cardSource.HasDigimonColor("Black") || cardSource.HasDigimonColor("Red"))
                    && cardSource.HasTSTraits
                    && CardEffectCommons.CanPlayAsNewPermanent(cardSource, true, activateClass, fixedCost: cardSource.GetCostItself - 3);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                // AS-IS :111 ReplaceBottomSecurityWithFaceUpOptionEffect — substrate STOP stub
                // (CardEffectFactory.cs:244-250, design item RD-P6C3-B1). Called verbatim per no-simplification;
                // this line throws NotSupportedException at activation until the substrate is built.
                await CardEffectFactory.ReplaceBottomSecurityWithFaceUpOptionEffect(card, activateClass);

                SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();
                int maxCount = Math.Min(1, new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition));

                selectHandEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectCardCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: maxCount,
                    canNoSelect: true,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    selectCardCoroutine: null,
                    afterSelectCardCoroutine: null,
                    mode: SelectHandEffect.Mode.PlayForCost,
                    cardEffect: activateClass);

                selectHandEffect.SetReducedCostTuple((3, null));
                selectHandEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                selectHandEffect.SetUpCustomMessage_ShowCard("Played Card");

                await selectHandEffect.Activate();
            }
        }

        #endregion

        #region Security Effect

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"Play 1 lvl 4- Black or Red [TS] Digimon card from hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
             => "[Security] You may play 1 level 4 or lower black or red [TS] trait Digimon card from your hand or trash without paying the cost.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            bool CanPlayCondition(CardSource cardSource)
            {
                return (cardSource.HasDigimonColor("Black") || cardSource.HasDigimonColor("Red"))
                    && cardSource.HasLevel && cardSource.Level <= 4
                    && cardSource.HasTSTraits
                    && CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                bool canSelectHand = CardEffectCommons.HasMatchConditionOwnersHand(card, CanPlayCondition);
                bool canSelectTrash = CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanPlayCondition);

                if (canSelectHand || canSelectTrash)
                {
                    if (canSelectHand && canSelectTrash)
                    {
                        List<SelectionElement<int>> selectionElements1 = new List<SelectionElement<int>>()
                    {
                        new (message: $"From hand", value : 1, spriteIndex: 0),
                        new (message: $"From trash", value : 2, spriteIndex: 1),
                        new (message: $"Don't play", value: 3, spriteIndex: 2)
                    };

                        string selectPlayerMessage1 = "From which area will you play a card?";
                        string notSelectPlayerMessage1 = "The opponent is choosing from which area to select a card.";

                        GManager.instance.userSelectionManager.SetIntSelection(selectionElements: selectionElements1, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage1, notSelectPlayerMessage: notSelectPlayerMessage1);
                    }
                    else
                    {
                        GManager.instance.userSelectionManager.SetInt(canSelectHand ? 1 : 2);
                    }
                    await GManager.instance.userSelectionManager.WaitForEndSelect();
                    bool fromHand = GManager.instance.userSelectionManager.SelectedIntValue == 1;
                    bool fromTrash = GManager.instance.userSelectionManager.SelectedIntValue == 2;

                    if (fromHand)
                    {
                        SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                        selectHandEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanPlayCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: 1,
                            canNoSelect: true,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            selectCardCoroutine: null,
                            afterSelectCardCoroutine: null,
                            mode: SelectHandEffect.Mode.PlayForFree,
                            cardEffect: activateClass);

                        selectHandEffect.SetUpCustomMessage("Select 1 digimon to play", "The opponent is selecting 1 digimon to play");

                        await selectHandEffect.Activate();
                    }
                    if (fromTrash)
                    {
                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: CanPlayCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => true,
                            selectCardCoroutine: null,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 digimon to play",
                            maxCount: 1,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.PlayForFree,
                            root: SelectCardEffect.Root.Trash,
                            customRootCardList: null,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage("Select 1 digimon to play", "The opponent is selecting 1 digimon to play");
                        selectCardEffect.SetUpCustomMessage_ShowCard("Selected Digimon");

                        await selectCardEffect.Activate();
                    }
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
