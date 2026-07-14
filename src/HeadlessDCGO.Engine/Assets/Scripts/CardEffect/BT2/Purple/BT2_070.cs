// Source: DCGO/Assets/Scripts/CardEffect/BT2/Purple/BT2_070.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the original
// BT2_070 (BT2/Purple).
//   [On Deletion] Trigger <Draw 1>. (Draw 1 card from your deck.)
// AS-IS structure kept verbatim: inline `new ActivateClass()` + local functions. CanUseCondition =
// CanTriggerOnDeletion; CanActivateCondition = IsExistOnTrash && owner.LibraryCards.Count >= 1; ORDER=-1,
// ISOPTIONAL=false; ActivateCoroutine = new DrawClass(owner, 1, activateClass).Draw().
// Substrate translations only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// `card.Owner.LibraryCards` (AS-IS live Player property) -> `new Player(card.Context, card.Owner).LibraryCards`
// (mirror `CardSource.Owner` is a bare HeadlessPlayerId; the Player handle is reconstructed, the established
// BT1_081 idiom); `new DrawClass(card.Owner, 1, activateClass)` -> the mirror carrier ctor
// `(EngineContext, HeadlessPlayerId, int, HeadlessEntityId? cause)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Purple;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT2_070 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Deletion] Trigger <Draw 1>. (Draw 1 card from your deck.)";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnDeletion(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnTrash(card))
                {
                    if (new Player(card.Context, card.Owner).LibraryCards.Count >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Draw();
            }
        }

        return cardEffects;
    }
}
