// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_115.cs
// 1:1 mirror of the original BT1_115 (BT1/Blue) — a Digimon, mixed timings.
//   [When Attacking][Once Per Turn] If you have a Tamer in play, unsuspend this Digimon.
//     -> AS-IS ActivateClass on OnAllyAttack: CanUseCondition = CanTriggerOnAttack(hashtable, card);
//        CanActivateCondition = IsExistOnBattleArea(card) && HasMatchConditionOwnersPermanent(card,
//        permanent => permanent.IsTamer); ORDER=1 (once per turn), ISOPTIONAL=false; ActivateCoroutine =
//        IUnsuspendPermanents(self).Unsuspend(). Headless mirror: CardEffectFactory.UnsuspendSelfTriggerEffect
//        (the TriggeredUnsuspendSelfEffect primitive, mirrors ST2_11) with maxCountPerTurn=1 + hash
//        ("Unsuspend_BT1_115", mirrors AS-IS SetHashString) and the trigger gate combining CanTriggerOnAttack
//        with the "you have a Tamer in play" precondition (both AS-IS gates fold into the single triggerGate
//        param this primitive exposes; CanResolve there checks only the gate, so AND-ing both conditions is
//        a verbatim behavioral mirror).
//   Inherited [Your Turn]-less continuous: if you have a blue Tamer in play, this Digimon gets +1000 DP.
//     -> AS-IS static effect (timing == EffectTiming.None): condition = IsExistOnBattleArea(card) &&
//        HasMatchConditionOwnersPermanent(card, permanent => permanent.TopCard.CardColors.Contains(Blue)
//        && permanent.IsTamer). -> CardEffectFactory.ChangeSelfDPStaticEffect(+1000, isInheritedEffect:true,
//        condition) (mirrors BT1_004/BT1_003-adjacent static-effect shape; color check via
//        Permanent.TopCard.HasCardColor("Blue") — headless colors are strings, no CardColor enum, per the
//        porting standard, mirrors BT1_053/BT1_085/ST4_03/BT1_048).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_115 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanActivateCondition() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.HasMatchConditionOwnersPermanent(card, permanent => permanent.IsTamer);

            cardEffects.Add(CardEffectFactory.UnsuspendSelfTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                card: card,
                description: "[When Attacking][Once Per Turn] If you have a Tamer in play, unsuspend this Digimon.",
                maxCountPerTurn: 1,
                hash: "Unsuspend_BT1_115",
                triggerGate: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card) && CanActivateCondition()));
        }

        if (timing == EffectTiming.None)
        {
            bool Condition() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.HasMatchConditionOwnersPermanent(
                    card, permanent => permanent.TopCard.HasCardColor("Blue") && permanent.IsTamer);

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(
                changeValue: 1000,
                isInheritedEffect: true,
                card: card,
                condition: Condition));
        }

        return cardEffects;
    }
}
