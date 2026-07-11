// TEST FIXTURE (not a real card). An OPTIONAL, CAPPED [Main] declaration skill (&lt;Draw 1&gt;), for the AS-IS
// declaration-path register order: the main loop registers the per-turn use BEFORE the optional prompt
// (TurnStateMachine.cs:1183-1186 — register, then ActivateEffectProcess asks yes/no), so DECLINING a declared
// capped [Main] skill leaves the use CONSUMED (that path has no RemoveUse). The resolver's `declarative` flavor
// mirrors this: consume before ConfirmOptionalAsync. Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxMainOptionalDraw : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnDeclaration)
        {
            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnDeclaration,
                canUse: null,
                canActivate: null,
                body: new DrawBody(1),
                maxCountPerTurn: 1,
                isOptional: true,
                description: "[Main] [Once Per Turn] You may draw 1 card."));
        }

        return effects;
    }
}
