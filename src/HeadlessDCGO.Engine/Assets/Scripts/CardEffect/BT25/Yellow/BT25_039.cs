// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S2 카드 — BT25_039 "Sirenmon" (Digimon / Yellow)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Yellow/BT25_039.cs (285 lines, 5 regions)
//    * Alternate Digivolution Requirement :14-24  (timing == None — AddSelfDigivolutionRequirementStaticEffect)
//    * End of your Turn - Security        :26-124 (timing == OnEndTurn, [Security] 존재 게이트, SetIsSkippable)
//      — [Ceresmon]을 손패에서 코스트-7로 무료성 플레이 → 배치 성공 시 이 카드를 그 밑에 진화원으로 배치할지
//        Yes/No 선택 → Yes면 배치 + IReduceSecurity(시큐리티 1장 트래시).
//    * All turns                          :127-181(timing == WhenRemoveField)
//      — 인접 [Shaman]/[Iliad] 퍼머넌트가 자기 효과 아닌 사유로 필드를 떠나려 할 때, 이 카드를 삭제해 막음.
//    * On Deletion                        :185-202(timing == OnDestroyedAnyone — OnDeletionClass 팩토리)
//    * Opponent's Turn - ESS              :206-278(timing == OnAllyAttack, Once Per Turn, isInherited)
//      — 상대 공격 시 자기 서스펜드 디지몬으로 공격 대상 전환.
//
// ② 프리미티브 매핑:
//    * P:AddSelfDigivolutionRequirementStaticEffect(level:4) — [TS] 특성 위 코스트 3 (AS-IS :17-22)
//    * P:SelectHandEffect Mode.PlayForCost + SetReducedCostTuple((7,null)) — [Ceresmon] 코스트-7 플레이
//      (AS-IS :61-81)
//    * P:AddDigivolutionCardsBottom + P:IReduceSecurity — Yes 분기 (AS-IS :112-118)
//    * P:CanTriggerWhenPermanentRemoveField + P:IsByEffect/IsOwnerEffect — [All turns] 게이트 (AS-IS :141-145)
//    * P:DeletePeremanentAndProcessAccordingToResult — 자기 삭제로 보호 (AS-IS :162)
//    * P:CardEffectFactory.OnDeletionClass — [On Deletion] 팩토리 (AS-IS :187-194)
//    * P:CanTriggerOnPermanentAttack — [Opponent's Turn] 상대-퍼머넌트 공격 트리거 게이트 (AS-IS :225)
//    * P:SelectPermanentEffect Mode.Custom + P:GManager.attackProcess.SwitchDefender — 방어자 전환 (AS-IS :271)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [End of your Turn - Security] → EffectTiming.OnEndTurn, [Security] IsExistInSecurity 게이트(AS-IS
//      :38-42 그대로).
//    * [Opponent's Turn - ESS] → EffectTiming.OnAllyAttack + CanTriggerOnPermanentAttack(hashtable,
//      IsOpponentDigimon) 게이트(AS-IS :225 그대로) + Once Per Turn(SetHashString) + isInherited.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`
//      (BT17_026 idiom).
//    * `card.Owner.CanAddSecurity(activateClass)` → `new Player(card.Context, card.Owner)
//      .CanAddSecurity(activateClass.EffectSourceCard?.InstanceId)` (Player.CanAddSecurity 1:1, MIG5/R3-W3c-4c).
//    * `playedPermanent.AddDigivolutionCardsBottom(list, activateClass)` → 미러 시그니처
//      `(list, causeEffectSourceId)` = `activateClass.EffectSourceCard?.InstanceId` (BT17_026 idiom).
//    * `new IReduceSecurity(player, ref skillInfos, activateClass)` → 미러 ctor
//      `(context, playerId, refCollector: null, cardEffect)` (BT9_043 idiom).
//    * `GManager.instance.userSelectionManager.SetBoolSelection/WaitForEndSelect/SelectedBoolValue` — 미러
//      1:1(bridge W5, UserSelectionManager.cs) — Yes/No 선택은 ModeChoice로 전송.
//    * AS-IS card-less `CanTriggerWhenPermanentRemoveField(hashtable, cond)`/`CanTriggerOnPermanentAttack
//      (hashtable, cond)` — 미러도 동일 raw-Hashtable 오버로드(WhenRemoveField.cs/OnAttack.cs) 그대로 존재.
//    * `IsByEffect(hashtable, cardEffect => IsOwnerEffect(cardEffect, card))` — 미러 IsOwnerEffect는
//      `(CardSource? effectSourceCard, CardSource card)` 시그니처(헤더 "AS-IS의 ICardEffect.EffectSourceCard로
//      번역" 명시, CardEffectCommons.cs:4155-4157) — `cardEffect => IsOwnerEffect(cardEffect.EffectSourceCard,
//      card)`로 어댑트.
//    * AS-IS :166-176 `permanent.willBeRemoveField = false; HideDeleteEffect(); HideHandBounceEffect();
//      HideDeckBounceEffect(); HideWillRemoveFieldEffect();` — Hide* 4개는 UI 연출(미러 확립 관례상 스트립,
//      Barrier.cs/Evade.cs/Fragment.cs 동일 판례); `willBeRemoveField = false`만 실질 상태 변경으로 유지.
//    * `GManager.instance.attackProcess.SwitchDefender(activateClass, false, selectedPermanent)` → 미러
//      시그니처 `(causeEffectSourceId, isBlock, newDefendingPermanentId)` =
//      `(activateClass.EffectSourceCard?.InstanceId, false, selectedPermanent.InstanceId)`.
//    * `card.Owner.HandCards`/`GetBattleAreaDigimons()` → `new Player(card.Context, card.Owner).*`.
//
// ④ 검증 중 발견(원장 기록, RD-S2-BT25039-01 — 포팅 결함 아님): [Opponent's Turn - ESS]는 AS-IS 원본이
//    SetIsInheritedEffect(true)로 등재(:212)하며, ICardEffect.CanActivate의 inherited/linked 분기(ICardEffect.cs
//    :454-484, DCGO/Assets/Scripts/Script/ICardEffect.cs:392-410과 바이트 단위로 동일 확인)는
//    "EffectSourceCard == permanentOfThisCard.TopCard이면 return false"이므로 이 능력은 BT25_039 자신이 아직
//    자기 스택의 top(비-진화 상태)인 동안에는 구조적으로 발동 불가 — 진화원으로 묻힌 뒤에만 발동(AS-IS 엔진의
//    inherited-effect 설계 자체가 그러함, 미러가 도입한 문제 아님). PILOT-S2.Witness.Tests의 BT25_039 W1은 이
//    발견에 따라 [On Deletion] 구간(비-inherited, else-분기)으로 witness 대상을 선정했다.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_039 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution Requirement
        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.HasTSTraits;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(level: 4, permanentCondition: PermanentCondition, digivolutionCost: 3, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region End of your Turn - Security
        if (timing == EffectTiming.OnEndTurn)
        {
            ActivateClass activateClass = new();
            activateClass.SetUpICardEffect("May play [Ceresmon] for -7, then may place this under played card", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsSkippable(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "(Security) [End of Your Turn] You may play 1 [Ceresmon] from your hand with the cost reduced by 7. If this effect played, you may place this card as the played Digimon's bottom digivolution card.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistInSecurity(card, false)
                    && CardEffectCommons.IsOwnerTurn(card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistInSecurity(card, false)
                    && CardEffectCommons.HasMatchConditionOwnersHand(card, CanSelectCardCondition);
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.HasPlayCost
                    && cardSource.EqualsCardName("Ceresmon")
                    && CardEffectCommons.CanPlayAsNewPermanent(cardSource, true, activateClass, fixedCost: cardSource.GetCostItself - 7);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                CardSource? selectedCard = null;

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
                    mode: SelectHandEffect.Mode.PlayForCost,
                    cardEffect: activateClass);

                selectHandEffect.SetReducedCostTuple((7, null!));

                selectHandEffect.SetUpCustomMessage("Select 1 card to play", "The opponent is selecting 1 card to play");

                await selectHandEffect.Activate();

                Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                {
                    if (cardSources.Count > 0)
                        selectedCard = cardSources[0];

                    return Task.CompletedTask;
                }

                if (selectedCard != null)
                {
                    Permanent? playedPermanent = ICardEffect.ResolvePermanentOfThisCard(selectedCard);

                    if (playedPermanent != null)
                    {
                        string selectPlayerMessage = "Will you place Sirenmon under Ceresmon?";
                        string notSelectPlayerMessage = "The opponent is choosing if they will place Sirenmon under Ceresmon.";

                        List<SelectionElement<bool>> command_SelectCommands = new List<SelectionElement<bool>>()
                        {
                            new SelectionElement<bool>(message: $"Yes", value: true, spriteIndex: 0),
                            new SelectionElement<bool>(message: $"No", value: false, spriteIndex: 1),
                        };

                        GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: command_SelectCommands, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);

                        await GManager.instance.userSelectionManager.WaitForEndSelect();

                        if (GManager.instance.userSelectionManager.SelectedBoolValue)
                        {
                            await playedPermanent.AddDigivolutionCardsBottom(new List<CardSource> { card }, activateClass.EffectSourceCard?.InstanceId);

                            await new IReduceSecurity(
                                card.Context,
                                card.Owner,
                                refCollector: null,
                                activateClass).ReduceSecurity();
                        }
                    }
                }
            }
        }
        #endregion

        #region All turns
        if (timing == EffectTiming.WhenRemoveField)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("By deleting this, they don't leave", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[All Turns] When any of your other [Shaman] or [Iliad] trait Digimon or Tamers would leave the battle area other than by your effects, by deleting this Digimon, they don't leave.";
            }

            // AS-IS CardSource.HasShamanTraits/HasIliadTraits (:4171-4177/:3751-3757, 미러 미존재) —
            // EqualsTraits 조립으로 인라인(BT9_111 편의-프로퍼티 인라인 판례).
            bool HasShamanTraits(CardSource cardSource) => cardSource.EqualsTraits("Shaman");
            bool HasIliadTraits(CardSource cardSource) => cardSource.EqualsTraits("Iliad");

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.CanTriggerWhenPermanentRemoveField(hashtable, PermanentCondition)
                    && !CardEffectCommons.IsByEffect(hashtable, cardEffect => CardEffectCommons.IsOwnerEffect(cardEffect.EffectSourceCard, card));

            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            bool PermanentCondition(Permanent permanent)
            {
                return permanent != ICardEffect.ResolvePermanentOfThisCard(card)
                    && CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card)
                    && (permanent.IsDigimon || permanent.IsTamer)
                    && (HasShamanTraits(permanent.TopCard) || HasIliadTraits(permanent.TopCard));
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(targetPermanents: new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) }, activateClass: activateClass, successProcess: permanents => SuccessProcess(), failureProcess: null);

                async Task SuccessProcess()
                {
                    List<Permanent> protectedPermanents = CardEffectCommons.GetPermanentsFromHashtable(hashtable)
                                .Filter(PermanentCondition);

                    foreach (Permanent permanent in protectedPermanents)
                    {
                        permanent.willBeRemoveField = false;
                        // AS-IS :172-175 HideDeleteEffect/HideHandBounceEffect/HideDeckBounceEffect/
                        // HideWillRemoveFieldEffect = UI 연출(스트립, Barrier.cs/Evade.cs/Fragment.cs 동일 판례).
                    }

                    await Task.CompletedTask;
                }
            }
        }
        #endregion

        #region On Deletion
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(CardEffectFactory.OnDeletionClass(
                card,
                "Place this card face up as the bottom security card",
                ActivateCoroutine,
                "[On Deletion] You may place this card face up as the bottom security card.",
                optional: true,
                additionalActivateCondition: AdditionalActivateCondition
            ));

            bool AdditionalActivateCondition(Hashtable hashtable, ActivateClass activateClass) => new Player(card.Context, card.Owner).CanAddSecurity(activateClass.EffectSourceCard?.InstanceId);

            async Task ActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
            {
                await CardObjectController.AddSecurityCard(card, toTop: false, faceUp: true);
            }
        }
        #endregion

        #region Opponent's Turn - ESS
        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("You may change the attack target to 1 of your suspended Digimon.", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetIsSkippable(true);
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("Redirect_BT25_039");
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "[Opponent's Turn] [Once Per Turn] When one of your opponent's Digimon attacks, you may change the attack target to 1 of your suspended Digimon.";

            bool IsOpponentDigimon(Permanent permanent) => CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.IsOpponentTurn(card)
                    && CardEffectCommons.CanTriggerOnPermanentAttack(hashtable, IsOpponentDigimon);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.HasMatchConditionOwnersPermanent(card, IsSuspendedDigimon);
            }

            bool IsSuspendedDigimon(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                    && permanent.IsSuspended;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                Permanent? selectedPermanent = null;

                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: IsSuspendedDigimon,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    selectPermanentCoroutine: SelectPermanentCoroutine,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to change the attack target to.", "The opponent is selecting 1 Digimon to change the attack target to.");

                await selectPermanentEffect.Activate();

                Task SelectPermanentCoroutine(Permanent permanent)
                {
                    selectedPermanent = permanent;
                    return Task.CompletedTask;
                }

                if (selectedPermanent != null)
                {
                    await GManager.instance.attackProcess.SwitchDefender(
                            activateClass.EffectSourceCard?.InstanceId,
                            false,
                            selectedPermanent.InstanceId);
                }
                else activateClass.RemoveUse();
            }
        }
        #endregion

        return cardEffects;
    }
}
