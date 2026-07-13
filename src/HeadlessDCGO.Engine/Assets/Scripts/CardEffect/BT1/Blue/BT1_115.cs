// Source: DCGO/Assets/Scripts/CardEffect/BT1/Blue/BT1_115.cs
// TRUE AS-IS-verbatim re-port (P5 batch 2). 1:1 mirror of the original BT1_115 (BT1/Blue) — a Digimon, mixed
// timings.
//   [When Attacking][Once Per Turn] If you have a Tamer in play, unsuspend this Digimon.
//   Static (timing == None): if you have a blue Tamer in play, this Digimon gets +1000 DP (inherited).
// AS-IS structure kept verbatim: inline ActivateClass (ORDER=1, SetHashString) for the attack half;
// AS-IS `card.PermanentOfThisCard()` (used as a Permanent) -> ICardEffect.ResolvePermanentOfThisCard(card).
// For `timing == EffectTiming.None`, AS-IS builds a `CardEffectFactory.ChangeSelfDPStaticEffect`-shaped static
// entry directly (not an ActivateClass) — kept via the same bridged factory (verified 1:1 static-effect
// primitive, mirrors BT1_004 family) since AS-IS itself calls this exact factory method, not an inline class.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT1_115 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Unsuspend this Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetHashString("Unsuspend_BT1_115");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking][Once Per Turn] If you have a Tamer in play, unsuspend this Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersPermanent(card, permanent => permanent.IsTamer))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass.EffectSourceCard?.InstanceId).Unsuspend();
            }
        }

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersPermanent(card, permanent => permanent.TopCard.HasCardColor("Blue") && permanent.IsTamer))
                    {
                        return true;
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(
                changeValue: 1000,
                isInheritedEffect: true,
                card: card,
                condition: Condition));
        }

        return cardEffects;
    }
}
