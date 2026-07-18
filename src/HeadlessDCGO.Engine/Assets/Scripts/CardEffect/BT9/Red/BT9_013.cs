// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S5 카드 — BT9_013 (Digimon / Red)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT9/Red/BT9_013.cs (84 lines, no region markers, 3 timing 블록)
//    * Alternate Digivolution :16-24 (timing == None — AddSelfDigivolutionRequirementStaticEffect)
//    * [Blitz] self ESS       :26-29 (timing == OnEnterFieldAnyone — BlitzSelfEffect, isWhenDigivolving: true)
//    * ESS CanAttackTargetDefendingPermanentClass :31-80 (timing == None — 표면 실존 확인됨, 아래 참조)
//
// ② 프리미티브 매핑:
//    * P:AddDigivolutionRequirementClass(AddSelfDigivolutionRequirementStaticEffect) — [OmniShoutmon] 위 코스트 0
//      (AS-IS :18-23; BT9_009/BT9_081/BT9_111 established idiom).
//    * K:Blitz(BlitzSelfEffect) — [Blitz] self ESS, isWhenDigivolving:true (AS-IS :28; BT5_086 established idiom).
//    * P:CanAttackTargetDefendingPermanentClass — ESS, 진화원에 [OmniShoutmon]/[X Antibody] 있으면 자기 턴에
//      상대 언서스펜드 디지몬 공격 허용 (AS-IS :31-80). 표면 실존 확인: 클래스 자체가 미러
//      Script/CardEffects/CanAttackTargetDefendingPermanentClass.cs에 1:1 이식돼 있고, CardEffectCommons/
//      KeyWordEffects/Execute.cs:57-63가 동일 SetUpICardEffect/SetUpCanAttackTargetDefendingPermanentClass
//      호출 패턴으로 실사용 중(런타임 소비 경로 확인) — R4 인프라 골이 손대지 않은 축이라는 사용자 사전-경고와
//      달리 실제로는 이미 정착된 프리미티브. 목표대로 표면 확인 우선했고, STOP 불필요.
//
// ③ 배선 관례 근거: [Blitz] self ESS는 AS-IS 그대로 OnEnterFieldAnyone(BlitzSelfEffect 팩토리 1:1).
//    ESS CanAttackTargetDefendingPermanentClass는 AS-IS 자체가 timing == None(상시 등록, 발화창 없음).
//
// 치환(substrate translations only):
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (BT8_092/BT17_026 idiom;
//      DigivolutionCards 접근에 가변 Permanent 필요).
//    * `permanent.TopCard.Owner.GetBattleAreaPermanents().Contains(permanent)` (AS-IS :66) — TopCard.Owner는
//      이미 HeadlessPlayerId이고 GetBattleAreaPermanents()는 그 위의 확장 메서드(Player.cs:924, symbol_map_guide
//      §2.2 예외 조항) — 문법 변경 없이 그대로 사용 가능.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.Red;

using System.Collections;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT9_013 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.CardNames.Contains("OmniShoutmon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 0, ignoreDigivolutionRequirement: true, card: card, condition: null));
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            cardEffects.Add(CardEffectFactory.BlitzSelfEffect(isInheritedEffect: false, card: card, condition: null, isWhenDigivolving: true));
        }

        if (timing == EffectTiming.None)
        {
            CanAttackTargetDefendingPermanentClass canAttackTargetDefendingPermanentClass = new CanAttackTargetDefendingPermanentClass();
            canAttackTargetDefendingPermanentClass.SetUpICardEffect($"Can attack to unsuspended Digimon", CanUseCondition, card);
            canAttackTargetDefendingPermanentClass.SetUpCanAttackTargetDefendingPermanentClass(attackerCondition: AttackerCondition, defenderCondition: DefenderCondition, cardEffectCondition: CardEffectCondition);

            cardEffects.Add(canAttackTargetDefendingPermanentClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count((cardSource) => cardSource.CardNames.Contains("OmniShoutmon") || cardSource.CardNames.Contains("X Antibody") || cardSource.CardNames.Contains("XAntibody")) >= 1)
                    {
                        if (CardEffectCommons.IsOwnerTurn(card))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool AttackerCondition(Permanent permanent)
            {
                return permanent == ICardEffect.ResolvePermanentOfThisCard(card);
            }

            bool DefenderCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (!permanent.IsSuspended)
                    {
                        if (permanent.TopCard.Owner.GetBattleAreaPermanents().Contains(permanent))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CardEffectCondition(ICardEffect cardEffect)
            {
                return true;
            }
        }

        return cardEffects;
    }
}
