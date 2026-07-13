// Source: Assets/Scripts/CardEffect/BT2/BT2_015.cs
// [When Attacking] When this Digimon attacks a player, trigger <Draw 1>. (Draw 1 card from your deck.)
// STOP: DefendingPermanent == null (공격 대상이 Digimon이 아닌 플레이어임을 판별) 조건을
//       커버하는 헤드리스 predicate가 없음. CanTriggerOnAttack(ctx, card)만으로는 공격 대상이
//       플레이어인지 Digimon인지 구분 불가 — OnAllyAttack 타이밍 전체를 STOP 처리.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_015 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        // STOP: DefendingPermanent == null (is-attacking-player guard)을 표현하는
        //       헤드리스 commons predicate가 없어 canUse를 충실히 구성할 수 없음.
        if (timing == EffectTiming.OnAllyAttack)
        {
        }

        return cardEffects;
    }
}