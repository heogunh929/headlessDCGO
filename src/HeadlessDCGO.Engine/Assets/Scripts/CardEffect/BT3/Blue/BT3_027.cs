// Source: Assets/Scripts/CardEffect/BT3/Blue/BT3_027.cs
// 1:1 mirror of the original BT3_027 (BT3/Blue) — a Digimon.
//   Inherited continuous: this Digimon has <Jamming>. -> CardEffectFactory.JammingSelfStaticEffect
//     (timing == EffectTiming.None), verbatim.
//   [When Attacking][Once Per Turn] If this Digimon has [Imperialdramon] in its name, unsuspend it. AS-IS:
//     ActivateClass on OnAllyAttack, CanUseCondition = CanTriggerOnAttack(hashtable, card),
//     CanActivateCondition = IsExistOnBattleArea(card) && card.PermanentOfThisCard().TopCard.
//     ContainsCardName("Imperialdramon"), ORDER=1 (once per turn), ISOPTIONAL=false, ActivateCoroutine =
//     IUnsuspendPermanents(self).Unsuspend().
// Headless mirror: CardEffectFactory.UnsuspendSelfTriggerEffect (the TriggeredUnsuspendSelfEffect primitive,
// mirrors BT1_115/ST2_11) with maxCountPerTurn=1 + hash ("Unsuspend_BT3_027", mirrors AS-IS SetHashString)
// and a triggerGate combining CanTriggerOnAttack with the AS-IS CanActivateCondition (IsExistOnBattleArea(card)
// && name-contains "Imperialdramon") — all AS-IS gates fold into the single triggerGate param this primitive
// exposes, per BT1_115/BT3_029 precedent (which also AND in IsExistOnBattleArea).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_027 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.JammingSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.UnsuspendSelfTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                card: card,
                description: "[When Attacking][Once Per Turn] If this Digimon has [Imperialdramon] in its name, unsuspend it.",
                maxCountPerTurn: 1,
                hash: "Unsuspend_BT3_027",
                triggerGate: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card)
                    && CardEffectCommons.IsExistOnBattleArea(card)
                    && card.ContainsCardName("Imperialdramon")));
        }

        return cardEffects;
    }
}
