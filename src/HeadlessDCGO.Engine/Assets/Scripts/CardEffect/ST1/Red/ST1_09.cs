// Source: DCGO/Assets/Scripts/CardEffect/ST1/Red/ST1_09.cs
// TRUE AS-IS-verbatim re-port (ST1/Red batch). 1:1 mirror of the original ST1_09 (inherited).
//   [Your Turn] When this Digimon is blocked, gain 3 memory.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.AddMemoryTriggerEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure.
// AS-IS structure kept verbatim: inline ActivateClass, SetIsInheritedEffect(true) (after Add, matching AS-IS
// call order), the redundant `isExistOnField(card)` + `card.Owner.GetBattleAreaDigimons().Contains(...)`
// double-guard in ActivateCoroutine.
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// `isExistOnField(card)` resolves as the inherited `CEntity_Effect.isExistOnField` static method (unqualified,
// same as AS-IS); AS-IS `card.Owner.CanAddMemory(activateClass)` / `card.Owner.AddMemory(3, activateClass)`
// resolve against the `HeadlessPlayerId.CanAddMemory`/`AddMemory` extensions (Script/Player.cs,
// PlayerIdAsIsExtensions); AS-IS `card.Owner.GetBattleAreaDigimons()` resolves against the
// `HeadlessPlayerId.GetBattleAreaDigimons` extension (added this batch, same idiom as the AddMemory sibling —
// see Player.cs); `card.PermanentOfThisCard()` used as the `Permanent` argument to `List<Permanent>.Contains`
// -> `ICardEffect.ResolvePermanentOfThisCard(card)` (established Permanent-vs-PermanentView idiom).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class ST1_09 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnBlockAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +3", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When this Digimon is blocked, gain 3 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanTriggerOnAttack(hashtable, card))
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
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (card.Owner.CanAddMemory(activateClass))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (isExistOnField(card))
                {
                    if (card.Owner.GetBattleAreaDigimons().Contains(ICardEffect.ResolvePermanentOfThisCard(card)))
                    {
                        await card.Owner.AddMemory(3, activateClass);
                    }
                }
            }
        }

        return cardEffects;
    }
}
