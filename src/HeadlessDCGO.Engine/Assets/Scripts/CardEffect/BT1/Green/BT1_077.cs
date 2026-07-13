// Source: DCGO/Assets/Scripts/CardEffect/BT1/Green/BT1_077.cs
// TRUE AS-IS-verbatim re-port (P5 batch 2). 1:1 mirror of the original BT1_077 (BT1/Green).
//   [Your Turn] When this Digimon deletes an opponent's Digimon in battle and survives, gain 1 memory.
// AS-IS structure kept verbatim: inline ActivateClass on OnEndBattle. AS-IS's Hashtable-based
// `CanTriggerWhenDeleteOpponentDigimonByBattle(Hashtable, Func<Permanent,bool>, Func<Permanent,bool>, bool,
// ...)` bridge already exists 1:1 (CanUseEffects/WhenDeleteOpponentDigimonByBattle.cs) — used directly, no
// ctx-shape substitution needed. AS-IS `permanent.cardSources.Contains(card)` (WinnerCondition) — the winning
// permanent is the one this card belongs to (top or a digivolution source) — expressed via the established
// id idiom (ST4_11/BT1_112 precedent): a winner Permanent's InstanceId is its top card; `card.
// PermanentOfThisCard().TopInstanceId` is the top of the stack this card is part of — equal exactly when this
// card belongs to that winner.
// UNRESOLVED (kept verbatim, logged): AS-IS `card.Owner.CanAddMemory(activateClass)` — see BT1_030.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_077 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEndBattle)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When this Digimon deletes an opponent's Digimon in battle and survives, gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        bool WinnerCondition(Permanent permanent) =>
                            card.PermanentOfThisCard() is { TopInstanceId: var top } && !top.IsEmpty && permanent.InstanceId == top;
                        bool LoserCondition(Permanent permanent) => CardEffectCommons.IsOpponentPermanent(permanent, card);

                        if (CardEffectCommons.CanTriggerWhenDeleteOpponentDigimonByBattle(
                            hashtable: hashtable,
                            winnerCondition: WinnerCondition,
                            loserCondition: LoserCondition,
                            isOnlyWinnerSurvive: true))
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
                    // UNRESOLVED (see file header): AS-IS `card.Owner.CanAddMemory(activateClass)`.
                    if (card.Owner.CanAddMemory(activateClass))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        return cardEffects;
    }
}
