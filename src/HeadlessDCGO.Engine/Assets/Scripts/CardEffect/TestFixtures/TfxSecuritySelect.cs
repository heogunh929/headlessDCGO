// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [Security] returns a
// select-and-destroy effect, so resolving it as a revealed security card requires the agent to make a
// choice. Used by tests/G12-004.SecuritySkillDeferredE2E to exercise the [Security] deferred resume path
// (SecurityResolver suspends -> DeferredActivations -> ResolveChoice resumes). No real card has the number
// "TfxSecuritySelect", so this is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxSecuritySelect : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.SecuritySkill)
        {
            bool CanSelect(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            cardEffects.Add(CardEffectFactory.SelectAndDestroyEffect(
                card: card, canTarget: CanSelect, maxCount: 1, canEndNotMax: false,
                description: "[Security] Delete 1 of your opponent's Digimon."));
        }

        return cardEffects;
    }
}
