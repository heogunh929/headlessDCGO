// TEST FIXTURE (E-3). An Option that forbids ITS OWN play via a [None] CanNotPlayClass whose CardCondition
// matches only itself — exercises AS-IS CanNotPlayThisOption region ③ (`if (PermanentOfThisCard() == null)`
// the option's OWN EffectList(None) is scanned). While the option is in hand it is not a permanent, so region ③
// fires and the option is unplayable.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxOptionForbidsSelf : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            effects.Add(CardEffectFactory.CanNotPlayOptionStaticEffect(
                cardCondition: source => source is not null && source.InstanceId == card.InstanceId,
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        return effects;
    }
}
