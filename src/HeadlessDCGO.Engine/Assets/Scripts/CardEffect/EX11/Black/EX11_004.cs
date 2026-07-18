// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S4 카드 — EX11_004 (Kapurimon, Digimon / Black)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX11/Black/EX11_004.cs (47 lines, 1 region)
//    * [Your Turn][Once Per Turn] :15-42 (timing == EffectTiming.OnFaceUpSecurityIncreased — 상대 표지 시큐리티
//      증가 시 <Draw 1>, isInheritedEffect, SetHashString)
//
// ② 프리미티브 매핑:
//    * P:DrawClass — ActivateCoroutine 몸통 (AS-IS :40; new DrawClass(card.Owner, 1, activateClass).Draw()).
//    * T:OnFaceUpSecurityIncreased — 신규 창 타이밍 소비자. 표면 실존 확인:
//      EffectTiming.OnFaceUpSecurityIncreased 키 실존(CardController.cs:1683,1751 StackSkillInfos 발화),
//      CardEffectCommons.CanTriggerOnFaceUpSecurityIncreases(Hashtable, Player, Func<CardSource,bool>) 실장
//      (CanUseEffects/OnFaceUpSecurityIncrease.cs:13, 몸통 실재 — GetPlayerFromHashtable/GetCardSourcesFromHashtable
//      경유). 감사 시절 STOP-예상 불필요 — 그대로 포팅.
//
// ③ 배선 관례 근거: [Your Turn] 트리거 → AS-IS 타이밍 키 EffectTiming.OnFaceUpSecurityIncreased 그대로(미러
//    방언 변환 없음 — trigger-wiring-porting-rules에 해당 항목 없음).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine→await (BT8_092 idiom).
//    * `card.Owner.Enemy`(AS-IS Player) → `new Player(card.Context, card.Owner).Enemy!`
//      (symbol_map_guide §2.2 Player-is-now-a-PlayerId 규칙; CanTriggerOnFaceUpSecurityIncreases의 2번째
//      파라미터가 mirror Player? 형이라 그대로 전달).
//    * `new DrawClass(card.Owner, 1, activateClass).Draw()` → `new DrawClass(card.Context, card.Owner, 1,
//      activateClass).Draw()` (symbol_map §2.5: DrawClass ctor GAINS Context; EX10_045:439 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX11.Black;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX11_004 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnFaceUpSecurityIncreased)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("EX11_004_YT_Face_Up_Increase");
            cardEffects.Add(activateClass);

            string EffectDescription() => "[Your Turn] [Once Per Turn] When your opponent's face-up security cards increase, <Draw 1>.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.CanTriggerOnFaceUpSecurityIncreases(hashtable, new Player(card.Context, card.Owner).Enemy);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();
            }
        }

        return cardEffects;
    }
}
