// Source: DCGO/Assets/Scripts/CardEffect/BT2/Yellow/BT2_087.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_087 (BT2/Yellow, a Tamer).
//   [Start of Your Turn] If you have 3 or fewer security cards, gain 1 memory.
//   [Security] Play this Tamer.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.GainMemoryActivatedEffect(...)` call (an invented
// helper — explicitly prohibited/retired) with the literal AS-IS inline `new ActivateClass()` structure.
// `CardEffectFactory.PlaySelfTamerSecurityEffect` on SecuritySkill IS the real AS-IS call (verbatim, unchanged).
// FIDELITY NOTE: the previous pass collapsed CanUseCondition/CanActivateCondition into a single flattened
// `if` guard on the outer branch — restored as AS-IS's own two-gate split below: CanUseCondition =
// IsExistOnBattleArea && IsOwnerTurn; CanActivateCondition = isExistOnField && (this permanent is on MY OWN
// battle area) && SecurityCards.Count<=3 && CanAddMemory (the battle-area membership re-check is LOAD-BEARING
// per BT2_087's own AS-IS-fidelity note: AS-IS scans hand/trash too and relies on this exact re-check).
// Substrate translations: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// `isExistOnField(card)` (inherited static CEntity_Effect helper); `card.Owner.GetBattleAreaPermanents().
// Contains(card.PermanentOfThisCard())` -> `new Player(card.Context, card.Owner).GetBattleAreaPermanents()
// .Some(p => p.InstanceId == card.PermanentOfThisCard().TopInstanceId)` (Player reconstruction + the
// established Permanent-vs-PermanentView identity idiom, see BT2_002.cs); `card.Owner.SecurityCards.Count`
// -> `CardEffectCommons.SecurityCount(card)`; `card.Owner.CanAddMemory(activateClass)`/`card.Owner.
// AddMemory(1, activateClass)` -> the `HeadlessPlayerId.CanAddMemory`/`AddMemory` extensions (bridge W4/PRIM).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT2_087 : CEntity_Effect
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
                return "[Start of Your Turn] If you have 3 or fewer security cards, gain 1 memory.";
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
                    if (new Player(card.Context, card.Owner).GetBattleAreaPermanents().Some(p => p.InstanceId == card.PermanentOfThisCard().TopInstanceId))
                    {
                        if (CardEffectCommons.SecurityCount(card) <= 3)
                        {
                            if (card.Owner.CanAddMemory(activateClass))
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
