// TEST FIXTURE (not a real card). [BeforePayCost]: "When this card would be played, reduce its play cost by 3"
// — gated on the card's "allowReduce" metadata flag (default true) so tests can exercise both the applied and
// condition-unmet paths. Used by tests/PRIM-P0 (Build Order 4). Inert in actual play.
//
// (이연③-d EXHAUSTED) Re-ported the retired invented `CardEffectFactory.BeforePayCostReductionEffect(...)` →
// the literal AS-IS inline `[BeforePayCost] ActivateClass` structure (BT18_057 / EX8_074 region #1 template):
// a BeforePayCost activated effect whose ActivateCoroutine registers a ONE-SHOT self ChangeCostClass into the
// owner's `UntilCalculateFixedCostEffect` bucket (cleared once the play's cost is locked). ChangeCostClass IS a
// real AS-IS kind-class (Script/CardEffects/ChangeCostClass.cs); the reduction body is IDENTICAL to the retired
// subtype's Apply() (changeCostFunc `cost - 3`, gated cardSourceCondition `cs == card`, any root — the fixture's
// outer IsPayCostRoot(card, gateRoot) gate targets the intended action). Substrate: IEnumerator->Task; the
// resolver's ActivateICardEffect case runs it during the PlayCard/Digivolve BeforePayCost window.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxBeforePayCostReduction : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        // (B.O.4 #1) gate to a root read from the "gateRoot" metadata ("play" default / "digivolve" / "option")
        // so tests can target each action; a play-cost card gates to Play, a digivolve one to Digivolve.
        var gateRoot = Headless.Bridge.PayCostRoot.Play;
        if (card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? rec) && rec is not null &&
            rec.Metadata.TryGetValue("gateRoot", out object? rootRaw) && rootRaw is string rootStr)
        {
            gateRoot = rootStr switch
            {
                "digivolve" => Headless.Bridge.PayCostRoot.Digivolve,
                "option" => Headless.Bridge.PayCostRoot.Option,
                _ => Headless.Bridge.PayCostRoot.Play,
            };
        }

        if (timing == EffectTiming.BeforePayCost && CardEffectCommons.IsPayCostRoot(card, gateRoot))
        {
            const int reduce = 3;

            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Cost -3.", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, "Cost -3.");
            effects.Add(activateClass);

            // The "allowReduce" metadata gate — the fixture's availability condition (AS-IS CanUseCondition).
            bool CanUseCondition(Hashtable hashtable) =>
                !card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? r) ||
                r is null || !r.Metadata.TryGetValue("allowReduce", out object? raw) || raw is not bool b || b;

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                // AS-IS BeforePayCost: register a ONE-SHOT self ChangeCostClass into the owner's
                // UntilCalculateFixedCost bucket (body identical to the retired BeforePayCostReductionEffect.Apply).
                ChangeCostClass changeCostClass = new ChangeCostClass();
                changeCostClass.SetUpICardEffect($"Cost -{reduce}", _ => true, card);
                changeCostClass.SetUpChangeCostClass(
                    changeCostFunc: (cs, cost, root, targetPermanents) => cost - reduce,
                    cardSourceCondition: cs => cs == card,
                    rootCondition: root => true,
                    isUpDown: () => true,
                    isCheckAvailability: () => false,
                    isChangePayingCost: () => true);
                new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add(_ => changeCostClass);

                await Task.CompletedTask;
            }
        }

        return effects;
    }
}
