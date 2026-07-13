// Source: DCGO/Assets/Scripts/CardEffect/ST3/Yellow/ST3_09.cs
// TRUE AS-IS-verbatim re-port (ST3 Yellow batch). 1:1 mirror of the original ST3_09 (ST3/Yellow).
//   [When Digivolving] When you have 3 security cards or less, trigger <Recovery +1 (Deck)>. (Place the top
//   card of your deck on top of your security stack.)
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.RecoveryTriggerEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure.
// AS-IS structure kept verbatim: inline ActivateClass, no SetIsInheritedEffect/SetHashString (AS-IS omits both
// here).
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`.
// AS-IS `card.Owner.SecurityCards.Count` -> `CardEffectCommons.SecurityCount(card)`. AS-IS
// `card.Owner.LibraryCards.Count` (Player field) -> the mirror zone-state read (`IZoneStateReader.GetCards`),
// the same translation the BT1_010 bridge uses. AS-IS `card.Owner.CanAddSecurity(activateClass)` — the mirror
// `Player.CanAddSecurity` takes a `HeadlessEntityId?` cause id (not `ICardEffect`) and no bare-id
// `HeadlessPlayerId` extension exists for it, but an instance `Player` handle IS reachable at the call site
// (`new Player(card.Context, card.Owner)`, the same idiom `SelectPermanentEffect.cs:615`'s
// `Mode.PutSecurityBottom/Top` uses), so it is constructed inline rather than left unresolved; the cause id is
// `card.InstanceId` (== `activateClass.EffectSourceCard.InstanceId`, since `SetUpICardEffect` sets
// `EffectSourceCard = card`). AS-IS `new IRecovery(card.Owner, 1, activateClass).Recovery()` -> the mirror
// `IRecovery(EngineContext, HeadlessPlayerId, int, HeadlessEntityId?)` constructor (CardController.cs), same
// cause-id translation.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST3_09 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // DISPATCH REMAP (batch-3): AS-IS registers under EffectTiming.OnEnterFieldAnyone (the shared
        // enter-field timing, discriminated by the CanTriggerWhenDigivolving hashtable gate). The mirror
        // engine dispatches [When Digivolving] activated effects on the DEDICATED WhenDigivolving key
        // (DigivolveAction.cs ResolveAsync(..., EffectTiming.WhenDigivolving)) and plain plays on
        // OnEnterFieldAnyone — registering under the literal AS-IS key would silently never fire on digivolve
        // (batch-2 BT1_025/BT1_062 established this exact remap; the AS-IS gate below is kept verbatim).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Recovery +1 (Deck)", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] When you have 3 security cards or less, trigger <Recovery +1 (Deck)>. (Place the top card of your deck on top of your security stack.)";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.SecurityCount(card) <= 3)
                    {
                        if (((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Library).Count >= 1)
                        {
                            if (new Player(card.Context, card.Owner).CanAddSecurity(card.InstanceId))
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
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    await new IRecovery(card.Context, card.Owner, 1, card.InstanceId).Recovery();
                }
            }
        }

        return cardEffects;
    }
}
