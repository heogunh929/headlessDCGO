// TEST FIXTURE (not a real card). [Main] (OptionSkill) is the AS-IS DigivolveIntoHandOrTrashCard shape: select
// 1 of the owner's battle-area Digimon and a source Digimon in hand, then digivolve. The cost mode is read from
// the "digCost" metadata flag ("free" or "normal"). Used by tests/PRIM-P0 (Build Order 3 batch C). Inert in play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxSelectDigivolve : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing != EffectTiming.OptionSkill)
        {
            return effects;
        }

        string costMode = card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? record) &&
                          record is not null && record.Metadata.TryGetValue("digCost", out object? raw) && raw is string s
            ? s
            : "free";

        DigivolveCost cost = costMode == "normal" ? DigivolveCost.Normal : DigivolveCost.Free;

        effects.Add(CardEffectFactory.SelectAndDigivolveEffect(
            card,
            ChoiceZone.Hand,
            sourcePredicate: _ => true,
            targetPredicate: id => CardEffectCommons.IsOwnerBattleAreaDigimon(card, id),
            cost,
            costAmount: 0,
            "Digivolve 1 of your Digimon from your hand."));

        return effects;
    }
}
