// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S3 카드 — BT25_004 (Digimon / Green, Tapmon)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Green/BT25_004.cs (73 lines, 1 region + shared conditions)
//    * [Your Turn] Reduce Link Cost :32-67 (timing == EffectTiming.WhenWouldLink — GrantedReduceLinkCostClass
//      grant, isInheritedEffect, isOptional, Once Per Turn(maxCount 1), HashString "BT25_004_YT")
//
// ② 프리미티브 매핑:
//    * P:GrantedReduceLinkCostClass — WhenWouldLink 그랜트 등재 (AS-IS :57-63).
//    * (+X:GLink WhenWouldLink 창 — EXEMPLAR-GLINK 참조. 이 카드는 WhenWouldLink 축의 유일 실카드로
//      G-Link 원장 P2-④(창-변조 witness)를 이 witness로 해소한다.)
//
// ③ 배선 관례 근거:
//    * [Your Turn] WhenWouldLink → EffectTiming.WhenWouldLink 그대로 (AS-IS :32 그대로; 미러 방언 변환 없음
//      — trigger-wiring-porting-rules에 WhenWouldLink 항목 없음, AS-IS 타이밍 키 그대로 유지).
//    * AS-IS-signature CanTriggerWhenWouldLink(hashtable, cardCondition, permanentCondition, ...) 오버로드는
//      CardEffectCommons/CanUseEffects/WhenWouldLink.cs:14에 실존(EXEMPLAR-GLINK W2가 쓰는 ctx-형과는 별도
//      오버로드) — AS-IS 그대로 사용.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task; ActivateCoroutine 본문은 grant 등록 1문 + `yield return null`(실제 await 없음)
//      → non-async Task 메서드(BT8_092 idiom, "lone terminal yield return null" 규칙).
//    * `card.Owner.UntilCalculateFixedCostEffect` → `new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect`
//      (symbol_map_guide §2.2 Player-is-now-a-PlayerId 규칙).
//    * `permanent == card.PermanentOfThisCard()` (PermanentCondition 몸통, AS-IS :25) — 값-동등 비교이므로
//      `ICardEffect.ResolvePermanentOfThisCard(card)` 사용(§2.3 규칙: value-equality 지점은 Resolve 필요).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_004 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Shared Conditions

        bool CardCondition(CardSource cardSource)
        {
            return cardSource.EqualsTraits("Social")
                || cardSource.EqualsTraits("Tool")
                || cardSource.EqualsTraits("Game");
        }

        bool PermanentCondition(Permanent permanent) => permanent == ICardEffect.ResolvePermanentOfThisCard(card);

        bool RootCondition(SelectCardEffect.Root root) => true;

        #endregion

        #region Reduce Link Cost

        if (timing == EffectTiming.WhenWouldLink)
        {
            ActivateClass activateClass = new();
            activateClass.SetUpICardEffect("May reduce Link cost by 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("BT25_004_YT");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] [Once Per Turn] When a [Social], [Tool] or [Game] trait card would link to this Digimon, you may reduce the cost by 1.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerWhenWouldLink(hashtable, CardCondition, PermanentCondition)
                    && CardEffectCommons.IsOwnerTurn(card);
            }

            bool CanActivateCondition(Hashtable hashtable) => CardEffectCommons.IsExistOnBattleArea(card);

            Task ActivateCoroutine(Hashtable hashtable)
            {
                new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add((_) => CardEffectFactory.GrantedReduceLinkCostClass(
                    card: card,
                    reducedCost: 1,
                    cardSourceCondition: CardCondition,
                    permanentCondition: PermanentCondition,
                    rootCondition: RootCondition
                ));

                return Task.CompletedTask;
            }
        }

        #endregion

        return cardEffects;
    }
}
