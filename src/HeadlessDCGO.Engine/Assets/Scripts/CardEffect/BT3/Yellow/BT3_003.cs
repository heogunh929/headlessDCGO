// STOP: AS-IS BT3_003 [When Attacking][Once Per Turn] gates a self-fired <Draw 1> on
// "3 or fewer security cards", with the [Once Per Turn] limit encoded via
// activateClass.SetUpActivateClass(.., maxCountPerTurn: 1, ..) + SetHashString/SetIsInheritedEffect.
// No existing headless primitive covers this shape: CardEffectFactory.DrawCardsEffect (DrawEffect) is an
// IActivatedCardEffect with only Apply(sink) — it is never converted to an EffectBinding (ToBinding throws),
// so it cannot carry a CardEffectDefinition maxCountPerTurn/hash limiter the way TriggeredMemoryEffect /
// RecoverTriggerEffect do for their own auto-trigger stats. Wiring Draw here would either drop the
// [Once Per Turn] gate (forbidden guard relaxation) or require a new "triggered auto-draw with
// maxCountPerTurn" primitive (primitive development is out of scope for this pass). See also the
// project's "Triggered-activated bridge" gap note (docs/audit memory) for the broader category.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_003 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [When Attacking][Once Per Turn] If security <= 3, trigger <Draw 1> — no primitive covers a
        // maxCountPerTurn-gated auto-draw trigger (see file header).

        return cardEffects;
    }
}
