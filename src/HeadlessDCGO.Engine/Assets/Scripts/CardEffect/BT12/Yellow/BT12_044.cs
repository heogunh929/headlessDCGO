// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S5 카드 — BT12_044 (Digimon / Yellow)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT12/Yellow/BT12_044.cs (199 lines, 2 regions)
//    * [When Digivolving]                    :16-113 (timing == OnEnterFieldAnyone — Security Attack -2 부여 +
//      "Initial check" 락-스텝 SAttack 재보정)
//    * [Your Turn] Gain Security Attack       :119-193 (timing == 7개 복수 EffectTiming — 배경 프로세스,
//      SetIsBackgroundProcess(true))
//
// ② 프리미티브 매핑:
//    * E:SelectPermanentEffect Mode.Custom + P:ChangeDigimonSAttack(-2) — [When Digivolving] 상대 디지몬 1체
//      Security Attack -2 부여 (AS-IS :53-85).
//    * P:ChangeDigimonSAttack(+1, hashstring:"SecAttackFromLampEffect") — 자기 SecurityAttack 카운터-보정 루프,
//      두 블록(When Digivolving 말미 :88-111, Your Turn :173-191)에서 동형 반복(AS-IS 원본 구조 그대로 유지;
//      단순화/통합 금지).
//    * K:AS-IS-signature bridge overloads — CardEffectCommons/GiveEffect/GiveEffectToPermanent/ChangeSAttack.cs
//      가 정확히 AS-IS 호출 시그니처(targetPermanent, changeValue, effectDuration, activateClass[,
//      activateAnimation, hashstring])를 그대로 노출(symbol_map row 52 OK) — AS-IS named-arg 호출부를 그대로
//      옮기면 됨(치환은 IEnumerator→Task뿐).
//
// ③ 배선 관례 근거: [When Digivolving] → OnEnterFieldAnyone + CanTriggerWhenDigivolving(hashtable, card) 그대로
//    (AS-IS :35 그대로). [Your Turn] 블록은 AS-IS 자체가 7개 복수 EffectTiming(AfterEffectsActivate/
//    OnStartTurn/OnEnterFieldAnyone/OnUseOption/OptionSkill/SecuritySkill/WhenRemoveField)에 동일 등록되는
//    배경 프로세스 — 방언 변환 대상 아님, 값 그대로.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`
//      (BT8_092 idiom).
//    * `CardEffectCommons.HasMatchConditionPermanent(CanSelectPermanentCondition)`(구식, card 없음) →
//      `CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition)` — Permanent-술어
//      오버로드가 존재하므로(CardEffectCommons.cs:4079) id 어댑터 불필요, card 파라미터만 추가
//      (symbol_map_guide §2.3, BT1_017 idiom).
//    * `CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition)`도 Permanent-술어
//      오버로드(CardEffectCommons/KeyWordEffects/Save.cs:25) — SelectPermanentEffect.SetUp의 canTargetCondition
//      역시 정본 Func&lt;Permanent,bool&gt; 오버로드(id-flip 3b)라 동일 술어를 id 어댑터 없이 세 호출부 전부에 직결.
//    * `card.Owner.Enemy.GetBattleAreaDigimons()` → `new Player(card.Context, card.Owner)
//      .Enemy!.GetBattleAreaDigimons()` (Player.Enemy=Player? Player.cs:43; 2인전 비-null → null-forgiving;
//      BT18_042:166 established idiom).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (BT17_026 idiom;
//      EffectList/DigivolutionCards 접근에 가변 Permanent 필요).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT12.Yellow;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT12_044 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region When Digivolving

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Security Attack -2", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Until the end of your opponent's turn, 1 of your opponent's Digimon gains <Security Attack -2>. (This Digimon checks 2 fewer security cards.)";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
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
                        "Select 1 Digimon that will get Security Attack -2.",
                        "The opponent is selecting 1 Digimon that will get Security Attack -2.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.ChangeDigimonSAttack(
                            targetPermanent: permanent,
                            changeValue: -2,
                            effectDuration: EffectDuration.UntilOpponentTurnEnd,
                            activateClass: activateClass);
                    }
                }

                //Initial check for your turn effect

                int count()
                {
                    return new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons().Count((permanent) => permanent.HasSecurityAttackChanges);
                }

                List<ICardEffect> SecFromLamp = ICardEffect.ResolvePermanentOfThisCard(card).EffectList(EffectTiming.None).Where(x => x.HashString == "SecAttackFromLampEffect").ToList();

                if (count() > SecFromLamp.Count())
                {
                    int timesToRunLoop = count() - SecFromLamp.Count();

                    for (int i = 0; i < timesToRunLoop; i++)
                    {
                        await CardEffectCommons.ChangeDigimonSAttack(
                        targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),
                        changeValue: 1,
                        effectDuration: EffectDuration.UntilEachTurnEnd,
                        activateClass: activateClass,
                        activateAnimation: false,
                        hashstring: "SecAttackFromLampEffect");
                    }
                }
            }
        }

        #endregion

        #region Your Turn

        if (timing == EffectTiming.AfterEffectsActivate || timing == EffectTiming.OnStartTurn || timing == EffectTiming.OnEnterFieldAnyone || timing == EffectTiming.OnUseOption || timing == EffectTiming.OptionSkill || timing == EffectTiming.SecuritySkill || timing == EffectTiming.WhenRemoveField)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Gain Security Attack", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsBackgroundProcess(true);
            cardEffects.Add(activateClass);

            int count()
            {
                return new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons().Count((permanent) => permanent.HasSecurityAttackChanges);
            }

            string EffectDiscription()
            {
                return "[Your Turn] This Digimon gains [Security A+1] for each of your opponent's Digimon with [Security Attack].";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            bool OpponentHasDigimonWithSecChanges(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.IsDigimon)
                    {
                        if (permanent.HasSecurityAttackChanges)
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
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.HasMatchConditionPermanent(card, OpponentHasDigimonWithSecChanges))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                List<ICardEffect> SecFromLamp = ICardEffect.ResolvePermanentOfThisCard(card).EffectList(EffectTiming.None).Where(x => x.HashString == "SecAttackFromLampEffect").ToList();

                if (count() > SecFromLamp.Count())
                {
                    int timesToRunLoop = count() - SecFromLamp.Count();

                    for (int i = 0; i < timesToRunLoop; i++)
                    {
                        await CardEffectCommons.ChangeDigimonSAttack(
                        targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),
                        changeValue: 1,
                        effectDuration: EffectDuration.UntilEachTurnEnd,
                        activateClass: activateClass,
                        activateAnimation: false,
                        hashstring: "SecAttackFromLampEffect");
                    }
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
