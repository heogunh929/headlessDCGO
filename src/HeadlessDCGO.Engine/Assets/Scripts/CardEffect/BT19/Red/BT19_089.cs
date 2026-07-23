// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// BT19_089 (Digimon / Red) — 1:1 mirror of DCGO/Assets/Scripts/CardEffect/BT19/Red/BT19_089.cs (183 lines).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커 3 region:
//    * [None] Ignore Color Requirements :15-37  (자기 색 요구 무시, 상대 White Digimon/Tamer 보유 조건)
//    * [Main] :40-152 (상대 턴 종료까지 자기 Digimon 1체에 immune-from-opponent-Option + immune-from-DP-minus 부여)
//    * [Security] :155-178 (이 카드를 손패로)
//
// ② 프리미티브 매핑:
//    * P:IgnoreColorConditionClass + SetUpIgnoreColorConditionClass — [None] (BT18_098 선례).
//    * E:SelectPermanentEffect Mode.Custom (id-adapter) — [Main] 대상 선정 (BT18_098/BT7_055 선례).
//    * P:GainImmuneFromDPMinus(permanent-target, cardEffectCondition, UntilOpponentTurnEnd) — [Main]
//      (GiveEffectToPermanent/ImmuneFromDPMinus.cs, AS-IS 시그니처 1:1). BT19_089 = 이 프리미티브의 FIRST live 호출자.
//    * P:CanNotAffectedClass + SetUpCanNotAffectedClass(CardCondition, SkillCondition) + Permanent.
//      UntilOpponentTurnEndEffects.Add — [Main] Option-면역 (AS-IS :105-108).
//    * P:AddThisCardToHand — [Security] (BT9_109 선례, 2번째 인자=card).
//
// ③ 배선 관례 근거: [None]→None 그대로, [Main]→OptionSkill + CanTriggerOptionMainEffect(hashtable, card),
//    [Security]→SecuritySkill + CanTriggerSecurityEffect(hashtable, card) + SetIsSecurityEffect(true).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`.
//    * `permanent.TopCard.CardColors.Contains(CardColor.White)` → `permanent.TopCard.HasCardColor("White")`
//      (색 enum→string, BT18_098:117 idiom).
//    * `HasMatchConditionOpponentsPermanent(card, Func<Permanent,bool>)` / `MatchConditionPermanentCount` /
//      `SelectPermanentEffect.canTargetCondition` → id-adapter `Func<HeadlessEntityId,bool>` (BT18_098/BT7_055 idiom).
//    * `cardEffect.EffectSourceCard.Owner == card.Owner.Enemy` → `== CardEffectCommons.OpponentOf(card)` (AD1_013 idiom).
//    * `AddThisCardToHand(card, activateClass)` → `AddThisCardToHand(card, card)` (CardSource-형 substrate overload, BT9_109:100 idiom).
//
// ✓ 판독-측 원장 (RD-J-03 / RD-P6B-13, RESOLVED @5731561b): [Main]의 `SkillCondition` 술어는 causing-effect의 per-instance
//   `IsDigimonEffect`/`IsTamerEffect` 플래그를 읽는다 (`!IsDigimonEffect || !IsTamerEffect` — "digimon-이자 tamer-효과
//   BOTH인 효과는 제외" 세분). GainImmuneFromDPMinus grant-측은 이 REAL 술어를 그대로 저장하고(RD-W2-1, ②J-4 완료), 라이브
//   판독 경로(Permanent.ImmuneFromDPMinus)는 실 causing-effect를 그대로 통과시켜 세분을 verbatim 평가한다. 남아 있던
//   DP-감소 재구성 경로의 causing-effect 스탠드인(`NewModelContinuousScan.BuildCausingEffectStandIn`, 소스-id로만 구성)도
//   이제 ctor가 소스 카드 타입에서 두 플래그를 스탬프한다(`SetIsDigimonEffect(source.IsDigimon)`/`SetIsTamerEffect(
//   source.IsTamer)` — ActivatedHashtableBridge.CauseStubEffect / RD-W3-5 effectStamp 선례 동형). 따라서 both-flag
//   세분이 재구성 경로에서도 직독 경로와 동일하게 평가된다(과대-근사 소멸: both-flagged enemy cause = 면역 거부 = DP-minus
//   적용). 술어는 단순화 없이 그대로 배선된다(delegate 실호출). 이 카드가 그 상환의 live witness —
//   RD-J03-BT19_089.Witness.Tests의 ApplicationPathReconstructsFlags(both/plain/own 3-경로) green.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT19.Red;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT19_089 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        Permanent? PermanentOf(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                ? new Permanent(card.Context, id, rec.OwnerId)
                : null;

        #region Ignore Color Requirements
        if (timing == EffectTiming.None)
        {
            IgnoreColorConditionClass ignoreColorConditionClass = new IgnoreColorConditionClass();
            ignoreColorConditionClass.SetUpICardEffect("Ignore color requirements", CanUseCondition, card);
            ignoreColorConditionClass.SetUpIgnoreColorConditionClass(cardCondition: CardCondition);

            cardEffects.Add(ignoreColorConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.HasMatchConditionOpponentsPermanent(card, IsWhiteDigimonOrTamer);
            }

            bool IsWhiteDigimonOrTamer(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null
                    && (permanent.IsDigimon || permanent.IsTamer)
                    && permanent.TopCard.HasCardColor("White");
            }

            bool CardCondition(CardSource cardSource)
            {
                return (cardSource == card);
            }
        }
        #endregion

        #region Main
        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("This Digimon becomes immune to opponent's options, and can't be DP reduced", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Until the end of your opponent's turn, 1 of your Digimon isn't affected by the effects of your opponent's Option cards and it can't have its DP reduced.";
            }

            bool CanSelectDigimonCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);
            }

            bool CanSelectDigimonById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanSelectDigimonCondition(permanent);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                Permanent? selectedPermanent = null;

                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectDigimonCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectDigimonById));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectDigimonById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will gain effect.", "The opponent is selecting 1 Digimon that will gain effect.");

                    await selectPermanentEffect.Activate();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanent = permanent;

                        return Task.CompletedTask;
                    }

                    if (selectedPermanent != null)
                    {
                        await CardEffectCommons.GainImmuneFromDPMinus(
                            targetPermanent: selectedPermanent,
                            cardEffectCondition: SkillCondition,
                            effectDuration: EffectDuration.UntilOpponentTurnEnd,
                            activateClass: activateClass,
                            effectName: "Can't have DP reduced");

                        CanNotAffectedClass canNotAffectedClass = new CanNotAffectedClass();
                        canNotAffectedClass.SetUpICardEffect("Isn't affected by opponent's option effects", CanUseCondition1, card);
                        canNotAffectedClass.SetUpCanNotAffectedClass(CardCondition: CardCondition, SkillCondition: SkillCondition);
                        selectedPermanent.UntilOpponentTurnEndEffects.Add((_timing) => canNotAffectedClass);

                        // AS-IS :110 CreateBuffEffect = UI (dropped).

                        bool CanUseCondition1(Hashtable hashtable)
                        {
                            return CardEffectCommons.IsPermanentExistsOnBattleArea(selectedPermanent);
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

                        // ✓ RD-J-03/RD-P6B-13 (RESOLVED @5731561b, header): reads causing-effect per-instance flags; the
                        // DP-minus reconstruction stand-in now stamps IsDigimonEffect/IsTamerEffect from the source card's
                        // type, so the "excludes an effect flagged BOTH digimon- and tamer-effect" refinement holds on the
                        // application path identically to the live read path (was a narrow over-approximation). Predicate is
                        // threaded verbatim, not simplified.
                        bool SkillCondition(ICardEffect cardEffect)
                        {
                            if (cardEffect != null)
                            {
                                if (cardEffect.EffectSourceCard != null)
                                {
                                    if (cardEffect.EffectSourceCard.Owner == CardEffectCommons.OpponentOf(card))
                                    {
                                        if (!cardEffect.IsDigimonEffect || !cardEffect.IsTamerEffect)
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
        }
        #endregion

        #region Security
        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Add this card to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] Add this card to the hand.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.AddThisCardToHand(card, card);
            }
        }
        #endregion

        return cardEffects;
    }
}
