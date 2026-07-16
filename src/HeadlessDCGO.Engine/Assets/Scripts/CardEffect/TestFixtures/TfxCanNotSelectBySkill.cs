// TEST FIXTURE (not a real card). Returns a CanNotSelectBySkill (untargetability) kind-class at EffectTiming.None
// carrying the joint predicate CanNotSelectBySkill(candidate, skillSource) from the static Predicate slot the
// harness sets before placing the card. Consumed by the AS-IS-literal live getter Permanent.CanSelectBySkill
// (R3-W3c-4c D-1) reached by SelectPermanentEffect.IsUntargetableBySkill / CanTargetAsIs. Used by tests/FAILd-01.
// Inert in play (no real card dispatches to it).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxCanNotSelectBySkill : CEntity_Effect
{
    /// <summary>The joint AS-IS predicate CanNotSelectBySkill(candidate, skillSource); set by the harness before
    /// the fixture card is placed. Null ⇒ the fixture contributes no untargetability.</summary>
    public static Func<CardSource, CardSource, bool>? Predicate;

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None && Predicate is { } predicate)
        {
            effects.Add(CardEffectFactory.CanNotSelectBySkillStaticEffect(predicate, card, condition: null));
        }
        return effects;
    }
}
