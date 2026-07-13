// Source: DCGO/Assets/Scripts/CardEffect/ST1/Red/ST1_06.cs
// TRUE AS-IS-verbatim re-port (ST1/Red batch). 1:1 mirror of the original ST1_06.
//   [All Turns] <Blocker>
//   [When Attacking] Lose 2 memory.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.AddMemoryTriggerEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure.
// The `[All Turns] <Blocker>` static keyword effect is left untouched — it already calls the REAL AS-IS
// `CardEffectFactory.BlockerSelfStaticEffect(...)` (verified against DCGO/Assets/Scripts/Script/
// CardEffectFactory/KeyWordEffects/Blocker.cs), so no rewrite is needed for it.
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `card.Owner.AddMemory(-2, activateClass)` resolves against the `HeadlessPlayerId.AddMemory` extension
// (Script/Player.cs, PlayerIdAsIsExtensions).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class ST1_06 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory -2", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] Lose 2 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(-2, activateClass);
            }
        }

        return cardEffects;
    }
}
