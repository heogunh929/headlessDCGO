// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S3 카드 — EX7_058 (Digimon / Purple)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX7/Purple/EX7_058.cs (481 lines, no region markers list below)
//    * Digivolution Condition :15-24  (timing None — AddSelfDigivolutionRequirementStaticEffect, LadyDevimon)
//    * On Play               :47-195 (AS-IS timing == OnEnterFieldAnyone + CanTriggerOnPlay gate)
//    * When Digivolving      :198-346(AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving gate —
//                                     **미러 방언 재배선**: WhenDigivolving 전용 키로 이동, 이중-키 등록 금지)
//    * Inherit               :349-477(timing == OnDestroyedAnyone — Once per turn, play lvl4- purple Digimon
//                                     from trash when an opponent's Digimon is deleted)
//
// ② 프리미티브 매핑:
//    * P:AddSelfDigivolutionRequirementStaticEffect, P:PlayVoleeZerdrucken, P:PlayPermanentCards,
//      P:CanPlayAsNewPermanent, P:CanTriggerOnPermanentDeleted, P:CanTriggerOnEndAttack
//
// ③ 배선 관례 근거:
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(trigger-wiring rule 3, §2.7): AS-IS는
//      On Play와 동일한 EffectTiming.OnEnterFieldAnyone에 두 ActivateClass를 CanUseCondition(CanTriggerOnPlay
//      vs CanTriggerWhenDigivolving)만으로 구분해서 등재하지만, 미러 DigivolveAction은 WhenDigivolving
//      전용 키만 해소하므로 재배선 필수.
//    * [Opponent's Turn][Once per turn] Inherit → OnDestroyedAnyone(AS-IS 타이밍 그대로) + SetHashString +
//      SetIsInheritedEffect(true).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`/
//      `yield return StartCoroutine(X)` → `await X` (BT8_092 idiom).
//    * `GManager.instance.GetComponent<Effects>().CreateDebuffEffect(permanent)` = UI 연출 — 스트립(§2.6).
//      두 호출 모두 상태효과와 무관(permanent.UntilOwnerTurnEndEffects.Add는 가드 밖에서 무조건 실행되므로
//      보존할 가드 술어 없음).
//    * `CardEffectCommons.PlayVoleeZerdrucken(activateClass)` — AS-IS-signature bridge 오버로드 실존
//      (PlayCardsBridge.cs:345, ICardEffect 인자) — 그대로.
//    * `CardEffectCommons.HasMatchConditionPermanent(CanSelectPermanentCondition)`(구 card-less 폼) →
//      `HasMatchConditionPermanent(card, CanSelectPermanentCondition)`(§2.3, leading card 파라미터 추가).
//    * `CardEffectCommons.MatchConditionPermanentCount(CanSelectPermanentCondition)` →
//      `MatchConditionPermanentCount(card, CanSelectPermanentCondition)`(§2.3, Permanent 오버로드 착지 +
//      leading card). SelectPermanentEffect.canTargetCondition도 정본 Func<Permanent,bool> — 술어 직결.
//    * `card.Owner.TrashCards.Count(cond)` → `new Player(card.Context, card.Owner).TrashCards.Count(cond)`.
//    * `new DestroyPermanentsClass(list, CardEffectCommons.CardEffectHashtable(activateClass1)).Destroy()` —
//      두 심볼 모두 실존(CardController.cs:4644, CardEffectCommons/HashtableSetting.cs:16) 그대로.
//    * `CardEffectCommons.CanTriggerOnEndAttack(hashtable1, permanent.TopCard)` — AS-IS-signature Hashtable
//      오버로드 실존(CanUseEffects/OnEndAttack.cs:12) 그대로.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX7.Purple;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX7_058 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Digivolution Condition
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.CardNames.Contains("LadyDevimon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 0, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region Shared On Play/When Digivolving
        bool CanSelectPermanentCondition(Permanent permanent)
        {
            return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
        }

        bool CanActivateSecondEffectCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
            {
                if (ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count((cardSource) => cardSource.CardNames.Contains("LadyDevimon") || cardSource.CardNames.Contains("X Antibody") || cardSource.CardNames.Contains("XAntibody")) >= 1)
                {
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region On Play
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Give effects to opponent's Digimon and play a Token", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] 1 of your opponent's Digimon gains \"[End of Attack] Delete this Digimon.\" until the end of their turn. Then, if this Digimon has [LadyDevimon]/[X Antibody] in its digivolution cards, you may play 1 [Volée & Zerdrücken] Token (Digimon/Lv.4/Purple/5000 DP/<Blocker>/<Retaliation>).";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                    return CardEffectCommons.CanTriggerOnPlay(hashtable, card);

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
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

                    selectPermanentEffect.SetUpCustomMessage(
                        "Select 1 Digimon that will get [End of Attack] Delete this Digimon.",
                        "The opponent is selecting 1 Digimon that will get [End of Attack] Delete this Digimon.");

                    await selectPermanentEffect.Activate();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        if (permanent != null)
                        {
                            ActivateClass activateClass1 = new ActivateClass();
                            activateClass1.SetUpICardEffect("Delete this Digimon", CanUseCondition1, permanent.TopCard);
                            activateClass1.SetUpActivateClass(CanActivateCondition1, ActivateCoroutine1, -1, false, EffectDiscription1());
                            activateClass1.SetEffectSourcePermanent(permanent);
                            permanent.UntilOwnerTurnEndEffects.Add(GetCardEffect);

                            string EffectDiscription1()
                            {
                                return "[End of Attack] Delete this Digimon.";
                            }

                            bool CanUseCondition1(Hashtable hashtable1)
                            {
                                if (CardEffectCommons.IsOpponentTurn(card))
                                {
                                    if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, permanent.TopCard))
                                    {
                                        if (CardEffectCommons.CanTriggerOnEndAttack(hashtable1, permanent.TopCard))
                                        {
                                            return true;
                                        }
                                    }
                                }

                                return false;
                            }

                            bool CanActivateCondition1(Hashtable hashtable1)
                            {
                                if (CardEffectCommons.IsPermanentExistsOnBattleArea(permanent))
                                {
                                    if (!permanent.TopCard.CanNotBeAffected(activateClass))
                                    {
                                        return true;
                                    }
                                }

                                return false;
                            }

                            async Task ActivateCoroutine1(Hashtable _hashtable1)
                            {
                                if (CardEffectCommons.IsPermanentExistsOnBattleArea(permanent))
                                {
                                    await new DestroyPermanentsClass(
                                    new List<Permanent>() { permanent },
                                    CardEffectCommons.CardEffectHashtable(activateClass1)).Destroy();
                                }
                            }

                            ICardEffect GetCardEffect(EffectTiming _timing)
                            {
                                if (_timing == EffectTiming.OnEndAttack)
                                {
                                    return activateClass1;
                                }

                                return null;
                            }
                        }

                        return Task.CompletedTask;
                    }
                }

                if (CanActivateSecondEffectCondition(hashtable))
                {
                    List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>()
                    {
                        new SelectionElement<bool>(message: $"Play Token", value : true, spriteIndex: 0),
                        new SelectionElement<bool>(message: $"Not Play Token", value : false, spriteIndex: 1),
                    };

                    string selectPlayerMessage = "Will you play a token?";
                    string notSelectPlayerMessage = "The opponent is choosing to play a token.";

                    GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);

                    await GManager.instance.userSelectionManager.WaitForEndSelect();

                    bool canPlay = GManager.instance.userSelectionManager.SelectedBoolValue;

                    if (canPlay)
                        await CardEffectCommons.PlayVoleeZerdrucken(activateClass);
                }
            }
        }
        #endregion

        #region When Digivolving
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Give effects to opponent's Digimon and play a Token", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] 1 of your opponent's Digimon gains \" Delete this Digimon.\" until the end of their turn. Then, if this Digimon has [LadyDevimon]/[X Antibody] in its digivolution cards, you may play 1 [Volée & Zerdrücken] Token (Digimon/Lv.4/Purple/5000 DP/<Blocker>/<Retaliation>).";
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

            async Task ActivateCoroutine(Hashtable hashtable)
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

                    selectPermanentEffect.SetUpCustomMessage(
                        "Select 1 Digimon that will get [End of Attack] Delete this Digimon.",
                        "The opponent is selecting 1 Digimon that will get [End of Attack] Delete this Digimon.");

                    await selectPermanentEffect.Activate();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        if (permanent != null)
                        {
                            ActivateClass activateClass1 = new ActivateClass();
                            activateClass1.SetUpICardEffect("Delete this Digimon", CanUseCondition1, permanent.TopCard);
                            activateClass1.SetUpActivateClass(CanActivateCondition1, ActivateCoroutine1, -1, false, EffectDiscription1());
                            activateClass1.SetEffectSourcePermanent(permanent);
                            permanent.UntilOwnerTurnEndEffects.Add(GetCardEffect);

                            string EffectDiscription1()
                            {
                                return "[End of Attack] Delete this Digimon.";
                            }

                            bool CanUseCondition1(Hashtable hashtable1)
                            {
                                if (CardEffectCommons.IsOpponentTurn(card))
                                {
                                    if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, permanent.TopCard))
                                    {
                                        if (CardEffectCommons.CanTriggerOnEndAttack(hashtable1, permanent.TopCard))
                                        {
                                            return true;
                                        }
                                    }
                                }

                                return false;
                            }

                            bool CanActivateCondition1(Hashtable hashtable1)
                            {
                                if (CardEffectCommons.IsPermanentExistsOnBattleArea(permanent))
                                {
                                    if (!permanent.TopCard.CanNotBeAffected(activateClass))
                                    {
                                        return true;
                                    }
                                }

                                return false;
                            }

                            async Task ActivateCoroutine1(Hashtable _hashtable1)
                            {
                                if (CardEffectCommons.IsPermanentExistsOnBattleArea(permanent))
                                {
                                    await new DestroyPermanentsClass(
                                    new List<Permanent>() { permanent },
                                    CardEffectCommons.CardEffectHashtable(activateClass1)).Destroy();
                                }
                            }

                            ICardEffect GetCardEffect(EffectTiming _timing)
                            {
                                if (_timing == EffectTiming.OnEndAttack)
                                {
                                    return activateClass1;
                                }

                                return null;
                            }
                        }

                        return Task.CompletedTask;
                    }
                }

                if (CanActivateSecondEffectCondition(hashtable))
                {
                    List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>()
                    {
                        new SelectionElement<bool>(message: $"Play Token", value : true, spriteIndex: 0),
                        new SelectionElement<bool>(message: $"Not Play Token", value : false, spriteIndex: 1),
                    };

                    string selectPlayerMessage = "Will you play a token?";
                    string notSelectPlayerMessage = "The opponent is choosing to play a token.";

                    GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);

                    await GManager.instance.userSelectionManager.WaitForEndSelect();

                    bool canPlay = GManager.instance.userSelectionManager.SelectedBoolValue;

                    if (canPlay)
                        await CardEffectCommons.PlayVoleeZerdrucken(activateClass);
                }
            }
        }
        #endregion

        #region Inherit
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 level 4 or lower purple Digimon from your trash", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("PlayLevel4_EX7_058");
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Opponent's Turn] [Once per turn]  When an opponent's Digimon is deleted, you may play 1 purple level 4 or lower Digimon card from your trash without paying the cost.";
            }

            bool CanSelectCardInTrash(CardSource cardSource)
            {
                if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                {
                    if (cardSource.HasCardColor("Purple"))
                    {
                        if (cardSource.IsDigimon)
                        {
                            if (cardSource.Level <= 4)
                            {
                                if (cardSource.HasLevel)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.IsOpponentTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerOnPermanentDeleted(hashtable, PermanentCondition))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.IsDigimon)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, (cardSource) => CanSelectCardInTrash(cardSource)))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardInTrash))
                {
                    int maxCount = Math.Min(1, new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardInTrash));

                    List<CardSource> selectedCards = new List<CardSource>();

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                                canTargetCondition: CanSelectCardInTrash,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                canNoSelect: () => true,
                                selectCardCoroutine: SelectCardCoroutine,
                                afterSelectCardCoroutine: null,
                                message: "Select 1 card to play.",
                                maxCount: maxCount,
                                canEndNotMax: false,
                                isShowOpponent: true,
                                mode: SelectCardEffect.Mode.Custom,
                                root: SelectCardEffect.Root.Trash,
                                customRootCardList: null,
                                canLookReverseCard: true,
                                selectPlayer: card.Owner,
                                cardEffect: activateClass);

                    selectCardEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                    selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                    await selectCardEffect.Activate();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);

                        return Task.CompletedTask;
                    }

                    await CardEffectCommons.PlayPermanentCards(
                        cardSources: selectedCards,
                        activateClass: activateClass,
                        payCost: false,
                        isTapped: false,
                        root: SelectCardEffect.Root.Trash,
                        activateETB: true);
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
