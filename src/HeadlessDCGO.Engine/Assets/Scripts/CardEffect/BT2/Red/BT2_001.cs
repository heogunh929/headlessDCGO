// Source: Assets/Scripts/CardEffect/BT2/Red/BT2_001.cs
// Decision: PORT (partial)
// Category: CardEffect
// Migration: Ported per-card effect.
//
// STOP: opponent trash count query — IZoneStateReader / ChoiceZone not accessible in this context;
// AS-IS requires card.Owner.Enemy.TrashCards.Count >= 5 but no resolvable headless primitive exists.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_001 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: IZoneStateReader / ChoiceZone 미발견 — 상대 트래시 카드 수(>= 5) 조건을
        // 헤드리스에서 조회할 수 있는 접근 가능한 프리미티브가 없음. 효과 미등록.

        return cardEffects;
    }
}