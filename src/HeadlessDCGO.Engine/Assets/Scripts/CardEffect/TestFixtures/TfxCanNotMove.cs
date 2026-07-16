// TEST FIXTURE (not a real card). Returns a CanNotMove kind-class at EffectTiming.None carrying the joint
// predicate CanNotMove(candidate, causingSource) from the static Predicate slot the harness sets before placing
// the card. Consumed by the AS-IS-literal live move-gate scan (R3-W3c-4c D-1) in HeadlessLegalActionDispatcher
// (AS-IS Permanent.CanMove — the gate passes a null causing effect). Used by tests/FAILd-03. Inert in play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxCanNotMove : CEntity_Effect
{
    /// <summary>The joint AS-IS predicate CanNotMove(candidate, causingSource); set by the harness before the
    /// fixture card is placed. Null ⇒ the fixture contributes no move restriction.</summary>
    public static Func<CardSource, CardSource?, bool>? Predicate;

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None && Predicate is { } predicate)
        {
            effects.Add(CardEffectFactory.CanNotMoveStaticEffect(predicate, card, condition: null));
        }
        return effects;
    }
}
