namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
public sealed class TfxZeroTrashXros : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var e = new List<ICardEffect>();
        if (timing == EffectTiming.None)
            e.Add(CardEffectFactory.DigiXrosWithExtraMaterialsEffect(card, 0, _ => 0, null, new SpecialPlayMaterial(cs => cs.CardNumber == "MAT", "MAT")));
        return e;
    }
}
