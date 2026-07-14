// Source: DCGO/Assets/Scripts/CardEffect/BT1/Green/BT1_081.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [End of Attack] branch (the
// [Piercing] OnDetermineDoSecurityCheck branch is already new-model, via CardEffectFactory.PierceSelfEffect,
// and is untouched).
//   [End of Attack][Twice Per Turn] You can unsuspend this Digimon by decreasing your memory by 3.
// AS-IS: ActivateClass on OnEndAttack, CanUseCondition = CanTriggerOnAttack, CanActivateCondition =
// IsExistOnBattleArea && card.Owner.MaxMemoryCost >= 3, ORDER=2 ([Twice Per Turn]), ISOPTIONAL=true,
// ActivateCoroutine = card.Owner.AddMemory(-3, activateClass) THEN IUnsuspendPermanents(self).Unsuspend().
// AS-IS structure kept verbatim: inline `new ActivateClass()` + local functions. Substrate translations only:
// IEnumerator->Task, StartCoroutine->await; `card.Owner.MaxMemoryCost` (AS-IS live Player property) ->
// `new Player(card.Context, card.Owner).MaxMemoryCost` (mirror `CardSource.Owner` is a bare HeadlessPlayerId,
// so the Player handle is reconstructed, the established BT1_104/BT2_023/BT9_109 idiom);
// `card.Owner.AddMemory(-3, activateClass)` -> the mirror HeadlessPlayerId extension (established
// BT1_114/BT1_030 idiom); `card.PermanentOfThisCard()` -> `ICardEffect.ResolvePermanentOfThisCard(card)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_081 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        if (timing == EffectTiming.OnEndAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Unsuspend this Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 2, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[End of Attack][Twice Per Turn] You can unsuspend this Digimon by decreasing your memory by 3.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).MaxMemoryCost >= 3)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(-3, activateClass);

                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass.EffectSourceCard?.InstanceId).Unsuspend();
            }
        }

        return cardEffects;
    }
}
