// Source: DCGO/Assets/Scripts/CardEffect/ST2/Blue/ST2_12.cs
// TRUE AS-IS-verbatim re-port (batch: ST2 Blue). 1:1 mirror of the original ST2_12 (a Tamer).
//   [Start of Your Turn] If your opponent has a Digimon with no digivolution cards, gain 1 memory.
//   [Security] Play this Tamer.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.AddMemoryTriggerEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure. The
// [Security] block already used the REAL AS-IS `CardEffectFactory.PlaySelfTamerSecurityEffect(card)` factory
// call (verified against AS-IS CardEffectFactory) — kept unchanged.
// AS-IS structure kept verbatim: inline ActivateClass, no SetIsInheritedEffect, no SetHashString; note AS-IS
// uses `isExistOnField(card)` (the CEntity_Effect base-class helper — NOT `IsExistOnBattleArea`) inside
// CanActivateCondition/ActivateCoroutine, distinct from the `IsExistOnBattleArea` used inside CanUseCondition.
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `card.Owner.CanAddMemory(activateClass)` / `card.Owner.AddMemory(1, activateClass)` -> the
// `HeadlessPlayerId` extensions (`PlayerIdAsIsExtensions`, Player.cs) established in BT2_010.cs.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class ST2_12 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Start of Your Turn] If your opponent has a Digimon with no digivolution cards, gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (isExistOnField(card))
                {
                    if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, permanent => permanent.IsDigimon && CardEffectCommons.HasNoDigivolutionCards(card, permanent.InstanceId)))
                    {
                        if (card.Owner.CanAddMemory(activateClass))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (isExistOnField(card))
                {
                    if (card.Owner.CanAddMemory(activateClass))
                    {
                        await card.Owner.AddMemory(1, activateClass);
                    }
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
