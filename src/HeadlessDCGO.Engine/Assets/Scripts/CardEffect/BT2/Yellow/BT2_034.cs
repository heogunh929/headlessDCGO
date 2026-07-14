// Source: DCGO/Assets/Scripts/CardEffect/BT2/Yellow/BT2_034.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the original
// BT2_034 (BT2/Yellow).
//   [On Deletion] If you have 3 or fewer security cards, trigger <Recovery +1 (Deck)>.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + local functions. CanUseCondition =
// CanTriggerOnDeletion; CanActivateCondition = IsExistOnTrash && owner.LibraryCards.Count >= 1 &&
// owner.SecurityCards.Count <= 3 && owner.CanAddSecurity(activateClass); ORDER=-1, ISOPTIONAL=false;
// ActivateCoroutine = new IRecovery(owner, 1, activateClass).Recovery().  (The previous old-model port's
// CanActivate dropped the AS-IS LibraryCards>=1 and CanAddSecurity guards — restored here.)
// Substrate translations only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// `card.Owner.LibraryCards`/`.SecurityCards`/`.CanAddSecurity(ICardEffect)` (AS-IS live Player members) ->
// `new Player(card.Context, card.Owner)` reconstruction (established BT1_081 idiom; mirror `CanAddSecurity`
// takes the cause id -> `activateClass.EffectSourceCard?.InstanceId`); `new IRecovery(card.Owner, 1,
// activateClass)` -> the mirror carrier ctor `(EngineContext, HeadlessPlayerId, int, HeadlessEntityId? cause)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT2_034 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Recovery +1 (Deck)", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Deletion] If you have 3 or fewer security cards, trigger <Recovery +1 (Deck)>. (Place the top card of your deck on top of your security stack.)";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnDeletion(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnTrash(card))
                {
                    Player owner = new Player(card.Context, card.Owner);

                    if (owner.LibraryCards.Count >= 1)
                    {
                        if (owner.SecurityCards.Count <= 3)
                        {
                            if (owner.CanAddSecurity(activateClass.EffectSourceCard?.InstanceId))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new IRecovery(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Recovery();
            }
        }

        return cardEffects;
    }
}
