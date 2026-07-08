// Source: Assets/Scripts/CardEffect/BT2/BT2_006.cs
// Decision: PORT (partial — None timing STOP)
// Category: CardEffect
// Migration: Ported per-card effect.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_006 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: HeadlessEntityId 기반으로 다른 permanent의 TopCard 카드 이름을 비교하는 commons 술어 없음
        // AS-IS: permanent.TopCard.HasSameCardName(card.PermanentOfThisCard().TopCard) —
        //   동명 카드 존재 조건(HasMatchConditionOwnersPermanent 내부)을 id 기반으로 표현할 프리미티브 부재
        //   (inherited DP +2000 / [Your Turn] / "동명 아군 디지몬 존재" 조건 전체가 미등록)

        return cardEffects;
    }
}