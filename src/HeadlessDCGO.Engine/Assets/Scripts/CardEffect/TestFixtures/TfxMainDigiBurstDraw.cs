// TEST FIXTURE (not a real card). "[Main] <Digi-Burst 2> Draw 1" — a NON-uniform activated skill (DigiBurst body)
// returned at OnDeclaration, the shape of ST4_13's real [Main] Digi-Burst. Exercises the B-2 declare-gate: AS-IS
// CanDigiBurst (ST4_13's CanUseCondition) offers this [Main] skill only when the permanent holds >= 2 TRASHABLE
// digivolution sources, so CanDeclareAt must NOT surface it for a permanent that cannot pay (e.g. no sources).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxMainDigiBurstDraw : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnDeclaration)
        {
            effects.Add(CardEffectFactory.DigiBurstEffect(
                card, count: 2, CardEffectFactory.DrawCardsEffect(card, 1), "[Main] <Digi-Burst 2> Draw 1."));
        }
        return effects;
    }
}
