// TEST FIXTURE (not a real card). A CAPPED ([Once Per Turn]) UNIFORM activated effect at OnDeclaration —
// "[Main] [Once Per Turn] draw 1" — the shape of a player-declared [Main] skill (AS-IS OnDeclaration
// ActivateICardEffect, e.g. BT11_061 / BT13_050). Exercises B-2 (P1-5): the MainSkillActivateAction offers it as
// an ActivateMain legal move while under cap, resolving it consumes the per-turn use (shared resolver), and once
// spent CanDeclareAt no longer offers it — until ResetForTurn. Non-interactive body so it always executes.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxMainDeclareDraw : CEntity_Effect
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
                isOptional: false,
                description: "[Main] [Once Per Turn] Draw 1."));
        }

        return effects;
    }
}
