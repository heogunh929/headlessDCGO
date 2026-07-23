// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// PILOT-S1 카드 — BT9_009 (Digimon / Red)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT9/Red/BT9_009.cs (139 lines, no region markers, 3 timing 블록)
//    * Alternate Digivolution  :14-22 (timing == None — AddSelfDigivolutionRequirementStaticEffect)
//    * [When Digivolving]      :24-91 (timing == OnEnterFieldAnyone)
//    * ESS ChangeDPDeleteEffectMaxDPClass :93-135 (timing == None, inherited)
//
// ② 프리미티브 매핑:
//    * P:AddDigivolutionRequirementClass(AddSelfDigivolutionRequirementStaticEffect) — [Guilmon] 위 코스트 0
//      (AS-IS :16-21)
//    * E:SelectPermanentEffect Mode.Destroy — [When Digivolving] 3000DP 이하 상대 디지몬 1장 삭제 (AS-IS :67-89)
//    * P:ChangeDPDeleteEffectMaxDPClass — ESS, DP기반 삭제효과 상한 +1000 (AS-IS :95-134)
//
// ③ 배선 관례 근거:
//    * [When Digivolving] → OnEnterFieldAnyone + CanTriggerWhenDigivolving(hashtable, card) 게이트(AS-IS :49-52
//      그대로).
//    * ESS(ChangeDPDeleteEffectMaxDPClass)는 timing == None + SetIsInheritedEffect(true) — AS-IS 그대로.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`.
//    * `card.Owner.MaxDP_DeleteEffect(3000, activateClass)` → `new Player(card.Context, card.Owner)
//      .MaxDP_DeleteEffect(3000, activateClass)` (미러 Player 접근 idiom).
//    * `CardEffectCommons.HasMatchConditionPermanent(CanSelectPermanentCondition)`(구식, card 없음) →
//      `CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition)` (미러는 컨텍스트
//      획득을 위해 card 파라미터 필수 — 스캔 범위(양측 배틀에어리어)는 AS-IS와 동일).
//    * `CardEffectCommons.MatchConditionPermanentCount(CanSelectPermanentCondition)` → `(card, cond)` 오버로드;
//      `SelectPermanentEffect.SetUp`의 canTargetCondition은 정본 Func<Permanent,bool> — 술어 직결(id 어댑터 없음).
//    * `isExistOnField(card)` — CEntity_Effect 상속 정적 헬퍼, 미러에 동일 존재(ST1_09/BT2_087 idiom) 그대로.
//    * `card.Owner.GetBattleAreaDigimons()` → HeadlessPlayerId 확장 메서드(Player.cs:914) 그대로 사용 가능.
//    * `card.PermanentOfThisCard()`(가변 비교 필요) → `ICardEffect.ResolvePermanentOfThisCard(card)` (BT8_092 관례).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.Red;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT9_009 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.CardNames.Contains("Guilmon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 0, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 Digimon with 3000 DP or less", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Delete 1 of your opponent's Digimon with 3000 DP or less.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.DP <= new Player(card.Context, card.Owner).MaxDP_DeleteEffect(3000, activateClass))
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
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        if (timing == EffectTiming.None)
        {
            ChangeDPDeleteEffectMaxDPClass changeDPDeleteEffectMaxDPClass = new ChangeDPDeleteEffectMaxDPClass();
            changeDPDeleteEffectMaxDPClass.SetUpICardEffect("Maximum DP of DP-based deletion effects gets +1000 DP", CanUseCondition, card);
            changeDPDeleteEffectMaxDPClass.SetUpChangeDPDeleteEffectMaxDPClass(changeMaxDP: ChangeMaxDP);

            changeDPDeleteEffectMaxDPClass.SetIsInheritedEffect(true);
            cardEffects.Add(changeDPDeleteEffectMaxDPClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (isExistOnField(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (card.Owner.GetBattleAreaDigimons().Contains(ICardEffect.ResolvePermanentOfThisCard(card)))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            int ChangeMaxDP(int maxDP, ICardEffect cardEffect)
            {
                if (cardEffect != null)
                {
                    if (cardEffect.EffectSourceCard != null)
                    {
                        if (cardEffect.EffectSourceCard.Owner == card.Owner)
                        {
                            maxDP += 1000;
                        }
                    }
                }

                return maxDP;
            }
        }

        return cardEffects;
    }
}
