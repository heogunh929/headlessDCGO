// TEST FIXTURE (not a real card). A CAPPED ([Once Per Turn]) UNIFORM activated effect with an INTERACTIVE body:
// "[Once Per Turn] select 1 card in your hand and trash it, then unsuspend this." at OnEnterFieldAnyone. Exercises
// B-1 (P1-3): the interactive body suspends mid-choice (DeferredChoicePendingException); the per-turn cap must be
// consumed AFTER the body completes, not before — otherwise the resumed re-invocation's CanActivate re-check reads a
// spent cap and BREAK-vanishes the effect with its use wasted. Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class TfxOncePerTurnInteractiveTrash : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: null,
                canActivate: null,
                body: new SelectTrashHandThenSelfMutationBody(1, MatchStateMutationSink.UnsuspendKind, "select 1 card in your hand to trash"),
                maxCountPerTurn: 1,
                isOptional: false,
                description: "[Once Per Turn] Select 1 card in your hand and trash it, then unsuspend this."));
        }

        return effects;
    }
}
