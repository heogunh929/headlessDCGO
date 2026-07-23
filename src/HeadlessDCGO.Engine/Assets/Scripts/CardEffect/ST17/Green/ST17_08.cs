// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S2 카드 — ST17_08 (Digimon / Green)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/ST17/Green/ST17_08.cs (360 lines, 4 regions)
//    * Digivolution Condition :18-32  (timing == None — AddSelfDigivolutionRequirementStaticEffect)
//    * Blocker and Reboot     :35-44  (timing == None — BlockerSelfStaticEffect, RebootSelfStaticEffect)
//    * Counter Timing         :47-51  (timing == OnCounterTiming — BlastDigivolveEffect)
//    * When Digivolving       :55-297 (timing == OnEnterFieldAnyone, 2개 ActivateClass:
//        1) 상대 Digimon/Tamer 최대 2 tap + 2체에 언서스펜드/진화불가 부여(AS-IS :57-249)
//        2) 자기 언서스펜드, Once Per Turn(AS-IS :251-297))
//    * End of Attack          :304-352(timing == OnEndAttack — 자기 언서스펜드, Once Per Turn)
//
// ② 프리미티브 매핑:
//    * P:AddSelfDigivolutionRequirementStaticEffect — [Rapidmon] 위 코스트 5, 요구무시 (AS-IS :25-30)
//    * P:BlockerSelfStaticEffect / P:RebootSelfStaticEffect — 상시 [Blocker]/[Reboot] (AS-IS :37/42)
//    * P:BlastDigivolveEffect — [Counter Timing] (AS-IS :49; RD-P6C2-11 RESOLVED 2026-07-22 — BlastDigivolution.cs:4
//      A8 구조골 GOAL 1: read-side PermanentFrame idiom 착지, CanActivate/ActivateCoroutine 실행. 구 "latent 갭으로
//      NotSupportedException" 주석은 stale이라 정정. 카드 등재는 AS-IS 그대로 — witness A8G1-BlastDigivolve + PILOT-S2 ST17_08 W2)
//    * P:SelectPermanentEffect Mode.Tap — 상대 Digimon/Tamer 최대 2 tap (AS-IS :121-150)
//    * P:CanNotDigivolveClass + Permanent.UntilOwnerTurnEndEffects — tap된 대상에 진화불가 부여 (AS-IS :180-183)
//    * P:GainCantUnsuspendUntilOpponentTurnEnd — 같은 대상에 언서스펜드 불가 부여 (AS-IS :241-244)
//    * P:CanUnsuspend + P:IUnsuspendPermanents — 자기 언서스펜드(진화 시/공격 종료 시) (AS-IS :281-295/346-350)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [When Digivolving] → EffectTiming.OnEnterFieldAnyone + CanTriggerWhenDigivolving(hashtable, card)
//      게이트(AS-IS :88/268 그대로) — 2번째 효과는 IsExistOnBattleArea 추가 가드까지 포함(AS-IS :266-275).
//    * [End of Attack] → EffectTiming.OnEndAttack + IsOwnerTurn + CanTriggerOnAttack(hashtable, card) +
//      CanUnsuspend 게이트(AS-IS :327-343 그대로).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`
//      (BT17_026 idiom).
//    * `card.Owner.Enemy.GetBattleAreaPermanents()` → `new Player(card.Context, card.Owner).Enemy!
//      .GetBattleAreaPermanents()` (BT18_042/EX7_014 idiom); AS-IS card-less `HasMatchConditionPermanent(cond)`
//      (전역 스캔) → 미러 `(card, cond)` 오버로드(ST2_06/ST1_08 관례); `MatchConditionPermanentCount`는
//      id-형 오버로드(BT17_026 idiom).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`(BT8_092 관례).
//    * `GManager.instance.GetComponent<Effects>().DebuffSE`/`.CreateDebuffEffect(...)`(AS-IS :185-187) = UI
//      연출 — 미러 확립 관례상 스트립(CardController.cs:899 "CreateDebuffEffect = UI (stripped)" 동일,
//      BT17_026 헤더 동일 판례).
//    * SelectPermanentEffect.canTargetCondition는 id-형(Func&lt;HeadlessEntityId,bool&gt;) — AS-IS
//      Permanent-술어는 그대로 두고 PermanentOf(id) 어댑터를 덧댐(술어 뭉갬 금지; BT17_026 관례).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST17.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST17_08 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Digivolution Condition
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.ContainsCardName("Rapidmon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition,
                digivolutionCost: 5,
                ignoreDigivolutionRequirement: true,
                card: card,
                condition: null));
        }
        #endregion

        #region Blocker and Reboot
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.RebootSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Counter Timing
        if (timing == EffectTiming.OnCounterTiming)
        {
            cardEffects.Add(CardEffectFactory.BlastDigivolveEffect(card: card, condition: null));
        }
        #endregion

        #region When Digivolving

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Suspend Digimon and Tamers and give effects", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetHashString("SuspendAndGiveEffects_ST17_08");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Suspend 2 of your opponent's Digimon and/or Tamers. Then, 2 of your opponent's Digimon or Tamers can't unsuspend or digivolve until the end of your opponent's turn.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleArea(permanent, card))
                {
                    if (permanent.IsDigimon)
                    {
                        return true;
                    }

                    if (permanent.IsTamer)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
            }

            bool CanSelectPermanentCondition1(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleArea(permanent, card))
                {
                    if (permanent.IsDigimon)
                    {
                        return true;
                    }

                    if (permanent.IsTamer)
                    {
                        return true;
                    }
                }

                return false;
            }

            Permanent? PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            bool CanSelectPermanentById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanSelectPermanentCondition(permanent);
            }

            bool CanSelectPermanentById1(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanSelectPermanentCondition1(permanent);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (new Player(card.Context, card.Owner).Enemy!.GetBattleAreaPermanents().Count(CanSelectPermanentCondition) >= 1)
                {
                    int maxCount = Math.Min(2, new Player(card.Context, card.Owner).Enemy!.GetBattleAreaPermanents().Count(CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: CanEndSelectCondition,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Tap,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();

                    bool CanEndSelectCondition(List<Permanent> permanents)
                    {
                        if (CardEffectCommons.HasNoElement(permanents))
                        {
                            return false;
                        }

                        return true;
                    }
                }

                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition1))
                {
                    int maxCount = Math.Min(2, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentById1));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById1,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 2 Digimon or Tamers that will not unsuspend or digivolve.", "The opponent is selecting 2 Digimon that will not unsuspend or digivolve.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        Permanent selectedPermanent = permanent;

                        CanNotDigivolveClass canNotPutFieldClass = new CanNotDigivolveClass();
                        canNotPutFieldClass.SetUpICardEffect("Can't Digivolve", CanUseCondition1, card);
                        canNotPutFieldClass.SetUpCanNotEvolveClass(permanentCondition: PermanentCondition, cardCondition: CardCondition);
                        selectedPermanent.UntilOwnerTurnEndEffects.Add(GetCardEffect);

                        // AS-IS :185-187 PlaySE(DebuffSE) + CreateDebuffEffect(...) = UI 연출 — 미러 확립
                        // 관례상 스트립(CardController.cs:899 동일, BT17_026 헤더 동일 판례).

                        bool CanUseCondition1(Hashtable hashtable)
                        {
                            return true;
                        }

                        bool PermanentCondition(Permanent permanent)
                        {
                            if (permanent == selectedPermanent)
                            {
                                if (permanent.TopCard != null)
                                {
                                    if (permanent.IsDigimon || permanent.IsTamer)
                                    {
                                        if (!permanent.TopCard.CanNotBeAffected(canNotPutFieldClass))
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }

                            return false;
                        }

                        bool CardCondition(CardSource cardSource)
                        {
                            if (cardSource.Owner == new Player(card.Context, card.Owner).Enemy!.PlayerId)
                            {
                                if (cardSource.IsDigimon || cardSource.IsTamer)
                                {
                                    if (!cardSource.CanNotBeAffected(canNotPutFieldClass))
                                    {
                                        return true;
                                    }
                                }
                            }

                            return false;
                        }

                        ICardEffect? GetCardEffect(EffectTiming _timing)
                        {
                            if (_timing == EffectTiming.None)
                            {
                                return canNotPutFieldClass;
                            }

                            return null;
                        }

                        await CardEffectCommons.GainCantUnsuspendUntilOpponentTurnEnd(
                            targetPermanent: selectedPermanent,
                            activateClass: activateClass);
                    }
                }
            }
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Unsuspend this Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("Unsuspend_ST17_08");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] You may unsuspend this Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanUnsuspend(ICardEffect.ResolvePermanentOfThisCard(card)))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass).Unsuspend();
            }

        }


        #endregion

        #region End of Attack

        if (timing == EffectTiming.OnEndAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Unsuspend this Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("Unsuspend_ST17_08");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[End of Attack] You may unsuspend this Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerOnAttack(hashtable, card))
                        {
                            if (CardEffectCommons.CanUnsuspend(ICardEffect.ResolvePermanentOfThisCard(card)))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass).Unsuspend();
            }
        }

        #endregion


        return cardEffects;
    }
}
