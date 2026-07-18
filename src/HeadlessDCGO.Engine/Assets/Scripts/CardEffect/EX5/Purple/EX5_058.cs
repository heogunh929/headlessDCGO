// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S4 카드 — EX5_058 (Digimon / Purple)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX5/Purple/EX5_058.cs (145 lines, 3 timing 블록 — 전부
//    timing == EffectTiming.OnEnterFieldAnyone)
//    * [On Play]            :10-49  — 4장↑ 필드 시 자기 배틀에어리어, 3장↓면 상대 배틀에어리어에 [Fujitsumon]
//      토큰 서스펜드 플레이
//    * [When Digivolving]   :51-90  — 동일 몸통(EffectDescription만 "[When Digivolving]"로 상이)
//    * [All Turns][Once]    :92-141 — 효과로 상대 디지몬이 플레이되면 메모리 +1, isInheritedEffect
//
// ② 프리미티브 매핑:
//    * P:PlayFujitsumonToken — [On Play]/[When Digivolving] 공유 몸통 (AS-IS :47,88; CardEffectCommons.cs:225).
//    * P:AddMemory (extension) — [All Turns] 몸통 (AS-IS :139).
//
// ③ 배선 관례 근거: AS-IS 자체가 [On Play]와 [When Digivolving] 둘 다 timing ==
//    EffectTiming.OnEnterFieldAnyone 한 키에 등록(CanUseCondition만 CanTriggerOnPlay vs
//    CanTriggerWhenDigivolving로 분기) — trigger-wiring rule 3([When Digivolving]→WhenDigivolving 전용 키
//    "이중-키 등록 금지")은 AS-IS가 OnEnterFieldAnyone에 WD 게이트를 다는 경우에 적용되는데, 이 카드는
//    실제로 두 arm이 이미 SEPARATE ActivateClass(별개 cardEffects.Add 호출)로 등록되어 있으므로, 두 번째
//    arm만 WhenDigivolving 전용 키로 재배선하고(BT17_026/BT22_040/EX7_072 확립 관례), 첫 번째 arm은
//    OnEnterFieldAnyone에 그대로 둔다(이중-키 등록 방지).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine→await (BT8_092 idiom).
//    * `card.Owner.GetBattleAreaDigimons()` → HeadlessPlayerId 확장 그대로 (symbol_map_guide §2.2 예외절;
//      GetBattleAreaDigimons는 id 위 확장 메서드).
//    * `card.Owner.Enemy.GetBattleAreaDigimons()` → `CardEffectCommons.OpponentOf(card).GetBattleAreaDigimons()`
//      (§2.2 상대 id 단축형; Enemy는 Player 인스턴스 필요 없이 상대 id만 필요).
//    * AS-IS :34 `player.fieldCardFrames.Count(frame => frame.IsEmptyFrame() && frame.IsBattleAreaFrame()) >= 1`
//      — 미러 frame/slot 모델 부재(RD-P6C1-1/-2). 이 카드의 CanActivateCondition은 전부가 이 빈-프레임
//      용량 체크로만 구성(공-conjunct 없음) — BT19_091 관례(§2.8) 적용 시 co-conjunct가 없으므로 체크 전체
//      소거, `IsExistOnBattleArea(card)` 단독으로 환원(토큰-플레이 커먼즈가 하류에서 용량 재확인).
//    * `PlayFujitsumonToken(activateClass, isOwnerPermanent)`(AS-IS 1st arg=ICardEffect) →
//      `PlayFujitsumonToken(card, isOwnerPermanent)`(mirror 1st arg=CardSource sourceCard; activateClass의
//      EffectSourceCard==card이므로 card 직접 전달 — ST3_13 AddThisCardToHand(card,card) idiom과 동형).
//    * `card.Owner.AddMemory(1, activateClass)` — HeadlessPlayerId 확장 그대로(§2.2 예외절).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX5.Purple;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX5_058 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 [Fujitsumon] token", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[On Play] If there are 4 or more total Digimon, play 1 [Fujitsumon] Token (Digimon/Purple/3000 DP/[All Turns] This Digimon doesn't unsuspend./[On Deletion] Trash 1 card in your hand.) suspended to your battle area. If there are 3 or fewer, play it suspended to your opponent's battle area.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                // AS-IS :29-38 — 빈-프레임 용량 체크 전체 소거(RD-P6C1-1, 위 헤더 참조).
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool isOwnerPermanent = card.Owner.GetBattleAreaDigimons().Count + CardEffectCommons.OpponentOf(card).GetBattleAreaDigimons().Count >= 4;

                await CardEffectCommons.PlayFujitsumonToken(card, isOwnerPermanent);
            }
        }

        #endregion

        #region When Digivolving

        // ③ 배선: AS-IS는 이 두 번째 arm도 OnEnterFieldAnyone(:51)에 등록하지만, 미러 방언은 WhenDigivolving
        //   전용 키(trigger-wiring rule 3 — 이중-키 등록 금지; BT17_026/EX7_072 확립 관례). 게이트 몸통은
        //   CanTriggerWhenDigivolving 그대로.
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 [Fujitsumon] token", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[When Digivolving] If there are 4 or more total Digimon, play 1 [Fujitsumon] Token (Digimon/Purple/3000 DP/[All Turns] This Digimon doesn't unsuspend./[On Deletion] Trash 1 card in your hand.) suspended to your battle area. If there are 3 or fewer, play it suspended to your opponent's battle area.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                // AS-IS :70-79 — 빈-프레임 용량 체크 전체 소거(RD-P6C1-1, 위 헤더 참조).
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool isOwnerPermanent = card.Owner.GetBattleAreaDigimons().Count + CardEffectCommons.OpponentOf(card).GetBattleAreaDigimons().Count >= 4;

                await CardEffectCommons.PlayFujitsumonToken(card, isOwnerPermanent);
            }
        }

        #endregion

        #region All Turns - Once Per Turn

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetHashString("Memory1_EX5_058");
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[All Turns] [Once Per Turn] When an effect plays an opponent's Digimon, gain 1 memory.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanTriggerOnPermanentPlay(hashtable, PermanentCondition))
                    {
                        if (CardEffectCommons.IsByEffect(hashtable, null))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        #endregion

        return cardEffects;
    }
}
