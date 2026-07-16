// TEST FIXTURE (not a real card). Returns an ImmuneStackTrashing kind-class at EffectTiming.None (AS-IS
// ImmuneStackTrashingClass — "Isn't affected by trashing any stacked card", BT21_060 shape). The EffectCondition
// gates on the CAUSING effect: only an OPPONENT effect's source-trash is blocked (CardEffectCommons.IsOpponentEffect),
// exactly BT21_060's `EffectCondition = IsOpponentEffect(effect, card)` — so the witness can exercise the cause
// predicate (opponent cause = immune, own cause = not immune). Consumed by the AS-IS-literal live getter
// Permanent.ImmuneFromStackTrashing(ICardEffect) (R3-W3c B6), reached through the mutation sink / CardController /
// ActivatedEffectResolver stack-trash gates. permanentCondition null → protects exactly this card's permanent
// (single-permanent fixture). Used by tests/G9-040. Inert in play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxImmuneStackTrashing : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            effects.Add(CardEffectFactory.CanNotBeTrashedBySkillStaticEffect(
                permanentCondition: null,
                cardEffectCondition: effect => CardEffectCommons.IsOpponentEffect(effect?.EffectSourceCard, card),
                isInheritedEffect: false, card, condition: null,
                effectName: "Isn't affected by trashing any stacked card"));
        }
        return effects;
    }
}
