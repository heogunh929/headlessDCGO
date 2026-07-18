// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S4 카드 — EX6_044 (Digimon / Black)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX6/Black/EX6_044.cs (331 lines, 5 regions)
//    * Alternate Digivolution :16-35  (None — 레벨5+[Legend-Arms] 대체진화 조건 cost 4)
//    * Blocker/Reboot         :38-56  (None — BlockerSelfStaticEffect x2 + RebootSelfStaticEffect)
//    * [Hand][Main]           :59-150 (OnDeclaration — 3코스트+레벨6/[Legend-Arms] 자기 디지몬 밑에 배치,
//      전체 DP≤ 그 디지몬 상대 디지몬 <De-Digivolve 1>)
//    * [When Digivolving]     :153-269 (OnEnterFieldAnyone→WhenDigivolving — 자기 디지몬 1체가 상대 디지몬
//      효과에 영향받지 않음 until 상대 턴 끝)
//    * Opponent's Turn - ESS  :272-326 (None — 조건부 CanNotBeRemovedClass; 이 카드가 진화원(비-TopCard)이고
//      호스트 TopCard명이 "RagnaLoardmon"일 때만 등록)
//
// ② 프리미티브 매핑:
//    * P:AddSelfDigivolutionRequirementStaticEffect / BlockerSelfStaticEffect / RebootSelfStaticEffect —
//      상시효과 3건(AS-IS :28-33,40-53).
//    * P:IDegeneration — [Hand][Main] 몸통 <De-Digivolve 1> (AS-IS :145; CardController.cs:829).
//    * P:CanNotAffectedClass — [When Digivolving] 몸통 면역 부여(AS-IS :216-219).
//    * P:CanNotBeRemovedClass — Opponent's Turn ESS(AS-IS :280-286; 조건부 등록 — AS-IS 원문 그대로).
//
// ③ 배선 관례 근거: [Hand][Main] → OnDeclaration 그대로; [When Digivolving] → AS-IS는 OnEnterFieldAnyone에
//    CanTriggerWhenDigivolving 게이트를 달아 등록(:154, 단일 arm) — trigger-wiring rule 3 적용:
//    WhenDigivolving 전용 키로 재배선(BT17_026 idiom).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine→await (BT8_092 idiom).
//    * `card.Owner.AddMemory(-3, activateClass)` — HeadlessPlayerId 확장 그대로(§2.2 예외절).
//    * `selectedPermanent.AddDigivolutionCardsBottom(list, activateClass)`(AS-IS 2nd arg=ICardEffect) →
//      `AddDigivolutionCardsBottom(list, activateClass.EffectSourceCard?.InstanceId)`(BT17_026:316-317 idiom).
//    * `card.Owner.Enemy.GetBattleAreaDigimons()` → `CardEffectCommons.OpponentOf(card).GetBattleAreaDigimons()`
//      (§2.2 상대 id 단축형).
//    * `new IDegeneration(permanent, 1, activateClass).Degeneration()` → ctor가 causeEffectSourceId를 얻음:
//      `new IDegeneration(permanent, 1, activateClass.EffectSourceCard?.InstanceId, cardEffect: activateClass)
//      .Degeneration()` (symbol_map §2.5, CardController.cs:831 시그니처).
//    * `Func<Permanent,bool>` 술어(IsLevel6OrHasLegendArmsTrait/IsYourDigimon)는 SelectPermanentEffect의
//      canTargetCondition(id-형)에 id-어댑터를 덧댐(BT17_026 idiom); HasMatchConditionPermanent는 Permanent
//      오버로드 그대로(§2.3 비대칭).
//    * `CardEffectCommons.IsOpponentEffect(cardEffect, card)`/`IsOwnerEffect(cardEffect, card)`(AS-IS
//      1st arg=ICardEffect) → mirror 1st arg=CardSource?: `IsOpponentEffect(cardEffect.EffectSourceCard, card)`
//      / `IsOwnerEffect(cardEffect.EffectSourceCard, card)`(GameContextDeterminarion.cs:788,808 시그니처 델타).
//    * `card.PermanentOfThisCard()`(가변/값-동등 필요) → `ICardEffect.ResolvePermanentOfThisCard(card)`
//      (§2.3); `selectedPermanent.TopCard != card` — mirror CardSource `!=`는 InstanceId 기반 값-동등
//      연산자 오버로드 그대로 사용 가능(CARDSOURCE-EQUALITY).
//    * AS-IS :285 `if (!cardEffects.Contains(canNotBeRemovedClass)) cardEffects.Add(...)` — canNotBeRemovedClass는
//      바로 위에서 새로 `new`된 인스턴스이므로 이 Contains 체크는 AS-IS 원문에서도 항상 false(죽은 가드) —
//      1:1 그대로 보존(단순화 금지).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX6.Black;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX6_044 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution
        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                if (targetPermanent.TopCard.HasLevel && targetPermanent.TopCard.Level == 5)
                {
                    return targetPermanent.TopCard.ContainsTraits("Legend-Arms");
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition,
                digivolutionCost: 4,
                ignoreDigivolutionRequirement: false,
                card: card,
                condition: null));
        }
        #endregion

        #region Blocker/Reboot
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(
                            isInheritedEffect: false,
                            card: card,
                            condition: null));

            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(
            isInheritedEffect: true,
            card: card,
            condition: null));

            cardEffects.Add(CardEffectFactory.RebootSelfStaticEffect(
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        #endregion

        #region Hand - Main
        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("<De-Digivolve 1> all of your opponent's Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, true, EffectDiscription());
            activateClass.SetIsDigimonEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Hand][Main] By paying 3 cost and placing this card as the bottom digivolution card of 1 of your Digimon that's level 6 or has the [Legend-Arms] trait, <De-Digivolve 1> all of your opponent's Digimon with as much or less DP than that Digimon.";
            }

            bool IsLevel6OrHasLegendArmsTrait(Permanent targetPermanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(targetPermanent, card))
                {
                    if (targetPermanent.TopCard.HasLevel && targetPermanent.Level == 6)
                        return true;

                    if (targetPermanent.TopCard.ContainsTraits("Legend-Arms"))
                        return true;
                }

                return false;
            }

            Permanent? PermanentOfHM(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            bool IsLevel6OrHasLegendArmsTraitById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOfHM(id);
                return permanent is not null && IsLevel6OrHasLegendArmsTrait(permanent);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnHand(card))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersPermanent(card, IsLevel6OrHasLegendArmsTrait))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                Permanent? selectedPermanent = null;

                if (CardEffectCommons.HasMatchConditionPermanent(card, IsLevel6OrHasLegendArmsTrait))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, IsLevel6OrHasLegendArmsTraitById));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsLevel6OrHasLegendArmsTraitById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to add bottom digivolution source.", "The opponent is selecting 1 Digimon to add bottom digivolution source.");

                    await selectPermanentEffect.Activate();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanent = permanent;
                        return Task.CompletedTask;
                    }
                }

                if (selectedPermanent != null)
                {
                    await card.Owner.AddMemory(-3, activateClass);

                    await selectedPermanent.AddDigivolutionCardsBottom(
                            new List<CardSource>() { card },
                            activateClass.EffectSourceCard?.InstanceId);

                    List<Permanent> enemyPermanents = CardEffectCommons.OpponentOf(card).GetBattleAreaDigimons().Where(x => x.DP <= selectedPermanent.DP).ToList();

                    foreach (Permanent permanent in enemyPermanents)
                    {
                        await new IDegeneration(permanent, 1, activateClass.EffectSourceCard?.InstanceId, cardEffect: activateClass).Degeneration();
                    }
                }
            }
        }
        #endregion

        #region When Digivolving
        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:154)에 등록하지만, 미러 방언은 WhenDigivolving 전용 키
        //   (trigger-wiring rule 3 — 이중-키 등록 금지).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Unaffected by opponents Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] 1 of your Digimon isn't affected by your opponent's Digimon effects until the end of your opponent's turn.";
            }

            bool IsYourDigimon(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);
            }

            Permanent? PermanentOfWD(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            bool IsYourDigimonById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOfWD(id);
                return permanent is not null && IsYourDigimon(permanent);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                Permanent? selectedPermanent = null;

                if (CardEffectCommons.HasMatchConditionPermanent(card, IsYourDigimon))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, IsYourDigimonById));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsYourDigimonById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will not be affected by the effect's of your opponent's Digimon.", "The opponent is selecting 1 Digimon that will not be affected by the effect's of your Digimon.");

                    await selectPermanentEffect.Activate();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanent = permanent;
                        return Task.CompletedTask;
                    }
                }

                if (selectedPermanent != null)
                {
                    CanNotAffectedClass canNotAffectedClass = new CanNotAffectedClass();
                    canNotAffectedClass.SetUpICardEffect("Isn't affected by opponent's Digimon's effects", CanUseConditionImmunity, card);
                    canNotAffectedClass.SetUpCanNotAffectedClass(CardCondition: CardCondition, SkillCondition: SkillCondition);
                    selectedPermanent.UntilOpponentTurnEndEffects.Add(GetCardEffect);

                    bool CanUseConditionImmunity(Hashtable hashtable1)
                    {
                        return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(selectedPermanent, card);
                    }

                    bool CardCondition(CardSource cardSource)
                    {
                        if (CardEffectCommons.IsPermanentExistsOnBattleArea(selectedPermanent))
                        {
                            if (cardSource == selectedPermanent.TopCard)
                            {
                                return true;
                            }
                        }

                        return false;
                    }

                    bool SkillCondition(ICardEffect cardEffect)
                    {
                        if (CardEffectCommons.IsOpponentEffect(cardEffect.EffectSourceCard, card))
                        {
                            if (cardEffect.IsDigimonEffect)
                            {
                                return true;
                            }

                            if (cardEffect.IsDigimonEffect && cardEffect.IsSecurityEffect)
                            {
                                return true;
                            }
                        }

                        return false;
                    }

                    ICardEffect GetCardEffect(EffectTiming _timing)
                    {
                        if (_timing == EffectTiming.None)
                        {
                            return canNotAffectedClass;
                        }

                        return null!;
                    }
                }
            }
        }
        #endregion

        #region Opponent's Turn - ESS
        if (timing == EffectTiming.None)
        {
            if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
            {
                if (ICardEffect.ResolvePermanentOfThisCard(card).TopCard != card)
                {
                    Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                    CanNotBeRemovedClass canNotBeRemovedClass = new CanNotBeRemovedClass();
                    canNotBeRemovedClass.SetUpICardEffect("Can't leave battle area except by deletion effect", CanUseProtectionCondition, card);
                    canNotBeRemovedClass.SetUpCanNotBeRemovedClass(permanentCondition: PermanentCondition);
                    canNotBeRemovedClass.SetIsInheritedEffect(true);

                    if (!cardEffects.Contains(canNotBeRemovedClass))
                        cardEffects.Add(canNotBeRemovedClass);

                    bool CanUseProtectionCondition(Hashtable hashtable)
                    {
                        if (CardEffectCommons.IsOpponentTurn(card))
                        {
                            if (CardEffectCommons.IsPermanentExistsOnBattleArea(selectedPermanent))
                            {
                                if (!CardEffectCommons.IsByEffect(hashtable, cardEffect => CardEffectCommons.IsOwnerEffect(cardEffect.EffectSourceCard, card)))
                                {
                                    return true;
                                }
                            }
                        }

                        return false;
                    }

                    bool PermanentCondition(Permanent permanent)
                    {

                        if (CardEffectCommons.IsPermanentExistsOnBattleArea(selectedPermanent))
                        {
                            if (permanent == selectedPermanent)
                            {
                                if (selectedPermanent.TopCard != card)
                                {
                                    if (selectedPermanent.TopCard.CardNames.Contains("RagnaLoardmon"))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }

                        return false;
                    }
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
