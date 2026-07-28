// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_109.cs
// 1:1 mirror of the original BT1_109 (BT1/Green) — an Option (single OptionSkill block).
//   [Main] "For the turn, the next time you would digivolve one of your green Digimon from level 5 to level
//   6, decrease the digivolution cost by 4."
// AS-IS (OptionSkill): ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false).
//   ActivateCoroutine registers, via AddEffectToPlayer(UntilEachTurnEnd), a ChangeCostClass whose ChangeCost
//   reduces the digivolution cost by 4 ONLY when BOTH:
//     - CardSourceCondition(cardSource): the digivolving-TO card is level 6 && HasLevel, AND
//     - PermanentsCondition(targetPermanents): >=1 target permanent is the owner's own battle-area Digimon,
//       green, level 5, HasLevel (the digivolving-FROM permanent).
//   It is paired with a background cleanup ActivateClass (CanTriggerWhenPermanentWouldDigivolve) that removes
//   BOTH effects on the first matching digivolve — the AS-IS "next time only, this turn only" one-shot.
//
// (R6-Da'-3) Re-ported the outer invented `new ActivatedEffect(... GrantPlayerScopeRestrictionBody ...)` to the
// AS-IS-verbatim inline `new ActivateClass()` (SetUpActivateClass(null, ActivateCoroutine, -1, false, ...)) whose
// ActivateCoroutine calls the grant directly — same shape as ST2_14 / ST4_12. The grant target was ALREADY on
// the AS-IS canonical bucket path (RegisterDigivolutionCostDeltaForPlayer builds a ChangeCostClass and stores it
// via AddEffectToPlayer(UntilEachTurnEnd); read back by ContinuousModifierGate.ResolveDigivolutionCost's
// GetChangedCostItselef/GetChangedPayingCost fold — NO EffectRegistry). This flip removes the last invented
// wrapper (ActivatedEffect + GrantPlayerScopeRestrictionBody) from this card.
//
// FIDELITY DEBT (documented): headless has no player-scope WhenDigivolving trigger delivery to a trashed Option
// card, so the AS-IS one-shot cleanup (remove after the FIRST matching digivolve) is not mirrored — the
// reduction lasts until end of turn and would apply to EVERY matching green-5→6 digivolve that turn rather than
// only the next one. Scope is exact (correct lines only); the sole deviation is one-shot vs. once-per-matching-
// line-this-turn. See RegisterDigivolutionCostDeltaForPlayer's remarks.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_109 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            // AS-IS CardSourceCondition: the digivolving-TO card is level 6 && HasLevel.
            bool ToCardCondition(CardSource toCard) => toCard.Level == 6 && toCard.HasLevel;

            // AS-IS PermanentCondition: the digivolving-FROM permanent is the owner's own battle-area Digimon,
            // green, level 5, HasLevel.
            bool TargetPermanentCondition(CardSource targetCard)
            {
                var permanent = new Permanent(targetCard.Context, targetCard.InstanceId, targetCard.Owner);
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card)
                    && permanent.TopCard.HasCardColor("Green")
                    && permanent.Level == 5
                    && permanent.TopCard.HasLevel;
            }

            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[Main] For the turn, the next time you would digivolve one of your green Digimon from level 5 to level 6, decrease the digivolution cost by 4.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                // AS-IS ActivateCoroutine: register the two-sided digivolution-cost delta into the OWNER player's
                // UntilEachTurnEnd bucket (a ChangeCostClass via AddEffectToPlayer). Read back by the
                // digivolution-cost pipeline's ChangeCostClass fold.
                CardEffectCommons.RegisterDigivolutionCostDeltaForPlayer(
                    card, delta: -4, EffectDuration.UntilEachTurnEnd, ToCardCondition, TargetPermanentCondition);
                await Task.CompletedTask;
            }
        }

        return cardEffects;
    }
}
