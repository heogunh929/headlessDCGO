// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 포팅 카드 — BT25_094 (Digimon / Red, Cosmic Area)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Red/BT25_094.cs (258 lines, 5 regions)
//    * Ignore Color Requirement :15-35  (timing None — IgnoreColorConditionClass, "0 unflipped security" gate)
//    * Alliance                 :39-56  (timing OnAllyAttack — AllianceStaticEffect, gated IsExistInSecurity)
//    * Rush                     :58-83  (timing None — RushStaticEffect, gated IsExistInSecurity + Apollomon/Dianamon)
//    * [Main]                   :87-143 (OptionSkill — ReplaceBottomSecurityWithFaceUpOptionEffect then
//                                          play 1 red/blue [TS] Digimon from hand at cost -3)
//    * [Security]               :145-253(SecuritySkill — play 1 lvl4- red/blue [TS] Digimon from hand/trash free)
//
// ② 프리미티브 매핑:
//    * P:IgnoreColorConditionClass, P:AllianceStaticEffect(CardEffectFactory), P:RushStaticEffect(CardEffectFactory)
//    * P:ReplaceBottomSecurityWithFaceUpOptionEffect — [Main] 몸통 1문(AS-IS :116). **RESOLVED: 미러 substrate가
//      폴리시 아크에서 이식됨(CardEffectFactory.cs:259-269, RD-P6C3-B1 UN-STOP). 구 "NotSupportedException 스텁"
//      주석은 stale이라 정정(BT21_030 자기-정정 판례). [Main] 활성화가 실행되며 Replace 경로 실착지 —
//      witness LT-B BT25_094 W2(Replace flip). BT25_102 선례.**
//    * E:SelectHandEffect Mode.PlayForCost(코스트-3)/Mode.PlayForFree, E:SelectCardEffect Mode.PlayForFree Root.Trash
//    * userSelectionManager SetIntSelection/SetInt/WaitForEndSelect/SelectedIntValue — hand↔trash 분기(§2.6 kept-UI)
//
// ③ 배선 관례 근거:
//    * Alliance → OnAllyAttack + IsExistInSecurity. Rush → None + IsExistInSecurity&Apollomon/Dianamon(AS-IS 그대로).
//    * [Main] → OptionSkill + CanTriggerOptionMainEffect. [Security] → SecuritySkill + CanTriggerSecurityEffect.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`.
//    * `card.Owner.SecurityCards`/`HandCards` → `new Player(card.Context, card.Owner).*` (미러 Player 접근 idiom).
//    * `cardSource.HasCardColor(CardColor.Red/Blue)`(enum) → `cardSource.HasCardColor("Red"/"Blue")`
//      (미러 string-색 idiom); `permanent.TopCard.CardColors.Contains(CardColor.Red/Blue)` →
//      `.CardColors.Contains("Red"/"Blue")` (미러 CardColors=IReadOnlyList<string> — BT25_102 idiom).
//    * `permanent.TopCard.HasTSTraits` — 미러 실존(EqualsTraits("TS") 파생) 그대로.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Red;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_094 : CEntity_Effect
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
                // (SEC-FaceUpSecuritySource) AS-IS `!cardSource.IsFlipped` face gate — the mirror stamps security
                // face state via SecurityFaceState (never the raw field-ACE flag; Permanent.cs FoldLinkedMax
                // precedent, commit 40d1eaee). Gate TRUE when zero face-UP security cards.
                return new Player(card.Context, card.Owner).SecurityCards.Count(cardSource => Headless.Runtime.SecurityFaceState.IsFaceUpInSecurity(card.Context, cardSource.InstanceId)) == 0;
            }

            bool CardCondition(CardSource cardSource)
            {
                return cardSource == card;
            }
        }

        #endregion

        #region All Turns - Security

        #region Alliance
        if (timing == EffectTiming.OnAllyAttack)
        {
            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                    && (permanent.TopCard.CardColors.Contains("Red") || permanent.TopCard.CardColors.Contains("Blue"))
                    && permanent.TopCard.HasTSTraits;
            }

            bool CanUseCondition()
            {
                return CardEffectCommons.IsExistInSecurity(card, false);
            }

            cardEffects.Add(CardEffectFactory.AllianceStaticEffect(PermanentCondition, false, card, CanUseCondition));
        }
        #endregion

        #region Rush
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                    && (permanent.TopCard.CardColors.Contains("Red") || permanent.TopCard.CardColors.Contains("Blue"))
                    && permanent.TopCard.HasTSTraits;
            }

            bool HasOXII(Permanent permanent)
            {
                return permanent.TopCard.EqualsCardName("Apollomon")
                    || permanent.TopCard.EqualsCardName("Dianamon");
            }

            bool Condition()
            {
                return CardEffectCommons.IsExistInSecurity(card, false)
                    && CardEffectCommons.HasMatchConditionOwnersPermanent(card, HasOXII);
            }

            cardEffects.Add(CardEffectFactory.RushStaticEffect(PermanentCondition, false, card, Condition));
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
                return "[Main] Add your bottom security card to the hand and place this card face up as the bottom security card. Then, you may play 1 red or blue [TS] trait Digimon card from your hand with the play cost reduced by 3.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.IsDigimon
                    && (cardSource.HasCardColor("Red") || cardSource.HasCardColor("Blue"))
                    && cardSource.HasTSTraits
                    && CardEffectCommons.CanPlayAsNewPermanent(cardSource, true, activateClass, fixedCost: cardSource.GetCostItself - 3);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                // AS-IS :116 ReplaceBottomSecurityWithFaceUpOptionEffect — 1:1 mirror, now PORTED (RD-P6C3-B1
                // UN-STOP, CardEffectFactory.cs:259-269). This activation runs; the earlier "throws
                // NotSupportedException" note was stale and is struck. Witnessed: LT-B BT25_094 W2 (Replace flip).
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
            activateClass.SetUpICardEffect($"Play 1 lvl 4- Red or Blue [TS] Digimon card from hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
             => "[Security] You may play 1 level 4 or lower red or blue [TS] trait Digimon card from your hand or trash without paying the cost.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            bool CanPlayCondition(CardSource cardSource)
            {
                return cardSource.IsDigimon && cardSource.HasLevel && cardSource.Level <= 4
                    && (cardSource.HasCardColor("Red") || cardSource.HasCardColor("Blue"))
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
