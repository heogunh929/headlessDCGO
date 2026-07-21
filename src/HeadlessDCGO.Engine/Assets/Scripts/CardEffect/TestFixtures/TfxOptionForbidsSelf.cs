// TEST FIXTURE (E-3). An Option that forbids ITS OWN play via a [None] CanNotPlayClass whose CardCondition
// matches only itself — exercises AS-IS CanNotPlayThisOption region ③ (`if (PermanentOfThisCard() == null)`
// the option's OWN EffectList(None) is scanned). While the option is in hand it is not a permanent, so region ③
// fires and the option is unplayable.
// (이연④-f) Emits the AS-IS kind-class `CanNotPlayClass` (an ICanNotPlayCardEffect) directly, so region ③ is served
// by the LIVE interface-scan (CanNotPlayOptionScan option.EffectList(None) ↦ ICanNotPlayCardEffect), NOT the retired
// ContinuousCanNotPlayOptionEffect registry/dispatch half.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOptionForbidsSelf : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            CanNotPlayClass canNotPlayClass = new CanNotPlayClass();
            canNotPlayClass.SetUpICardEffect("Forbids own play", (Hashtable hashtable) => true, card);
            canNotPlayClass.SetUpCanNotPlayClass(
                cardCondition: source => source is not null && source.InstanceId == card.InstanceId);
            effects.Add(canNotPlayClass);
        }

        return effects;
    }
}
