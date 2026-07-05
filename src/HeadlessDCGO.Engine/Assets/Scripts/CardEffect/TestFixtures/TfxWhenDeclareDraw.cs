// TEST FIXTURE. "[When Attacking] (OnDeclaration) Draw 1" — an activated draw returned at the OnDeclaration
// (attack-declaration) timing, resolved by the auto-processing bridge (v4). This is the timing Digi-Burst bodies
// (BT6_028) are declared at.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxWhenDeclareDraw : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnDeclaration)
        {
            effects.Add(CardEffectFactory.DrawCardsEffect(card, 1));
        }
        return effects;
    }
}
