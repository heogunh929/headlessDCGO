// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [BeforePayCost] returns a
// SuspendCostReductionEffect: "When this card would be played, by suspending 2 Digimon, reduce the play
// cost by 4." This mirrors EX8_074's effect #1 in isolation so tests/G9-006 can exercise the PlayCardAction
// pre-payment window (Stage 3 brick 2) end-to-end via the real play action. No real card has the number
// "TfxBeforePayCost", so this is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxBeforePayCost : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // [When Would be Played] By suspending 2 Digimon, reduce the play cost by 4. (B.O.4 #1) Gated to the
        // PLAY root so it does NOT fire when this card is digivolved/option-activated (AS-IS rootCondition).
        if (timing == EffectTiming.BeforePayCost && CardEffectCommons.IsPayCostRoot(card, HeadlessDCGO.Engine.Headless.Bridge.PayCostRoot.Play))
        {
            bool CanSuspendTarget(HeadlessEntityId id) =>
                CardEffectCommons.IsOwnerBattleAreaDigimon(card, id) && !CardEffectCommons.IsSuspended(card, id);

            // Mirror EX8_074's CanActivate gate: only offer the effect when >= 2 Digimon are suspendable.
            if (CardEffectCommons.MatchConditionPermanentCount(card, CanSuspendTarget) >= 2)
            {
                const string desc = "Suspend 2 Digimon to get Play Cost -4.";
                cardEffects.Add(new ActivatedEffect(
                    card, EffectTiming.None, canUse: null, canActivate: null,
                    body: new SuspendCostReductionEffect(card, CanSuspendTarget, suspendCount: 2, costReduction: 4, description: desc),
                    maxCountPerTurn: null, isOptional: false, desc));
            }
        }

        return cardEffects;
    }
}
