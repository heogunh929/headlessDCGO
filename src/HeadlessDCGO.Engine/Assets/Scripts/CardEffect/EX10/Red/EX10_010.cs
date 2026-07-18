// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 정본 트랜치2A 카드 — WarGreymon ACE (EX10_010, Digimon / Red / ACE)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX10/Red/EX10_010.cs (213 lines, 8 regions)
//    * Blast Digivolve                :15-22  (timing == OnCounterTiming — BlastDigivolveEffect)
//    * Static - Raid                  :24-30  (timing == OnAllyAttack — RaidSelfEffect)
//    * Static - Reboot/Blocker        :32-37  (timing == None — Reboot/Blocker SelfStaticEffect)
//    * On Play/When Digivolving Shared:41-83  (공유 술어 + 공유 코루틴 SharedActivateCoroutine)
//    * On Play                        :85-100 (timing == OnEnterFieldAnyone + CanTriggerOnPlay 게이트)
//    * When Digivolving               :102-118(AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving 게이트)
//    * All Turns - Immunity           :122-173(timing == None — CanNotAffectedClass)
//    * All Turns - DP                 :175-207(timing == OnRemovedField||OnEnterFieldAnyone||WhenTopCardTrashed
//                                              — ActivateClass 밖 명령형 Boosts 뮤테이션)
//
// ② 감사 축 매핑 (canonical ②):
//    * P:CanNotAffectedClass  — [All Turns] Immunity: 자신은 상대 디지몬 효과 영향 안 받음 (AS-IS :124-171)
//    * T:OnRemovedField       — [All Turns] DP 부스트 재평가 트리거 (AS-IS :177)
//    * T:WhenTopCardTrashed   — [All Turns] DP 부스트 재평가 트리거 (AS-IS :177)
//    * (+BlastDigivolveEffect :19, RaidSelfEffect :29, RebootSelfStaticEffect/BlockerSelfStaticEffect :35-36,
//       [On Play]/[When Digivolving] 공유 삭제 = SelectPermanentEffect Mode.Destroy + play-cost ≤7 술어
//       (HasPlayCost/GetCostItself, AS-IS :50-52,:64-79), DP boost AT_EX10-010 +3000 (AS-IS :192-202))
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [On Play] → OnEnterFieldAnyone + CanTriggerOnPlay(hashtable, card) 게이트(AS-IS :87,:96 그대로; rule 3).
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(미러 방언; AS-IS는 OnEnterFieldAnyone :104에
//      두지만 미러 DigivolveAction은 WhenDigivolving만 해소, 이중-키 등록 금지). CanUseCondition의
//      IsExistOnBattleAreaDigimon + CanTriggerWhenDigivolving(hashtable, card) 게이트는 AS-IS :113-114 그대로.
//    * Static/Immunity/DP 상시 영역은 AS-IS 타이밍(OnCounterTiming/OnAllyAttack/None/OnRemovedField/
//      WhenTopCardTrashed) 그대로 유지.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`.
//      공유 코루틴 `IEnumerator SharedActivateCoroutine(Hashtable, ActivateClass)`→`async Task ...`; AS-IS 람다
//      `(hashTable)=>SharedActivateCoroutine(hashTable, activateClass)`는 `Func<Hashtable,Task>`로 유지.
//    * `GManager.instance.GetComponent<SelectPermanentEffect>()` VERBATIM 유지.
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (ICardEffect.cs:537).
//    * `CardEffectCommons.HasMatchConditionPermanent(pred)` 1-arg — 미러엔 1-arg 오버로드가 없고 card 선행 인자
//      필수. Permanent-술어 오버로드(CardEffectCommons.cs:4079)로 `HasMatchConditionPermanent(card, pred)` 치환.
//    * SelectPermanentEffect.SetUp의 canTargetCondition은 미러에서 `Func<HeadlessEntityId,bool>`(SetUp :341).
//      AS-IS `Func<Permanent,bool> CanSelectPermanentCondition`는 그대로 두고 id 어댑터(PermanentOf +
//      CanSelectPermanentById)를 덧댐(BT17_026 idiom; 술어 뭉갬 금지). Mode.Destroy 존재(:34,:589).
//    * `CardEffectCommons.IsOpponentEffect(cardEffect, card)` — 미러 시그니처는 `IsOpponentEffect(CardSource?,
//      CardSource)`(CardEffectCommons.cs:4162)이므로 `cardEffect.EffectSourceCard`를 넘김(Progress.cs:79 확립).
//    * `thisPermanent.AddBoost(new Permanent.DPBoost("AT_EX10-010", 3000, null))` /
//      `thisPermanent.RemoveBoost("AT_EX10-010")` — 미러 Permanent엔 인스턴스 AddBoost/RemoveBoost가 없고
//      `Boosts`는 읽기-뷰(Permanent.cs:2260). substrate `DpBoostHelpers.AddBoost/RemoveBoost(repository,
//      cardId, id[, dp])`(DpBoostHelpers.cs:20,35)로 치환. `Boosts.Exists(x => x.ID == "...")` 읽기는 1:1 유지
//      (Permanent.Boosts 읽기-뷰 + DPBoost.ID).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX10.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX10_010 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Blast Digivolve

        if (timing == EffectTiming.OnCounterTiming)
        {
            cardEffects.Add(CardEffectFactory.BlastDigivolveEffect(card: card, condition: null));
        }

        #endregion

        #region Static Effects

        //Raid
        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.RaidSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        //Reboot/Blocker
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.RebootSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        #endregion

        #region On Play/When Digivolving Shared

        string EffectDescription(string tag)
        {
            return $"[{tag}] Delete 1 of your opponent's play cost 7 or lower Digimon or Tamers.";
        }

        bool CanSelectPermanentCondition(Permanent permanent)
        {
            return (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) || CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaTamer(permanent, card))
                   && permanent.TopCard.HasPlayCost && permanent.TopCard.GetCostItself <= 7;
        }

        // (미러 idiom — BT17_026) SelectPermanentEffect 브릿지의 canTargetCondition은 id-형이므로
        // AS-IS Permanent-술어는 그대로 두고 id 어댑터를 덧댄다(술어 뭉갬 금지).
        Permanent? PermanentOf(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                ? new Permanent(card.Context, id, rec.OwnerId)
                : null;

        bool CanSelectPermanentById(HeadlessEntityId id)
        {
            Permanent? permanent = PermanentOf(id);
            return permanent is not null && CanSelectPermanentCondition(permanent);
        }

        bool CanActivateSharedCondition(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleArea(card) &&
                   CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition);
        }

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectPermanentById,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Destroy,
                    cardEffect: activateClass);

                await selectPermanentEffect.Activate();
            }
        }

        #endregion

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 Digimon/Tamer", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateSharedCondition, (hashTable) => SharedActivateCoroutine(hashTable, activateClass), -1, false, EffectDescription("On Play"));
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }
        }

        #endregion

        #region When Digivolving

        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:104)이지만 미러 방언은 WhenDigivolving 전용 키
        //   (DigivolveAction이 WhenDigivolving만 해소; 이중-키 등록 금지 — trigger-wiring rule 3).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 Digimon/Tamer", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateSharedCondition, (hashTable) => SharedActivateCoroutine(hashTable, activateClass), -1, false, EffectDescription("When Digivolving"));
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }
        }

        #endregion

        #region All Turns

        #region All Turns - Immunity

        if (timing == EffectTiming.None)
        {
            CanNotAffectedClass canNotAffectedClass = new CanNotAffectedClass();
            canNotAffectedClass.SetUpICardEffect("Isn't affected by opponent's Digimon's effects", CanUseCondition, card);
            canNotAffectedClass.SetUpCanNotAffectedClass(CardCondition: CardCondition, SkillCondition: SkillCondition);
            cardEffects.Add(canNotAffectedClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.HasMatchConditionPermanent(card, OpponentsPermanent);
            }

            bool CardCondition(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    if (CardEffectCommons.IsExistOnBattleArea(card))
                    {
                        if (cardSource == ICardEffect.ResolvePermanentOfThisCard(card).TopCard)
                        {
                            return true;
                        }
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
                }

                return false;
            }

            bool OpponentsPermanent(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) &&
                       ICardEffect.ResolvePermanentOfThisCard(card).Boosts.Exists(x => x.ID == "AT_EX10-010");
            }
        }

        #endregion

        #region All Turns - DP

        if (timing == EffectTiming.OnRemovedField || timing == EffectTiming.OnEnterFieldAnyone || timing == EffectTiming.WhenTopCardTrashed)
        {
            if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
            {
                Permanent thisPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                bool OpponentsPermanent(Permanent permanent)
                {
                    return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) &&
                           permanent.DP >= 13000;
                }

                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, OpponentsPermanent))
                        DpBoostHelpers.AddBoost(card.Context.CardInstanceRepository, thisPermanent.InstanceId, "AT_EX10-010", 3000);
                    else
                    {
                        if (thisPermanent.Boosts.Exists(x => x.ID == "AT_EX10-010"))
                            DpBoostHelpers.RemoveBoost(card.Context.CardInstanceRepository, thisPermanent.InstanceId, "AT_EX10-010");
                    }
                }
                else
                {
                    if (thisPermanent.Boosts.Exists(x => x.ID == "AT_EX10-010"))
                        DpBoostHelpers.RemoveBoost(card.Context.CardInstanceRepository, thisPermanent.InstanceId, "AT_EX10-010");
                }
            }
        }

        #endregion

        #endregion

        return cardEffects;
    }
}
