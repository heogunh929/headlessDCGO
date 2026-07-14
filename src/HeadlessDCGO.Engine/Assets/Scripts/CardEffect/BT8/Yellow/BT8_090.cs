// Source: DCGO/Assets/Scripts/CardEffect/BT8/Yellow/BT8_090.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [Your Turn] OnAddSecurity
// branch. The OnStartTurn (SetMemoryTo3TamerEffect) and SecuritySkill (PlaySelfTamerSecurityEffect) branches are
// already the real AS-IS factory calls and are untouched.
//   [Your Turn] When a card is added to your security stack, you may suspend this Tamer to gain 1 memory.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpICardEffect/SetUpActivateClass + local
// functions (BT8_090.cs:20-67); ORDER=-1 (uncapped), ISOPTIONAL=true ("you may"). Substrate translations only:
// IEnumerator->Task, StartCoroutine->await; `new SuspendPermanentsClass(list, CardEffectHashtable(activateClass))
// .Tap()` -> the mirror ctor `(List<Permanent>, HeadlessEntityId? cause, bool isBlock:false).Tap()` (established
// BT1_110/BT1_088 idiom); `card.PermanentOfThisCard()` -> `ICardEffect.ResolvePermanentOfThisCard(card)`;
// `card.Owner.AddMemory(1, activateClass)` -> the mirror HeadlessPlayerId extension; AS-IS
// `player => player == card.Owner` -> `player.PlayerId == card.Owner` (the Hashtable-overload playerCondition is a
// mirror `Player`; `.PlayerId` is its HeadlessPlayerId, the established BT8_057 identity idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT8.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT8_090 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
        }

        if (timing == EffectTiming.OnAddSecurity)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When a card is added to your security stack, you may suspend this Tamer to gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerWhenAddSecurity(hashtable, player => player.PlayerId == card.Owner))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanActivateSuspendCostEffect(card))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new SuspendPermanentsClass(
                    new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) },
                    activateClass.EffectSourceCard?.InstanceId,
                    isBlock: false).Tap();

                await card.Owner.AddMemory(1, activateClass);
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
