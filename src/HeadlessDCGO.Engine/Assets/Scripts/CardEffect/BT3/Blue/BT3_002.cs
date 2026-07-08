// Source: Assets/Scripts/CardEffect/BT3/Blue/BT3_002.cs
// 1:1 mirror of the original BT3_002 (BT3/Blue) — a Digimon.
//   [When Attacking][Once Per Turn] If this Digimon has <Jamming>, trigger <Draw 1>. (Draw 1 card from your
//   deck.) AS-IS: ActivateClass on OnAllyAttack, CanUseCondition = CanTriggerOnAttack(hashtable, card),
//   CanActivateCondition = IsExistOnBattleArea(card) && card.PermanentOfThisCard().HasJamming, ORDER=1 (once
//   per turn), ISOPTIONAL=false, ActivateCoroutine = DrawClass(owner, 1).Draw().
// Headless mirror: uniform ActivatedEffect (= AS-IS ActivateClass) with body=DrawBody(1) — same shape as
// BT1_006's conditional [When Attacking] draw. permanent.HasJamming (keyword-possession query) is mirrored
// via ContinuousKeywordGate.HasKeyword (porting cheatsheet section 9 — the possession query, not the
// self-static grant). AS-IS SetIsInheritedEffect(true) is not modeled by the triggered-effect primitive
// (same accepted omission as BT1_006, whose AS-IS also sets SetIsInheritedEffect(true) but whose existing
// headless port does not carry it).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;

public sealed class BT3_002 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && ContinuousKeywordGate.HasKeyword(card.Context, card.InstanceId, ContinuousKeywordGate.Jamming);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAllyAttack,
                canUse: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card),
                canActivate: CanActivate,
                body: new DrawBody(1),
                maxCountPerTurn: 1,
                isOptional: false,
                description: "[When Attacking][Once Per Turn] If this Digimon has <Jamming>, trigger <Draw 1>. (Draw 1 card from your deck.)"));
        }

        return cardEffects;
    }
}
