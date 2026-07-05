// TEST FIXTURE (not a real card). [BeforePayCost] returns a non-interactive BeforePayCostReductionEffect:
// "When this card would be played, reduce its play cost by 3" — gated on the card's "allowReduce" metadata
// flag (default true) so tests can exercise both the applied and condition-unmet paths. Mirrors the AS-IS
// BeforePayCost ActivateClass that registers a UntilCalculateFixedCost cost reduction (e.g. BT18_057). Used by
// tests/PRIM-P0 (Build Order 4). Inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxBeforePayCostReduction : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.BeforePayCost)
        {
            bool Condition() =>
                !card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? r) ||
                r is null || !r.Metadata.TryGetValue("allowReduce", out object? raw) || raw is not bool b || b;

            effects.Add(CardEffectFactory.BeforePayCostReductionEffect(card, amount: 3, condition: Condition, "Play cost -3."));
        }

        return effects;
    }
}
