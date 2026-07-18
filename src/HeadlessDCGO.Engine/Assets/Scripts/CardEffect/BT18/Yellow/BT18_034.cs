// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// PILOT-S1 카드 — BT18_034 (Digimon / Yellow)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT18/Yellow/BT18_034.cs (411 lines, 4 regions)
//    * Digivolution Condition     :16-31  (timing == None — AddSelfDigivolutionRequirementStaticEffect)
//    * Start of Your Main Phase   :34-149 (timing == OnStartMainPhase — 손패 1장 트래시→상대 시큐리티 트래시 or
//      <Recovery +1>)
//    * On Play                    :152-259 (timing == OnEnterFieldAnyone — Start-of-Main과 동일 몸통)
//    * End of Your Turn           :262-406 (timing == OnEndTurn — Lv.6를 시큐리티 위에 두고 [Lucemon: Chaos Mode]
//      로 무료 진화)
//
// ② 프리미티브 매핑:
//    * P:AddDigivolutionRequirementClass(AddSelfDigivolutionRequirementStaticEffect) — [Cupimon] 위 코스트 5
//      (AS-IS :18-29)
//    * E:SelectHandEffect Mode.Discard + userSelectionManager(bool) + P:IRecovery/P:IDestroySecurity —
//      [Start of Your Main Phase]/[On Play] 동일 몸통 (AS-IS :74-146 / :182-256)
//    * E:SelectPermanentEffect Mode.Custom + P:PlacePermanentInSecurityAndProcessAccordingToResult +
//      P:DigivolveIntoHandOrTrashCard — [End of Your Turn] (AS-IS :330-403)
//
// ③ 배선 관례 근거:
//    * [Start of Your Main Phase] → OnStartMainPhase + IsExistOnBattleAreaDigimon && IsOwnerTurn (AS-IS :46-56
//      그대로).
//    * [On Play] → OnEnterFieldAnyone + CanTriggerOnPlay(hashtable, card) 게이트(AS-IS :164 그대로).
//    * [End of Your Turn] → OnEndTurn, Once Per Turn(SetHashString), isOptional true — AS-IS 그대로.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return StartCoroutine(X)`/`yield return
//      ContinuousController.instance.StartCoroutine(X)`→`await X` (BT17_026 idiom); lone `yield return null`→
//      Task.CompletedTask (BT8_092 idiom).
//    * `GManager.instance.userSelectionManager.SetBool/SetBoolSelection/WaitForEndSelect/SelectedBoolValue` —
//      미러에 동일 표면 존재(UserSelectionManager.cs, BT1_056/BT1_111 확립 idiom) 그대로 사용.
//    * `new IRecovery(card.Owner, 1, activateClass).Recovery()` → 미러 ctor(CardController.cs:409)
//      `new IRecovery(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Recovery()`
//      (BT2_034/BT14_030 idiom).
//    * `new IDestroySecurity(player: card.Owner.Enemy, destroySecurityCount: 1, cardEffect: activateClass,
//      fromTop: true)` → 미러 ctor(CardController.cs:669) `new IDestroySecurity(card.Context, new Player(
//      card.Context, card.Owner).Enemy!.PlayerId, 1, activateClass, fromTop: true)` (AD1_025 idiom).
//    * `permanent.TopCard.IsLevel6` — 미러 CardSource에 해당 프로퍼티 없음(AS-IS CardSource.cs:2941 게터:
//      `HasLevel && Level == 6`). 이미 존재하는 HasLevel/Level 1차 프리미티브로 그 게터 몸통을 인라인 조립
//      (발명 아님 — EX8_028의 Level==5 인라인과 동일 성격).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (BT8_092 관례).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT18.Yellow;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT18_034 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Digivolution Condition
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsCardName("Cupimon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition,
                digivolutionCost: 5,
                ignoreDigivolutionRequirement: false,
                card: card,
                condition: null)
            );
        }
        #endregion

        #region Start of Your Main Phase
        if (timing == EffectTiming.OnStartMainPhase)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash 1 card from hand to trash the opponents top security or Recovery +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Start of Your Main Phase] By trashing 1 card in your hand, your opponent may trash their top security card. If this effect didn't trash, <Revovery +1>";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
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
                    if (new Player(card.Context, card.Owner).HandCards.Count >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (new Player(card.Context, card.Owner).HandCards.Count >= 1)
                {
                    bool discarded = false;

                    int discardCount = 1;

                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: (cardSource) => true,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: discardCount,
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
                            discarded = true;

                            await Task.CompletedTask;
                        }
                    }

                    if (discarded)
                    {
                        if (new Player(card.Context, card.Owner).Enemy!.SecurityCards.Count == 0)
                        {
                            GManager.instance.userSelectionManager.SetBool(false);
                        }
                        else
                        {
                            List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>()
                            {
                                new SelectionElement<bool>(message: $"Discard", value : true, spriteIndex: 0),
                                new SelectionElement<bool>(message: $"Not Discard", value : false, spriteIndex: 1),
                            };

                            string selectPlayerMessage = "Will you discard the top card of your security?";
                            string notSelectPlayerMessage = "The opponent is choosing whether to discard security.";

                            GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements, selectPlayer: new Player(card.Context, card.Owner).Enemy!.PlayerId, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);
                        }

                        await GManager.instance.userSelectionManager.WaitForEndSelect();

                        bool doDiscard = GManager.instance.userSelectionManager.SelectedBoolValue;

                        if (!doDiscard)
                        {
                            await new IRecovery(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Recovery();
                        }

                        else
                        {
                            await new IDestroySecurity(
                                card.Context,
                                new Player(card.Context, card.Owner).Enemy!.PlayerId,
                                1,
                                activateClass,
                                fromTop: true).DestroySecurity();
                        }
                    }
                }
            }
        }
        #endregion

        #region On Play
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash 1 card from hand to trash the opponents top security or Recovery +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] By trashing 1 card in your hand, your opponent may trash their top security card. If this effect didn't trash, <Revovery +1>";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (new Player(card.Context, card.Owner).HandCards.Count >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (new Player(card.Context, card.Owner).HandCards.Count >= 1)
                {
                    bool discarded = false;

                    int discardCount = 1;

                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: (cardSource) => true,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: discardCount,
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
                            discarded = true;

                            await Task.CompletedTask;
                        }
                    }

                    if (discarded)
                    {
                        if (new Player(card.Context, card.Owner).Enemy!.SecurityCards.Count == 0)
                        {
                            GManager.instance.userSelectionManager.SetBool(false);
                        }
                        else
                        {
                            List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>()
                            {
                                new SelectionElement<bool>(message: $"Discard", value : true, spriteIndex: 0),
                                new SelectionElement<bool>(message: $"Not Discard", value : false, spriteIndex: 1),
                            };

                            string selectPlayerMessage = "Will you discard the top card of your security?";
                            string notSelectPlayerMessage = "The opponent is choosing whether to discard security.";

                            GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements, selectPlayer: new Player(card.Context, card.Owner).Enemy!.PlayerId, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);
                        }

                        await GManager.instance.userSelectionManager.WaitForEndSelect();

                        bool doDiscard = GManager.instance.userSelectionManager.SelectedBoolValue;

                        if (!doDiscard)
                        {
                            await new IRecovery(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Recovery();
                        }

                        else
                        {
                            await new IDestroySecurity(
                                card.Context,
                                new Player(card.Context, card.Owner).Enemy!.PlayerId,
                                1,
                                activateClass,
                                fromTop: true).DestroySecurity();
                        }
                    }
                }
            }
        }
        #endregion

        #region End of Your Turn
        if (timing == EffectTiming.OnEndTurn)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Place one of your level 6 Digimon on top of your security stack to digivolve into [Lucemon: Chaos Mode] in the trash without paying the cost.", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDescription());
            activateClass.SetHashString("EOT_BT18-034");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[End of Your Turn] [Once Per Turn] By placing one of your level 6 Digimon on top of your security stack, this Digimon may digivolve into [Lucemon: Chaos Mode] in the trash without paying the cost.";
            }

            bool IsPermanentLevel6Condition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    // AS-IS `permanent.TopCard.IsLevel6` (CardSource.cs:2941: HasLevel && Level == 6) —
                    // 미러 프로퍼티 없음, 인라인 조립(발명 아님 — EX8_028의 Level==5 동일 성격).
                    if (permanent.TopCard.HasLevel && permanent.TopCard.Level == 6)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool IsCardLucemonChaosModeCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.ContainsCardName("Lucemon: Chaos Mode") || cardSource.ContainsCardName("Lucemon:ChaosMode") || cardSource.ContainsCardName("Lucemon Chaos Mode") || cardSource.ContainsCardName("LucemonChaosMode"))
                    {
                        if (cardSource.CanEvolve(ICardEffect.ResolvePermanentOfThisCard(card), true))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            Permanent? PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            bool IsPermanentLevel6ConditionById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && IsPermanentLevel6Condition(permanent);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
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
                    if (CardEffectCommons.HasMatchConditionPermanent(card, IsPermanentLevel6Condition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, IsPermanentLevel6Condition))
                {
                    Permanent? selectedPermanent = null;

                    int maxCount = Math.Min(1,
                        CardEffectCommons.MatchConditionPermanentCount(card, IsPermanentLevel6ConditionById));

                    SelectPermanentEffect selectPermanentEffect =
                        GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsPermanentLevel6ConditionById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage(
                        "Select 1 Digimon to place on top of your security stack.",
                        "The opponent is selecting 1 Digimon to place on top of their security stack.");

                    await selectPermanentEffect.Activate();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanent = permanent;
                        return Task.CompletedTask;
                    }

                    if (selectedPermanent != null)
                    {
                        if (selectedPermanent.TopCard != null)
                        {
                            if (!selectedPermanent.TopCard.CanNotBeAffected(activateClass))
                            {
                                CardSource securityCard = selectedPermanent.TopCard;

                                await CardEffectCommons.PlacePermanentInSecurityAndProcessAccordingToResult(
                                    selectedPermanent,
                                    activateClass,
                                    true,
                                    SuccessProcess);

                                async Task SuccessProcess(CardSource cardSource)
                                {
                                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, IsCardLucemonChaosModeCondition))
                                    {
                                        await CardEffectCommons.DigivolveIntoHandOrTrashCard(
                                            targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),
                                            cardCondition: IsCardLucemonChaosModeCondition,
                                            payCost: false,
                                            reduceCostTuple: null,
                                            fixedCostTuple: null,
                                            ignoreDigivolutionRequirementFixedCost: -1,
                                            isHand: false,
                                            activateClass: activateClass,
                                            successProcess: null);
                                    }
                                }
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
