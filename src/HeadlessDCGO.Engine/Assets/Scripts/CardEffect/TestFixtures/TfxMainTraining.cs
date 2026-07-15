// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [OnDeclaration] returns the
// faithful <Training> keyword ability (CardEffectFactory.TrainingEffect) — the exact shape AS-IS uses on
// every [Training] Digimon (e.g. EX9_026.cs:31-33: `if (timing == EffectTiming.OnDeclaration)
// cardEffects.Add(CardEffectFactory.TrainingEffect(card))`). Used by tests/G3.5-C5 to drive Training through
// the LIVE player-declared activated path (MainSkillActivateAction -> ActivatedEffectResolver ->
// ActivateClass), proving the re-homed keyword resolves without the retired invented TrainAsync primitive.
// No real card has the number "TfxMainTraining", so this is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxMainTraining : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDeclaration)
        {
            cardEffects.Add(CardEffectFactory.TrainingEffect(card: card));
        }

        return cardEffects;
    }
}
