// TEST FIXTURE (not a real card). Returns a CannotAddSecurity kind-class at EffectTiming.None built from the
// static config the harness sets before placing the card (the restricted player + optional causing-effect
// predicate). Consumed by the AS-IS-literal live scan Player.CanAddSecurity / the sink's AddToSecurity restriction
// gate (R3-W3c-4c B3). Used by tests/PRIM-P0.CannotAddSecurity + tests/FAILa-PPS. Inert in play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxCannotAddSecurity : CEntity_Effect
{
    /// <summary>The restricted (scope) player; set by the harness before the fixture card is placed.</summary>
    public static HeadlessPlayerId ScopePlayer;

    /// <summary>Optional AS-IS CardEffectCondition (fires only when the causing effect's source matches); null =
    /// block every add. Set by the harness before placing.</summary>
    public static Func<CardSource, bool>? CausingPredicate;

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            effects.Add(CardEffectFactory.CanNotAddSecurityStaticEffect(
                ScopePlayer, isInheritedEffect: false, card, condition: null, causingEffectPredicate: CausingPredicate));
        }
        return effects;
    }
}
