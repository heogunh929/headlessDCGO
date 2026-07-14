// Source: DCGO/Assets/Scripts/CardEffect/BT8/Black/BT8_092.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [Your Turn] OnMove branch.
//   [Your Turn] When one of your Digimon with [X-Antibody] in its traits is moved from your breeding area to your
//   battle area, gain 1 memory and <Draw 1>.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpICardEffect/SetUpActivateClass + local
// functions. CanUseCondition/CanActivateCondition/ActivateCoroutine mirror the gold 1:1 (BT8_092.cs:15-79),
// including the AS-IS CanActivate OR ((CanAddMemory || LibraryCards>=1)) restored verbatim, and the coroutine's
// Draw-1-THEN-gain-1-memory order. Substrate translations only: IEnumerator->Task, StartCoroutine->await;
// `new DrawClass(card.Owner, 1, activateClass)` -> the mirror ctor (EngineContext/HeadlessPlayerId/cause-id,
// established BT2_070/BT1_003 idiom); `card.Owner.AddMemory(1, activateClass)`/`card.Owner.CanAddMemory(...)`
// -> the mirror HeadlessPlayerId extensions; `card.Owner.LibraryCards` (AS-IS live Player field) ->
// `new Player(card.Context, card.Owner).LibraryCards` (established BT1_081 Player-handle idiom).
//
// The AS-IS second effect ([Your Turn] OnAllyAttack: suspend-cost place-under-digivolution via SelectHandEffect,
// BT8_092.cs:82-196) remains UNPORTED — the mirror `SelectHandEffect` is a 0-type skeleton (design item RD-P8-01
// / RD-P6C3-D2, same root gap as BT1_039). Deliberately omitted, as in the prior pass.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT8.Black;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT8_092 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnMove)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1 and Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When one of your Digimon with [X-Antibody] in its traits is moved from your breeding area to your battle area, gain 1 memory and <Draw 1>.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent.TopCard.HasXAntibodyTraits)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerOnMove(hashtable, PermanentCondition))
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
                    if (card.Owner.CanAddMemory(activateClass))
                    {
                        return true;
                    }

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

                await card.Owner.AddMemory(1, activateClass);
            }
        }

        return cardEffects;
    }
}
