// Source: Assets/Scripts/CardEffect/BT2/Purple/BT2_008.cs
// Decision: PARTIAL PORT
// Category: CardEffect
// STOP: card.Owner.TrashCards.Count >= 5 조건 → IZoneStateReader / ChoiceZone 타입 미존재로 쓰레기통 매수 쿼리 불가; None 타이밍 블록 전체 생략

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_008 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: card.Owner.TrashCards.Count >= 5 조건을 표현할 IZoneStateReader / ChoiceZone 프리미티브가 없어
        //       Condition() 술어를 충실히 구현할 수 없으므로 EffectTiming.None 블록 전체를 등록하지 않는다.

        return cardEffects;
    }
}