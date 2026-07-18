// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T2B 정본 카드 — Goldramon (BT14_018, Digimon / Red / Lv.6)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT14/Red/BT14_018.cs (292 lines, 4 regions)
//    * [On Play]           :14-50  (OnEnterFieldAnyone + CanTriggerOnPlay — Amon·Umon 토큰 2종 플레이)
//    * [When Digivolving]  :52-88  (OnEnterFieldAnyone + CanTriggerWhenDigivolving — 동일 본체)
//    * [All Turns]would-evo:90-188 (BeforePayCost — 진화 예정 시 Amon/Umon 전삭제 + <Recovery+1(Deck)>)
//    * [All Turns]leave    :190-288(WhenRemoveField — 배틀에어리어 이탈 시 동일)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #23):
//    * P:PlayAmonToken   — [On Play]/[When Digivolving] 몸통 (AS-IS :46/:84; TokenSpecs["Amon"])
//    * P:PlayUmonToken   — [On Play]/[When Digivolving] 몸통 (AS-IS :48/:86; TokenSpecs["Umon"])
//    * (+P:DestroyPermanentsClass (AS-IS :175-179), IRecovery(deck) (AS-IS :183),
//       T:BeforePayCost/WhenRemoveField, CanTriggerWhenPermanentWouldDigivolveOfCard)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(미러 방언 rule 3; AS-IS는 OnEnterFieldAnyone에
//      두지만 미러 DigivolveAction은 WhenDigivolving만 해소, 이중-키 등록 금지). CanUseCondition의
//      CanTriggerWhenDigivolving 게이트는 AS-IS 그대로 유지.
//    * [On Play] → OnEnterFieldAnyone + CanTriggerOnPlay(AS-IS :26-29 그대로).
//    * [All Turns] would-digivolve → BeforePayCost + CanTriggerWhenPermanentWouldDigivolveOfCard(hashtable, null,
//      card)(AS-IS :139-150 그대로); leave-field → WhenRemoveField + CanTriggerWhenRemoveField.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine(X)→await X (BT8_092 idiom); `yield return new WaitForSeconds(0.4f)`
//      = UI 연출 딜레이 — 미러 확립 관례상 스트립.
//    * AS-IS :35/:73 `card.Owner.fieldCardFrames.Count(빈 배틀프레임)>=1` = 빈-프레임 용량 절반은 미러
//      frame-모델 부재로 소거(확립 어댑테이션 RD-P6C1-2, BT19_091 헤더 동일 근거). IsExistOnBattleArea 절반은
//      1:1 유지. (토큰 커먼즈 자체가 capacity를 다시 검사 — CanPlayTokens.)
//    * `card.Owner.GetBattleAreaPermanents()` → `new Player(card.Context, card.Owner).*`; `.Filter` 확장 유지.
//    * AS-IS :183 `new IRecovery(card.Owner, 1, activateClass)` → 미러 ctor `(Context, playerId, count,
//      causeEffectSourceId)` (BT2_034 idiom).
//    * AS-IS :175-177 `new DestroyPermanentsClass(list, CardEffectHashtable(activateClass))` — 미러 동일 ctor.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT14.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT14_018 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region On Play
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play tokens", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Play 1 [Amon of Crimson Flame] (Digimon/Red/6000 DP/<Rush>) Token and 1 [Umon of Blue Thunder] (Digimon/Yellow/6000 DP/<Blocker>) Token.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                // AS-IS :35 `&& card.Owner.fieldCardFrames.Count(빈 배틀프레임)>=1` — 빈-프레임 용량 절반은
                // 미러 frame-모델 부재로 소거(RD-P6C1-2); 토큰 커먼즈가 capacity를 다시 검사.
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.PlayAmonToken(activateClass);

                await CardEffectCommons.PlayUmonToken(activateClass);
            }
        }
        #endregion

        #region When Digivolving
        // ③ 미러 방언: AS-IS OnEnterFieldAnyone(:52) + CanTriggerWhenDigivolving 게이트 → WhenDigivolving 전용 키.
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play tokens", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Play 1 [Amon of Crimson Flame] (Digimon/Red/6000 DP/<Rush>) Token and 1 [Umon of Blue Thunder] (Digimon/Yellow/6000 DP/<Blocker>) Token.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.PlayAmonToken(activateClass);

                await CardEffectCommons.PlayUmonToken(activateClass);
            }
        }
        #endregion

        #region All Turns - would digivolve
        if (timing == EffectTiming.BeforePayCost)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete tokens and Recovery +1 (Deck)", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetHashString("Recovery1_BT14_018");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns] When this Digimon would digivolve or leave the battle area, delete all of your [Amon of Crimson Flame] and [Umon of Blue Thunder]. If this effect deletes, <Recovery +1 (Deck)>.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card))
                {
                    if (permanent.TopCard.CardNames.Contains("Amon of Crimson Flame"))
                    {
                        return true;
                    }

                    if (permanent.TopCard.CardNames.Contains("AmonofCrimsonFlame"))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool PermanentCondition1(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card))
                {
                    if (permanent.TopCard.CardNames.Contains("Umon of Blue Thunder"))
                    {
                        return true;
                    }

                    if (permanent.TopCard.CardNames.Contains("UmonofBlueThunder"))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanTriggerWhenPermanentWouldDigivolveOfCard(hashtable, null, card))
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
                    if (CardEffectCommons.HasMatchConditionPermanent(card, PermanentCondition))
                    {
                        return true;
                    }

                    if (CardEffectCommons.HasMatchConditionPermanent(card, PermanentCondition1))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<Permanent> destroyTargetPermanents = new Player(card.Context, card.Owner).GetBattleAreaPermanents()
                    .Filter(permanent => PermanentCondition(permanent) || PermanentCondition1(permanent));

                DestroyPermanentsClass destroyPermanentsClass = new DestroyPermanentsClass(
                    destroyTargetPermanents,
                    CardEffectCommons.CardEffectHashtable(activateClass));

                await destroyPermanentsClass.Destroy();

                if (destroyPermanentsClass.DestroyedPermanents.Count >= 1)
                {
                    await new IRecovery(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Recovery();

                    // AS-IS :185 `yield return new WaitForSeconds(0.4f)` = UI 딜레이 — 스트립.
                }
            }
        }
        #endregion

        #region All Turns - leave field
        if (timing == EffectTiming.WhenRemoveField)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete tokens and Recovery +1 (Deck)", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetHashString("Recovery1_BT14_018");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns] When this Digimon would digivolve or leave the battle area, delete all of your [Amon of Crimson Flame] and [Umon of Blue Thunder]. If this effect deletes, <Recovery +1 (Deck)>.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card))
                {
                    if (permanent.TopCard.CardNames.Contains("Amon of Crimson Flame"))
                    {
                        return true;
                    }

                    if (permanent.TopCard.CardNames.Contains("AmonofCrimsonFlame"))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool PermanentCondition1(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card))
                {
                    if (permanent.TopCard.CardNames.Contains("Umon of Blue Thunder"))
                    {
                        return true;
                    }

                    if (permanent.TopCard.CardNames.Contains("UmonofBlueThunder"))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanTriggerWhenRemoveField(hashtable, card))
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
                    if (CardEffectCommons.HasMatchConditionPermanent(card, PermanentCondition))
                    {
                        return true;
                    }

                    if (CardEffectCommons.HasMatchConditionPermanent(card, PermanentCondition1))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<Permanent> destroyTargetPermanents = new Player(card.Context, card.Owner).GetBattleAreaPermanents()
                    .Filter(permanent => PermanentCondition(permanent) || PermanentCondition1(permanent));

                DestroyPermanentsClass destroyPermanentsClass = new DestroyPermanentsClass(
                    destroyTargetPermanents,
                    CardEffectCommons.CardEffectHashtable(activateClass));

                await destroyPermanentsClass.Destroy();

                if (destroyPermanentsClass.DestroyedPermanents.Count >= 1)
                {
                    await new IRecovery(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Recovery();

                    // AS-IS :285 `yield return new WaitForSeconds(0.4f)` = UI 딜레이 — 스트립.
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
