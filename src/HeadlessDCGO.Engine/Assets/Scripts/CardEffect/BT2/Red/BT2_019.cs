namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_019 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: AS-IS는 OnAllyAttack 타이밍에 DefendingPermanent == null(수비 Permanent 없음 = 플레이어 직접 공격)을
        // 발동 조건으로 요구하나, 헤드리스 CardEffectCommons 및 triggerGate(Func<CardEffectResolveContext,bool>)에
        // 공격 대상이 Digimon이 아닌 플레이어임을 확인하는 primitive가 없음 — 조건을 충실히 재현 불가로 블록 전체 등록 생략

        return cardEffects;
    }
}