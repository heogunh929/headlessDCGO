// Source: Assets/Scripts/CardEffect/BT2/Yellow/BT2_033.cs
// [When Attacking][Inherited] If you have 3 or more yellow Tamers in play, <Draw 1>.
// STOP: OnAllyAttack — CanActivateCondition "자신 필드에 노란색 Tamer 3체 이상" 조건:
//       HeadlessEntityId predicate 내에서 permanent 의 색상을 조회하는
//       CardEffectCommons 술어가 문서화되지 않아 조건부 DrawCardsEffect 를 충실히 구현 불가.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_033 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        // STOP: "자신 필드에 노란색 Tamer 3체 이상" 조건 — HeadlessEntityId predicate 내 permanent 색상 조회 Commons 없음
        if (timing == EffectTiming.OnAllyAttack)
        {
        }

        return cardEffects;
    }
}