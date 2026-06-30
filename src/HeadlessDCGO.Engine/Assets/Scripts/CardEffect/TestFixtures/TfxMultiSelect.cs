// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [Main] returns TWO separate
// select-and-destroy effects, so activating it requires the agent to make TWO choices in one activation.
// Used by tests/G12-002.MultiChoiceActivationE2E to exercise the multi-choice deferred resume loop. No
// real card has the number "TfxMultiSelect", so this is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxMultiSelect : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelect(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            cardEffects.Add(CardEffectFactory.SelectAndDestroyEffect(
                card: card, canTarget: CanSelect, maxCount: 1, canEndNotMax: false, description: "[Main] Delete 1 of your opponent's Digimon (first)."));
            cardEffects.Add(CardEffectFactory.SelectAndDestroyEffect(
                card: card, canTarget: CanSelect, maxCount: 1, canEndNotMax: false, description: "[Main] Delete 1 of your opponent's Digimon (second)."));
        }

        return cardEffects;
    }
}
